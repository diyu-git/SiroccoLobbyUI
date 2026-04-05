using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using MelonLoader;

namespace SiroccoLobby.Services
{
    /// <summary>
    /// Prevents SteamP2PNetworkTester.StartSteamP2PServer from being called
    /// during an active match. Players pressing F1 mid-match would otherwise
    /// start a new server and crash the game.
    ///
    /// Only works for modded instances — unmodded clients cannot be protected.
    /// </summary>
    public static class ServerStartGuard
    {
        private const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.NonPublic |
                                           BindingFlags.Instance | BindingFlags.Static;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void d_VoidMethod(IntPtr instance, IntPtr methodInfo);

        private static d_VoidMethod? _originalStartServer;
        private static d_VoidMethod? _hookStartServer;


        public static unsafe void Install()
        {
            try
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                if (asm == null) return;

                // Hook StartSteamP2PServer
                var testerType = asm.GetType("Il2CppWartide.Testing.SteamP2PNetworkTester");
                if (testerType == null) return;

                var nativeField = testerType.GetField(
                    "NativeMethodInfoPtr_StartSteamP2PServer_Private_Void_0",
                    BindingFlags.NonPublic | BindingFlags.Static);
                if (nativeField == null) return;

                IntPtr methodInfoPtr = (IntPtr)nativeField.GetValue(null)!;
                if (methodInfoPtr == IntPtr.Zero) return;

                IntPtr methodPtr = *(IntPtr*)methodInfoPtr;
                if (methodPtr == IntPtr.Zero) return;

                _hookStartServer = new d_VoidMethod(Hook_StartServer);
                IntPtr hookPtr = Marshal.GetFunctionPointerForDelegate(_hookStartServer);
                IntPtr originalPtr = methodPtr;
#pragma warning disable CS0618
                MelonUtils.NativeHookAttach((IntPtr)(&originalPtr), hookPtr);
#pragma warning restore CS0618
                _originalStartServer = Marshal.GetDelegateForFunctionPointer<d_VoidMethod>(originalPtr);

                MelonLogger.Msg("[ServerStartGuard] Installed — blocks F1 server start");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ServerStartGuard] Install failed: {ex.Message}");
            }
        }

        private static void Hook_StartServer(IntPtr instance, IntPtr methodInfo)
        {
            // Block completely — the mod handles server start through its own lobby flow
            MelonLogger.Warning("[ServerStartGuard] Blocked F1 server start!");
        }
    }
}
