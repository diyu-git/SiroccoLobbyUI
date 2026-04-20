using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MelonLoader;

namespace SiroccoLobby.Services
{
    /// <summary>
    /// Harmony patches that turn Mirror's WartideNetworkManager lifecycle callbacks into
    /// C# events on NetworkIntegrationService. This is the source of truth for network
    /// state changes — there is no per-frame polling.
    ///
    /// Mirror is event-driven internally; we only translate its overrides into our own
    /// event surface. The actual feature wiring lives in NetworkIntegrationService.
    /// </summary>
    public static class NetworkLifecyclePatches
    {
        private static bool _applied;

        private const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.NonPublic |
                                           BindingFlags.Instance | BindingFlags.Static;

        public static void Apply(HarmonyLib.Harmony harmony)
        {
            if (_applied) return;

            try
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                if (asm == null)
                {
                    MelonLogger.Warning("[NetworkLifecyclePatches] Assembly-CSharp not loaded yet — patches deferred.");
                    return;
                }

                var wnmType = asm.GetType("Il2CppWartide.WartideNetworkManager");
                if (wnmType == null)
                {
                    MelonLogger.Warning("[NetworkLifecyclePatches] WartideNetworkManager type not found.");
                    return;
                }

                // ── Server start ──
                // StartSinglePlayer / StartSinglePlayerWithSteamP2P never call
                // OnStartServer (they use a custom setup path). Patch them directly.
                Patch(harmony, wnmType, "StartSinglePlayer", nameof(Postfix_OnStartServer));
                Patch(harmony, wnmType, "StartSinglePlayerWithSteamP2P", nameof(Postfix_OnStartServer));
                // Also patch OnStartServer as a belt-and-suspenders catch-all
                // (e.g. if Mirror calls it through a different code path).
                Patch(harmony, wnmType, "OnStartServer", nameof(Postfix_OnStartServer));

                // ── Server stop ──
                Patch(harmony, wnmType, "StopServer", nameof(Postfix_StopServer));

                // ── Peer connect/disconnect ──
                // Game uses OnServerConnectInternal (not OnServerConnect).
                Patch(harmony, wnmType, "OnServerConnectInternal", nameof(Postfix_OnServerConnect));
                Patch(harmony, wnmType, "OnServerDisconnect", nameof(Postfix_OnServerDisconnect));

                // ── Player added ──
                Patch(harmony, wnmType, "OnServerAddPlayer", nameof(Postfix_OnServerAddPlayer));

                // ── Client lifecycle ──
                Patch(harmony, wnmType, "OnClientAuthenticated", nameof(Postfix_OnClientAuthenticated));
                Patch(harmony, wnmType, "OnClientDisconnect", nameof(Postfix_OnClientDisconnect));

                _applied = true;
                MelonLogger.Msg("[NetworkLifecyclePatches] Applied — network state is now event-driven.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[NetworkLifecyclePatches] Apply failed: {ex}");
            }
        }

        private static void Patch(HarmonyLib.Harmony harmony, Type type, string methodName, string postfixName)
        {
            var method = type.GetMethod(methodName, FLAGS);
            if (method == null)
            {
                MelonLogger.Warning($"[NetworkLifecyclePatches] Method not found: WartideNetworkManager.{methodName}");
                return;
            }

            var postfix = new HarmonyMethod(typeof(NetworkLifecyclePatches).GetMethod(postfixName, FLAGS));
            harmony.Patch(method, postfix: postfix);
        }

        // ============================================================
        // Postfixes — fire and forget; never throw back into Mirror.
        // ============================================================

        // Guard: OnStartServer may fire multiple times (from StartSinglePlayer
        // postfix + from OnStartServer itself if it happens to be called).
        // Only dispatch once per server session.
        private static bool _serverStartNotified;

        public static void Postfix_OnStartServer()
        {
            if (_serverStartNotified) return;
            _serverStartNotified = true;
            try { NetworkIntegrationService.NotifyServerStarted(); }
            catch (Exception ex) { MelonLogger.Warning($"[NetworkLifecyclePatches] ServerStarted dispatch failed: {ex.Message}"); }
        }

        public static void Postfix_StopServer()
        {
            _serverStartNotified = false; // Reset so next server start is detected
            try { NetworkIntegrationService.NotifyServerStopped(); }
            catch (Exception ex) { MelonLogger.Warning($"[NetworkLifecyclePatches] StopServer dispatch failed: {ex.Message}"); }
        }

        public static void Postfix_OnServerConnect()
        {
            try { NetworkIntegrationService.NotifyServerPeerConnected(); }
            catch (Exception ex) { MelonLogger.Warning($"[NetworkLifecyclePatches] OnServerConnect dispatch failed: {ex.Message}"); }
        }

        public static void Postfix_OnServerDisconnect()
        {
            try { NetworkIntegrationService.NotifyServerPeerDisconnected(); }
            catch (Exception ex) { MelonLogger.Warning($"[NetworkLifecyclePatches] OnServerDisconnect dispatch failed: {ex.Message}"); }
        }

        public static void Postfix_OnServerAddPlayer()
        {
            try { NetworkIntegrationService.NotifyServerPlayerAdded(); }
            catch (Exception ex) { MelonLogger.Warning($"[NetworkLifecyclePatches] OnServerAddPlayer dispatch failed: {ex.Message}"); }
        }

        public static void Postfix_OnClientAuthenticated()
        {
            try { NetworkIntegrationService.NotifyClientConnectedToHost(); }
            catch (Exception ex) { MelonLogger.Warning($"[NetworkLifecyclePatches] OnClientAuthenticated dispatch failed: {ex.Message}"); }
        }

        public static void Postfix_OnClientDisconnect()
        {
            try { NetworkIntegrationService.NotifyClientDisconnectedFromHost(); }
            catch (Exception ex) { MelonLogger.Warning($"[NetworkLifecyclePatches] OnClientDisconnect dispatch failed: {ex.Message}"); }
        }
    }
}
