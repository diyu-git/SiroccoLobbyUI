using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SiroccoLobby.Services.Core;
using SiroccoLobby.Services.Helpers;

namespace SiroccoLobby.Services
{
    /// <summary>
    /// Handles network operations: connect, ready, validation, P2P.
    /// Refactored for readability, caching, and events.
    /// </summary>
    public class NetworkIntegrationService
    {
        private readonly GameReflectionBridge _reflection;

        private readonly NetworkManagerResolver _networkManagerResolver;
        private readonly ClientAuthenticatorHelper _clientAuthenticator;
    	private readonly AuthDebugTracker _authTrace = new AuthDebugTracker();

        // Cached frequently used properties for speed
        private PropertyInfo? _networkClientActiveProp;
        private PropertyInfo? _networkClientLocalPlayerProp;
		private PropertyInfo? _networkServerActiveProp;

        private bool _hasWarnedMissingPlayerStatusRefs;
   		private bool _hasDumpedTesterGraph;

        private static readonly ObjectDumper ProtoLobbyDumper = new ObjectDumper(
            memberFilter: ObjectDumper.ProtoLobbyRelatedFilter,
            maxDepth: 3,
            prefix: "[ProtoDump]",
            maxEnumerableItems: 8);

        // Events
        public event Action? OnConnected;
        public event Action? OnPlayerReady;
        public event Action<bool>? OnValidationComplete;

        public NetworkIntegrationService(GameReflectionBridge reflection)
        {
            _reflection = reflection;

            // Used to find the actual client NetworkManager instance (important when host exists).
            // This also gives us a single place to invoke the authenticator safely.
            var managerType = _reflection.NetworkManagerInstance?.GetType();
            _networkManagerResolver = new NetworkManagerResolver();
            _clientAuthenticator = managerType != null
                ? new ClientAuthenticatorHelper(managerType, _networkManagerResolver)
                : new ClientAuthenticatorHelper(typeof(object), _networkManagerResolver);

            CacheNetworkClientProperties();
            CacheNetworkServerProperties();
        }

        #region Public Methods

        public bool IsClientConnected => _networkClientActiveProp != null && (bool)(_networkClientActiveProp.GetValue(null) ?? false);

        public bool IsServerActive => _networkServerActiveProp != null && (bool)(_networkServerActiveProp.GetValue(null) ?? false);

        public void ConnectToGameServer(string? address = null)
        {
            if (_reflection.NetworkClientType == null)
            {
                MelonLoader.MelonLogger.Error("[NetworkIntegrationService] NetworkClient type not found!");
                return;
            }

            try
            {
                _authTrace.Reset("ConnectToGameServer");
                _authTrace.LogConnectRequested(address ?? "<local>");

                if (string.IsNullOrEmpty(address))
                {
                    ConnectToLocalServer();
                }
                else
                {
                    ConnectToRemoteServer(address);
                }

                // Snapshot state right after we initiated the connect + auth trigger.
                _authTrace.TrackStates(IsClientConnected, SafeGetIsAuthenticated(), "post-connect");

                OnConnected?.Invoke();
            }
            catch (Exception ex)
            {
                LogReflectionException("ConnectToGameServer", ex);
            }
        }

        public void CallNetworkClientReady(int captainIndex, int teamIndex)
        {
            if (_reflection.NetworkClientReadyMethod == null || _reflection.NetworkClientAddPlayerMethod == null)
            {
                MelonLoader.MelonLogger.Error("[NetworkIntegrationService] NetworkClient methods not found!");
                return;
            }

            // Mirror will disconnect clients that send Ready/AddPlayer before authenticating.
            // See: "Received message Mirror.ReadyMessage that required authentication, but the user has not authenticated yet".
            if (!IsClientAuthenticated())
            {
                _authTrace.TrackStates(IsClientConnected, SafeGetIsAuthenticated(), "ready-request");
                _authTrace.LogBlockedReady("client not authenticated yet", IsClientConnected, SafeGetIsAuthenticated());
                return;
            }

            try
            {
                InvokeSafe(_reflection.NetworkClientReadyMethod, null);

                var localPlayer = _networkClientLocalPlayerProp?.GetValue(null);
                if (localPlayer != null)
                {
                    MelonLoader.MelonLogger.Msg("[NetworkIntegrationService] Local player already exists, skipping AddPlayer.");
                    return;
                }

                AddLocalPlayer(captainIndex, teamIndex);

                OnPlayerReady?.Invoke();
            }
            catch (Exception ex)
            {
                LogReflectionException("CallNetworkClientReady", ex);
            }
        }

