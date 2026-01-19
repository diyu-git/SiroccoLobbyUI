using System;
using System.Linq;
using MelonLoader;
using UnityEngine;
using Il2CppInterop.Runtime;

namespace SiroccoLobby.Helpers
{
    public static class NativeLobbyHelpers
    {
        public static void CallExitLobbyEnterMatch()
        {
            try
            {
                var assemblyCSharp = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                if (assemblyCSharp == null) { MelonLogger.Error("[ProtoLobby] Assembly-CSharp not found!"); return; }

                // Primary lookup
                var systemType = assemblyCSharp.GetType("Il2CppWartide.LobbyAuthority");

                // Fallback scan
                if (systemType == null)
                {
                    MelonLogger.Warning("[ProtoLobby] LobbyAuthority not found under Il2CppWartide. Scanning...");

                    var candidates = assemblyCSharp.GetTypes()
                        .Where(t => t.FullName != null &&
                                    t.FullName.Contains("LobbyAuthority", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (candidates.Count == 0) { MelonLogger.Error("[ProtoLobby] No types containing 'LobbyAuthority' found."); return; }

                    if (candidates.Count > 1)
                    {
                        MelonLogger.Warning("[ProtoLobby] Multiple LobbyAuthority candidates found:");
                        foreach (var c in candidates) MelonLogger.Warning($" -> {c.FullName}");
                        MelonLogger.Warning("[ProtoLobby] Using the first match automatically.");
                    }

                    systemType = candidates.First();
                    MelonLogger.Msg($"[ProtoLobby] Using fallback type: {systemType.FullName}");
                }

                // Convert System.Type → Il2CppSystem.Type
                var il2cppType = Il2CppType.From(systemType);

                // Find instance
                var instance = UnityEngine.Object.FindObjectOfType(il2cppType);
                if (instance == null) { MelonLogger.Error("[ProtoLobby] LobbyAuthority instance not found!"); return; }

                // Get method
                var exitMethod = systemType.GetMethod("ExitLobbyEnterMatch");
                if (exitMethod == null) { MelonLogger.Error("[ProtoLobby] ExitLobbyEnterMatch method not found!"); return; }

                MelonLogger.Msg("[ProtoLobby] Calling ExitLobbyEnterMatch()");
                exitMethod.Invoke(instance, null);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ProtoLobby] Error calling ExitLobbyEnterMatch: {ex}");
            }
        }
    }
}
