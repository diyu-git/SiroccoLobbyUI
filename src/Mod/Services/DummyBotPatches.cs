using System;
using System.Linq;
using System.Reflection;
using MelonLoader;

namespace SiroccoLobby.Services
{
    /// <summary>
    /// Harmony patches to disable dummy bot AI behavior.
    /// Patches DummyPlayerDriver.UpdateExternal and FixedUpdateExternal
    /// to no-op, causing bots to stay idle at spawn.
    /// </summary>
    public static class DummyBotPatches
    {
        private static bool _enabled = false;

        private const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.NonPublic |
                                           BindingFlags.Instance | BindingFlags.Static;

        /// <summary>
        /// Enable or disable the bot freeze. When disabled, the prefix
        /// returns true (original method runs). When enabled, returns false (skipped).
        /// </summary>
        public static bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled != value)
                {
                    _enabled = value;
                    MelonLogger.Msg($"[DummyBotPatches] Bot AI {(value ? "DISABLED" : "ENABLED")}");
                }
            }
        }

        public static void Apply(HarmonyLib.Harmony harmony, Assembly? assemblyCSharp = null)
        {
            try
            {
                var asm = assemblyCSharp ?? AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

                if (asm == null)
                {
                    MelonLogger.Warning("[DummyBotPatches] Assembly-CSharp not found");
                    return;
                }

                var driverType = asm.GetType("Il2CppWartide.DummyPlayerDriver");
                if (driverType == null)
                {
                    MelonLogger.Warning("[DummyBotPatches] DummyPlayerDriver type not found");
                    return;
                }

                int patched = 0;
                var prefixMethod = typeof(DummyBotPatches).GetMethod(nameof(Prefix_SkipIfEnabled), FLAGS);

                // Patch UpdateExternal
                var updateMethod = driverType.GetMethod("UpdateExternal", FLAGS);
                if (updateMethod != null)
                {
                    harmony.Patch(updateMethod, prefix: new HarmonyLib.HarmonyMethod(prefixMethod));
                    patched++;
                }

                // Patch FixedUpdateExternal
                var fixedUpdateMethod = driverType.GetMethod("FixedUpdateExternal", FLAGS);
                if (fixedUpdateMethod != null)
                {
                    harmony.Patch(fixedUpdateMethod, prefix: new HarmonyLib.HarmonyMethod(prefixMethod));
                    patched++;
                }

                MelonLogger.Msg($"[DummyBotPatches] Patched {patched} methods on DummyPlayerDriver");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[DummyBotPatches] Error applying patches: {ex.Message}");
            }
        }

        /// <summary>
        /// Prefix that skips the original method when bot AI is disabled.
        /// Returns false to skip, true to run original.
        /// </summary>
        public static bool Prefix_SkipIfEnabled()
        {
            return !_enabled; // false = skip original, true = run original
        }
    }
}