        public bool ValidatePlayersReadyForGameStart()
        {
            if (_reflection.TesterInstance == null || _reflection.ValidatePlayersReadyMethod == null)
            {
                MelonLoader.MelonLogger.Error("[NetworkIntegrationService] Tester or validation method not found.");
                return false;
            }

            bool result = false;
            try
            {
                result = InvokeSafe(_reflection.ValidatePlayersReadyMethod, _reflection.TesterInstance) as bool? ?? false;
                MelonLoader.MelonLogger.Msg(result
                    ? "[NetworkIntegrationService] Players ready validation succeeded."
                    : "[NetworkIntegrationService] Players ready validation failed.");

                OnValidationComplete?.Invoke(result);
            }
            catch (Exception ex)
            {
                LogReflectionException("ValidatePlayersReadyForGameStart", ex);
            }

            return result;
        }

        public void UpdateLocalPlayerStatus(bool? isReady = null, bool? isTeamA = null)
        {
            if (_reflection.TesterInstance == null || _reflection.CachedInfoField == null || _reflection.PlayersListProp == null)
            {
                if (!_hasWarnedMissingPlayerStatusRefs)
                {
                    _hasWarnedMissingPlayerStatusRefs = true;
                    MelonLoader.MelonLogger.Warning(
                        "[NetworkIntegrationService] Cannot update player status yet (tester reflection not ready). " +
                        $"TesterInstance={( _reflection.TesterInstance != null)}, CachedInfoField={( _reflection.CachedInfoField != null)}, PlayersListProp={( _reflection.PlayersListProp != null)}");

                    // One-time: try to grab whatever the game has instantiated and dump it.
                    // This helps us understand the real runtime object graph even if we can't see IL2CPP method bodies.
                    TryDumpProtoLobbyGraphOnce("UpdateLocalPlayerStatus:missing-tester");
                }
                return;
            }

            try
            {
                var infoObj = _reflection.CachedInfoField.GetValue(_reflection.TesterInstance);
                var playersList = _reflection.PlayersListProp.GetValue(infoObj) as System.Collections.IEnumerable;
                if (playersList == null) return;

                foreach (var playerObj in playersList)
                {
                    if (isReady.HasValue && _reflection.PsIsReadyProp != null)
                        _reflection.PsIsReadyProp.SetValue(playerObj, isReady.Value);

                    if (isTeamA.HasValue && _reflection.PsIsTeamAProp != null)
                        _reflection.PsIsTeamAProp.SetValue(playerObj, isTeamA.Value);

                    break; // Only update the local player
                }

                MelonLoader.MelonLogger.Msg($"[NetworkIntegrationService] Updated Player Status: Ready={isReady}, TeamA={isTeamA}");
            }
            catch (Exception ex)
            {
                LogReflectionException("UpdateLocalPlayerStatus", ex);
            }
        }

