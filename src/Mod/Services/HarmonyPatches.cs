using System;
using System.Linq;
using System.Reflection;
using MelonLoader;
using SiroccoLobby.Services.Helpers;

namespace SiroccoLobby.Services
{
    public static class HarmonyPatches
    {
        private static bool _dumpedOnce = false;
        private static bool _dumpedLobbyMethods = false;
        private static bool _dumpedNetworkStatus = false;
        
        // Set to true to enable verbose network status logging on every call
        private const bool ENABLE_VERBOSE_NETWORK_STATUS = false;
        
        private const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.NonPublic |
                                           BindingFlags.Instance | BindingFlags.Static;

        // Reusable dumpers
        private static readonly ObjectDumper NetworkDumper = new ObjectDumper(
            memberFilter: ObjectDumper.NetworkRelatedFilter,
            maxDepth: 2);

        public static void Apply(HarmonyLib.Harmony harmony)
        {
            try
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

                if (asm == null)
                {
                    MelonLogger.Error("[ProtoTrace] Assembly-CSharp not found");
                    return;
                }

                // Group patches by type for better organization
                const string GA = "Il2CppWartide.GameAuthority";
                const string SM = "Il2CppWartide.SimulationManager";
                const string WNM = "Il2CppWartide.WartideNetworkManager";
                const string NC = "Mirror.NetworkClient";
                const string PT = "Il2CppWartide.PerformanceTracker";
                const string PD = "Il2CppWartide.PreloadData";

                // GameAuthority patches
                PatchMethod(harmony, asm, GA, "CompleteProtoLobbyServer",
                    nameof(Prefix_GA_Complete), nameof(Postfix_GA_Complete));
                PatchMethod(harmony, asm, GA, "SetGameAuthorityState",
                    nameof(Prefix_GA_SetState));
                PatchMethod(harmony, asm, GA, "InitializeProtoLobbyCaptainSelection",
                    postfixName: nameof(Postfix_GA_InitCaptainSelection));
                PatchMethod(harmony, asm, GA, "OnGUI_Lobby",
                    nameof(Prefix_GA_OnGUI_Lobby));
                PatchMethod(harmony, asm, GA, "LobbyNetworkStatus",
                    postfixName: nameof(Postfix_GA_LobbyNetworkStatus));

                // SimulationManager patches
                PatchMethod(harmony, asm, SM, "CompleteProtoLobbyClient",
                    nameof(Prefix_Sim_CompleteClient), nameof(Postfix_Sim_CompleteClient));
                PatchMethod(harmony, asm, SM, "CompleteProtoLobbyServer",
                    nameof(Prefix_Sim_CompleteServer), nameof(Postfix_Sim_CompleteServer));

                // WartideNetworkManager patches
                PatchMethod(harmony, asm, WNM, "FinishStartHost",
                    nameof(Prefix_WNM_FinishStartHost));
                PatchMethod(harmony, asm, WNM, "StartHostClient",
                    nameof(Prefix_WNM_StartHostClient));

                // Mirror.NetworkClient patches
                PatchMethod(harmony, asm, NC, "Ready",
                    nameof(Prefix_NC_Ready));
                PatchMethod(harmony, asm, NC, "AddPlayer",
                    nameof(Prefix_NC_AddPlayer));

                // Other patches
                PatchMethod(harmony, asm, PT, "Initialize",
                    nameof(Prefix_PerfTracker_Init));
                PatchMethod(harmony, asm, PD, "SetTargetConnectionCount",
                    nameof(Prefix_Preload_SetTarget), paramTypes: new[] { typeof(int) });

                // Special case: Constructor patch for ProtoLobbyCaptainDropdown
                {
                    var gaType = asm.GetType(GA);
                    var dropdownType = gaType?.GetNestedType("ProtoLobbyCaptainDropdown", FLAGS);
                    if (dropdownType != null)
                    {
                        var ctors = dropdownType.GetConstructors(FLAGS);
                        var ctor = ctors.FirstOrDefault(c => c.GetParameters().Length == 0);

                        if (ctor != null)
                        {
                            harmony.Patch(
                                ctor,
                                prefix: new HarmonyLib.HarmonyMethod(typeof(HarmonyPatches).GetMethod(nameof(Prefix_GA_CaptainDropdownCtor), FLAGS))
                            );
                            MelonLogger.Msg("[ProtoTrace] Patched GameAuthority.ProtoLobbyCaptainDropdown::.ctor()");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ProtoTrace] Error applying patches: {ex}");
            }
        }

        private static void PatchMethod(
            HarmonyLib.Harmony harmony,
            Assembly asm,
            string typeName,
            string methodName,
            string? prefixName = null,
            string? postfixName = null,
            Type[]? paramTypes = null)
        {
            var type = asm.GetType(typeName);
            if (type == null)
            {
                MelonLogger.Warning($"[ProtoTrace] Type not found: {typeName}");
                return;
            }

            var method = paramTypes != null
                ? type.GetMethod(methodName, FLAGS, null, paramTypes, null)
                : type.GetMethod(methodName, FLAGS);

            if (method == null)
            {
                MelonLogger.Warning($"[ProtoTrace] Method not found: {typeName}.{methodName}");
                return;
            }

            var prefix = prefixName != null 
                ? new HarmonyLib.HarmonyMethod(typeof(HarmonyPatches).GetMethod(prefixName, FLAGS))
                : null;

            var postfix = postfixName != null
                ? new HarmonyLib.HarmonyMethod(typeof(HarmonyPatches).GetMethod(postfixName, FLAGS))
                : null;

            harmony.Patch(method, prefix: prefix, postfix: postfix);
            MelonLogger.Msg($"[ProtoTrace] Patched {typeName}.{methodName}()");
        }

        // ============================================================
        // Prefix/Postfix Methods
        // ============================================================

        public static void Prefix_GA_OnGUI_Lobby(object __instance) => DumpLobbyMethods(__instance);

        public static void Prefix_GA_Complete() =>
            MelonLogger.Msg("[Trace] >>> Enter GameAuthority.CompleteProtoLobbyServer()");

        public static void Postfix_GA_Complete() =>
            MelonLogger.Msg("[Trace] <<< Exit GameAuthority.CompleteProtoLobbyServer()");

        public static void Prefix_GA_CaptainDropdownCtor(object __instance) =>
            MelonLogger.Msg("[ProtoTrace] ProtoLobbyCaptainDropdown ctor called: " + __instance);

        public static void Prefix_Preload_SetTarget(int __0) =>
            MelonLogger.Msg($"[Trace] PreloadData.SetTargetConnectionCount({__0})");

        public static void Prefix_Sim_CompleteServer() =>
            MelonLogger.Msg("[Trace] SimulationManager.CompleteProtoLobbyServer()");

        public static void Postfix_Sim_CompleteServer() =>
            MelonLogger.Msg("[Trace] SimulationManager.CompleteProtoLobbyServer() finished");

        public static void Prefix_Sim_CompleteClient() =>
            MelonLogger.Msg("[Trace] >>> Enter SimulationManager.CompleteProtoLobbyClient()");

        public static void Postfix_Sim_CompleteClient() =>
            MelonLogger.Msg("[Trace] <<< Exit SimulationManager.CompleteProtoLobbyClient()");

        public static void Prefix_WNM_FinishStartHost() =>
            MelonLogger.Msg("[Trace] WartideNetworkManager.FinishStartHost()");

        public static void Prefix_WNM_StartHostClient() =>
            MelonLogger.Msg("[Trace] WartideNetworkManager.StartHostClient()");

        public static void Prefix_NC_Ready() =>
            MelonLogger.Msg("[Trace] NetworkClient.Ready()");

        public static void Prefix_NC_AddPlayer(object[] __args)
        {
            MelonLogger.Msg("[Trace] NetworkClient.AddPlayer()");
            if (__args != null)
            {
                MelonLogger.Msg("[Trace] >>> NetworkClient.AddPlayer args:");
                for (int i = 0; i < __args.Length; i++)
                {
                    var arg = __args[i];
                    string typeName = arg?.GetType().FullName ?? "null";
                    MelonLogger.Msg($"  Arg[{i}] = {arg} (Type: {typeName})");
                }
            }
        }

        public static void Postfix_GA_InitCaptainSelection(object __instance)
        {
            if (_dumpedOnce) return;
            _dumpedOnce = true;
            MelonLogger.Msg("[ProtoTrace] >>> InitCaptainSelection finished");

            try
            {
                var type = __instance.GetType();
                var listProp = type.GetProperty("_protoLobbyDropdownAvailableCaptains", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var list = listProp?.GetValue(__instance);
                if (list == null){ MelonLogger.Warning("[ProtoTrace] Captain list is null"); return; }

                var listType = list.GetType();
                var countProp = listType.GetProperty("Count");
                var itemProp = listType.GetProperty("Item");

                int count = (int)(countProp?.GetValue(list) ?? 0);
                MelonLogger.Msg($"[ProtoTrace] Captain list count: {count}");

                for (int i = 0; i < count; i++)
                {
                    var entry = itemProp?.GetValue(list, new object[] { i });
                    var entryType = entry?.GetType();

                    var labelProp = entryType?.GetProperty("labelCaptainName");
                    var typeIdProp = entryType?.GetProperty("captainTypeID");

                    var label = labelProp?.GetValue(entry);
                    var typeId = typeIdProp?.GetValue(entry);

                    MelonLogger.Msg($"[ProtoTrace] Captain[{i}] label='{label}' typeID='{typeId}'");
                }
            }
            catch (Exception ex){ MelonLogger.Error("[ProtoTrace] Error dumping captain list: " + ex); }
        }

        private static void DumpLobbyMethods(object instance)
        {
            if (_dumpedLobbyMethods) return;
            _dumpedLobbyMethods = true;

            var type = instance.GetType();
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            MelonLogger.Msg("[ProtoTrace] Dumping GameAuthority methods:");

            foreach (var m in methods)
            {
                if (m.Name.Contains("Ready", StringComparison.OrdinalIgnoreCase) ||
                    m.Name.Contains("Lobby", StringComparison.OrdinalIgnoreCase) ||
                    m.Name.Contains("Start", StringComparison.OrdinalIgnoreCase) ||
                    m.Name.Contains("Proto", StringComparison.OrdinalIgnoreCase))
                {
                    MelonLogger.Msg("  " + m.Name);
                }
            }
        }

        public static void Postfix_GA_LobbyNetworkStatus(object __instance)
        {
            if (__instance == null)
            {
                MelonLogger.Msg("[ProtoTrace] LobbyNetworkStatus __instance is NULL");
                return;
            }

            // Print once by default, or every time if verbose mode is enabled
            if (!ENABLE_VERBOSE_NETWORK_STATUS && _dumpedNetworkStatus)
                return;

            _dumpedNetworkStatus = true;
            NetworkDumper.Dump(__instance, "LobbyNetworkStatus Info", "  ");
        }

        public static void Prefix_PerfTracker_Init(object __instance)
        {
            MelonLogger.Msg("[ProtoTrace] >>> PerformanceTracker.Initialize() called!");
        }

        public static void Prefix_GA_SetState(object __instance, object newState)
        {
            MelonLogger.Msg($"[ProtoTrace] >>> GameAuthority.SetGameAuthorityState({newState})");
        }
    }
}