        public void TryDumpProtoLobbyGraphOnce(string reason)
        {
            if (_hasDumpedTesterGraph)
                return;

            _hasDumpedTesterGraph = true;

            try
            {
                // Re-bind tester instance if it wasn't available when the reflection bridge was constructed.
                if (_reflection.TesterInstance == null && _reflection.TesterType != null)
                {
                    _reflection.TesterInstance = TryFindUnityObjectInstance(_reflection.TesterType);
                }

                if (_reflection.TesterInstance == null)
                {
                    MelonLoader.MelonLogger.Msg($"[NetworkIntegrationService] Proto-lobby dump skipped ({reason}): SteamP2PNetworkTester instance not found.");
                    return;
                }

                // Optional: if the tester has a proto-lobby integration helper, call it once.
                // If this explodes, we catch and continue dump anyway.
                if (_reflection.IntegrateWithProtoLobbyMethod != null)
                {
                    try
                    {
                        InvokeSafe(_reflection.IntegrateWithProtoLobbyMethod, _reflection.TesterInstance);
                        MelonLoader.MelonLogger.Msg("[NetworkIntegrationService] Invoked SteamP2PNetworkTester.IntegrateWithProtoLobby() for dump.");
                    }
                    catch
                    {
                        // ignore
                    }
                }

                if (_reflection.DisplayProtoLobbyPlayerStatusMethod != null)
                {
                    try
                    {
                        InvokeSafe(_reflection.DisplayProtoLobbyPlayerStatusMethod, _reflection.TesterInstance);
                        MelonLoader.MelonLogger.Msg("[NetworkIntegrationService] Invoked SteamP2PNetworkTester.DisplayProtoLobbyPlayerStatus() for dump.");
                    }
                    catch
                    {
                        // ignore
                    }
                }

                ProtoLobbyDumper.Dump(_reflection.TesterInstance, label: $"SteamP2PNetworkTester graph ({reason})");

                // Also dump cachedConnectionInfo directly if possible (often the real gold).
                if (_reflection.CachedInfoField != null)
                {
                    var info = _reflection.CachedInfoField.GetValue(_reflection.TesterInstance);
                    if (info != null)
                        ProtoLobbyDumper.Dump(info, label: $"SteamP2PNetworkTester.cachedConnectionInfo ({reason})");
                }
            }
            catch (Exception ex)
            {
                LogReflectionException("TryDumpProtoLobbyGraphOnce", ex);
            }
        }

        private static object? TryFindUnityObjectInstance(Type unityObjectType)
        {
            try
            {
                // IMPORTANT: UnityEngine.Object lives in UnityEngine.* modules, not Assembly-CSharp.
                // So we must locate UnityEngine.Object from loaded assemblies.
                var unityObject = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Select(a => a.GetType("UnityEngine.Object", throwOnError: false))
                    .FirstOrDefault(t => t != null);

                if (unityObject == null)
                    return null;

                // Prefer FindObjectOfType(Type)
                var find = unityObject.GetMethod(
                    "FindObjectOfType",
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    types: new[] { typeof(Type) },
                    modifiers: null);

                if (find != null)
                {
                    var obj = find.Invoke(null, new object[] { unityObjectType });
                    if (obj != null)
                        return obj;
                }

                // Fallback: Resources.FindObjectsOfTypeAll(Type) (finds inactive/hidden objects too)
                var resources = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Select(a => a.GetType("UnityEngine.Resources", throwOnError: false))
                    .FirstOrDefault(t => t != null);

                var findAll = resources?.GetMethod(
                    "FindObjectsOfTypeAll",
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    types: new[] { typeof(Type) },
                    modifiers: null);

                var all = findAll?.Invoke(null, new object[] { unityObjectType });
                if (all is Array arr && arr.Length > 0)
                    return arr.GetValue(0);

                return null;
            }
            catch
            {
                return null;
            }
        }

        public void TriggerSinglePlayer()
        {
            if (_reflection.NetworkManagerInstance == null)
            {
                MelonLoader.MelonLogger.Error("[NetworkIntegrationService] Cannot start single player - network manager instance missing.");
                return;
            }

            // IMPORTANT: The game uses GameAuthority._isSinglePlayer to decide which path to take
            // during match transition (offline vs backend/presence driven flow).
            // Old implementation explicitly set this before calling StartSinglePlayer.
            if (_reflection.GameAuthorityInstance == null || _reflection.IsSinglePlayerProp == null)
            {
                MelonLoader.MelonLogger.Error(
                    "[NetworkIntegrationService] Cannot start single player - GameAuthority or _isSinglePlayer property not resolved.");
                // Hosting will likely be broken without this state, but we still attempt to start.
            }
            else
            {
                try
                {
                    _reflection.IsSinglePlayerProp.SetValue(_reflection.GameAuthorityInstance, true);
                }
                catch (Exception ex)
                {
                    LogReflectionException("TriggerSinglePlayer:SetIsSinglePlayer", ex);
                }
            }

            // IMPORTANT: This should be the game's native offline singleplayer path.
            // Do not fall back to the Steam P2P test path here.
            if (_reflection.StartSinglePlayerMethod == null)
            {
                MelonLoader.MelonLogger.Error("[NetworkIntegrationService] StartSinglePlayer method not found.");
                return;
            }

            InvokeSafe(_reflection.StartSinglePlayerMethod, _reflection.NetworkManagerInstance);
            MelonLoader.MelonLogger.Msg("[NetworkIntegrationService] Single-player session started.");
        }

        public void TriggerSinglePlayerP2P()
        {
            if (_reflection.NetworkManagerInstance == null)
            {
                MelonLoader.MelonLogger.Error("[NetworkIntegrationService] Cannot start P2P single player - network manager instance missing.");
                return;
            }

            if (_reflection.StartSinglePlayerP2PMethod == null)
            {
                MelonLoader.MelonLogger.Error("[NetworkIntegrationService] StartSinglePlayerWithSteamP2P method not found.");
                return;
            }

            InvokeSafe(_reflection.StartSinglePlayerP2PMethod, _reflection.NetworkManagerInstance);
            MelonLoader.MelonLogger.Msg("[NetworkIntegrationService] Single-player P2P session started.");
        }

        public void ShutdownNetwork(bool asHost)
        {
            if (_reflection.NetworkManagerInstance == null)
            {
                MelonLoader.MelonLogger.Warning("[NetworkIntegrationService] Cannot shutdown network - manager instance missing.");
                return;
            }

            try
            {
                if (asHost)
                {
                    if (_reflection.StopHostMethod != null)
                    {
                        InvokeSafe(_reflection.StopHostMethod, _reflection.NetworkManagerInstance);
                        return; // StopHost cascades to StopServer/StopClient in Mirror
                    }

                    if (_reflection.StopServerMethod != null)
                    {
                        InvokeSafe(_reflection.StopServerMethod, _reflection.NetworkManagerInstance);
                    }
                }

                if (_reflection.StopClientMethod != null)
                {
                    InvokeSafe(_reflection.StopClientMethod, _reflection.NetworkManagerInstance);
                }
                else
                {
                    InvokeSafe(_reflection.DisconnectMethod, null);
                }
            }
            catch (Exception ex)
            {
                LogReflectionException("ShutdownNetwork", ex);
            }
        }

        public void DisconnectClient() => ShutdownNetwork(false);

        // P2P placeholders
        public void InitializeP2PConnections() => throw new NotImplementedException("[NetworkIntegrationService] P2P init not implemented.");
        public void ReceiveP2PData() => throw new NotImplementedException("[NetworkIntegrationService] P2P receive not implemented.");

        #endregion

        #region Private Helpers

        private void CacheNetworkClientProperties()
        {
            var clientType = _reflection.NetworkClientType;
            if (clientType == null) return;

            _networkClientActiveProp = clientType.GetProperty("active", BindingFlags.Public | BindingFlags.Static)
                ?? clientType.GetProperty("isConnected", BindingFlags.Public | BindingFlags.Static);

            _networkClientLocalPlayerProp = clientType.GetProperty("localPlayer", BindingFlags.Public | BindingFlags.Static);
        }

        private void CacheNetworkServerProperties()
        {
            var serverType = _reflection.NetworkServerType;
            if (serverType == null) return;

            _networkServerActiveProp = serverType.GetProperty("active", BindingFlags.Public | BindingFlags.Static)
                ?? serverType.GetProperty("isActive", BindingFlags.Public | BindingFlags.Static);
        }

        private void ConnectToLocalServer()
        {
            if (_networkClientActiveProp != null && (bool)(_networkClientActiveProp.GetValue(null) ?? false))
            {
                MelonLoader.MelonLogger.Msg("[NetworkIntegrationService] Local client already connected. Disconnecting...");
                InvokeSafe(_reflection.DisconnectMethod, null);
            }

            InvokeSafe(_reflection.ConnectLocalServerMethod, null);

            _authTrace.TrackStates(IsClientConnected, SafeGetIsAuthenticated(), "after-ConnectLocalServer");

            TryTriggerClientAuthentication();

            _authTrace.TrackStates(IsClientConnected, SafeGetIsAuthenticated(), "after-auth-trigger");

            MelonLoader.MelonLogger.Msg("[NetworkIntegrationService] Connected to local server.");
        }

        private void ConnectToRemoteServer(string address)
        {
            var method = _reflection.ConnectMethod;
            if (method == null || method.GetParameters().Length < 1)
            {
                MelonLoader.MelonLogger.Warning("[NetworkIntegrationService] Connect method not found or invalid signature.");
                return;
            }

            InvokeSafe(method, null, address);

            _authTrace.TrackStates(IsClientConnected, SafeGetIsAuthenticated(), "after-Connect(address)");

            TryTriggerClientAuthentication();

            _authTrace.TrackStates(IsClientConnected, SafeGetIsAuthenticated(), "after-auth-trigger");

            MelonLoader.MelonLogger.Msg($"[NetworkIntegrationService] Connected to remote server at {address}.");
        }

        private bool IsClientAuthenticated()
        {
            try
            {
                if (_reflection.NetworkClientIsAuthenticatedProp == null)
                    return true; // If we can't detect it, don't hard-block.

                return (bool)(_reflection.NetworkClientIsAuthenticatedProp.GetValue(null) ?? false);
            }
            catch
            {
                return true;
            }
        }

        private bool? SafeGetIsAuthenticated()
        {
            try
            {
                if (_reflection.NetworkClientIsAuthenticatedProp == null)
                    return null;

                return (bool)(_reflection.NetworkClientIsAuthenticatedProp.GetValue(null) ?? false);
            }
            catch
            {
                return null;
            }
        }

        private void TryTriggerClientAuthentication()
        {
            // Prefer the helper that resolves the *client-capable* NetworkManager instance.
            // This avoids invoking auth on a server/host manager in mixed-role situations.
            try
            {
                if (_networkManagerResolver.EnsureResolved(_reflection.NetworkManagerInstance?.GetType() ?? typeof(object)))
                {
                    if (_clientAuthenticator.Authenticate())
                    {
                        _authTrace.LogAuthenticatorTriggered(true, "resolver/helper");
                        MelonLoader.MelonLogger.Msg("[NetworkIntegrationService] Triggered client authenticator (OnClientAuthenticate)."
                        );
                        return;
                    }
                    _authTrace.LogAuthenticatorTriggered(false, "resolver/helper");
                }
            }
            catch (Exception ex)
            {
                LogReflectionException("TryTriggerClientAuthentication", ex);
            }

            // Fallback to old cached field/method if present.
            if (_reflection.AuthenticatorInstance != null && _reflection.OnClientAuthenticateMethod != null)
            {
                _authTrace.LogAuthenticatorTriggered(true, "fallback/reflection");
                MelonLoader.MelonLogger.Msg("[NetworkIntegrationService] Triggering Mirror authenticator (fallback): OnClientAuthenticate()" );
                InvokeSafe(_reflection.OnClientAuthenticateMethod, _reflection.AuthenticatorInstance);
            }
        }

        private void AddLocalPlayer(int captainIndex, int teamIndex)
        {
            var method = _reflection.NetworkClientAddPlayerMethod;
            if (method == null) return;

            int paramCount = method.GetParameters().Length;

            if (paramCount == 5)
            {
                var isTeamA = teamIndex == 1;
                var steamId = Steamworks.SteamUser.GetSteamID();
                var playerName = Steamworks.SteamFriends.GetPersonaName() ?? "Unknown";
                StringToUintFNV1a.Compute(playerName, out uint playerId);
                int gameCaptainIndex = captainIndex + 1;

                object?[] parameters = { isTeamA, gameCaptainIndex, playerId, playerName, null };
                InvokeSafe(method, null, parameters);
            }
            else
            {
                InvokeSafe(method, null);
            }

            MelonLoader.MelonLogger.Msg("[NetworkIntegrationService] Local player added.");
        }

        private object? InvokeSafe(MethodInfo? method, object? instance, params object?[] parameters)
        {
            if (method == null) return null;

            try
            {
                return method.Invoke(instance, parameters);
            }
            catch (Exception ex)
            {
                LogReflectionException(method.Name, ex);
                return null;
            }
        }

        private void LogReflectionException(string context, Exception ex)
        {
            var realEx = ex.InnerException ?? ex;
            MelonLoader.MelonLogger.Error($"[NetworkIntegrationService:{context}] {realEx.Message}");
            MelonLoader.MelonLogger.Error(realEx.StackTrace);
        }

        #endregion
    }
}
