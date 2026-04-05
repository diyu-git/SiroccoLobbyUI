using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using MelonLoader;
using SiroccoLobby.Services.Helpers;

namespace SiroccoLobby.Services
{
    /// <summary>
    /// Harmony patches + native IL2CPP hook to fix in-game text chat for P2P mode.
    ///
    /// Harmony patches (intercept managed C# wrappers):
    ///   - CommandSendChatMessage: intercept local player sending chat
    ///   - AddChatMessage: fix "Splayer" → real name on display
    ///
    /// Native hook (intercept IL2CPP native method directly):
    ///   - InvokeUserCode_CommandSendChatMessage: the actual Mirror RPC dispatch
    ///     entry point. Intercepts Commands arriving from remote clients on the host.
    /// </summary>
    public static class ChatPatches
    {
        private const BindingFlags FLAGS = BindingFlags.Public | BindingFlags.NonPublic |
                                           BindingFlags.Instance | BindingFlags.Static;

        // Cached reflection handles
        private static Type? _gameAuthorityType;
        private static PropertyInfo? _gaInstanceProp;
        private static MethodInfo? _getMappingsMethod;
        private static MethodInfo? _rpcBroadcastMethod;
        private static PropertyInfo? _mappingControllerProp;
        private static PropertyInfo? _mappingDisplayNameProp;
        private static PropertyInfo? _mappingPlayerIdProp;

        // HUD_Messages
        private static MethodInfo? _addChatMessageMethod;
        private static PropertyInfo? _hudInstanceProp;
        private static FieldInfo? _hudInstanceField;

        // Mirror NetworkReader.ReadString — resolved at runtime
        private static MethodInfo? _readerReadStringMethod;
        private static Type? _networkReaderType;
        private static ConstructorInfo? _networkReaderPtrCtor;

        // Reentrancy guard for AddChatMessage
        private static bool _insideOurAddChat = false;

        // Cache the local player's Steam name and ID
        private static string? _cachedSteamName;
        private static uint _cachedSteamId;

        // ================================================================
        // Native hook for InvokeUserCode_CommandSendChatMessage__String
        // Static IL2CPP method: (IntPtr obj, IntPtr reader, IntPtr senderConn, IntPtr methodInfo)
        // ================================================================
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void d_InvokeUserCode_CommandSendChat(IntPtr obj, IntPtr reader, IntPtr senderConn, IntPtr methodInfo);
        private static d_InvokeUserCode_CommandSendChat? _originalInvokeUserCode;
        private static d_InvokeUserCode_CommandSendChat? _hookDelegate;

        public static void Apply(HarmonyLib.Harmony harmony)
        {
            try
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

                if (asm == null)
                {
                    MelonLogger.Warning("[ChatPatches] Assembly-CSharp not found");
                    return;
                }

                var pcType = asm.GetType("Il2CppWartide.PlayerController");
                var hudType = asm.GetType("Il2CppWartide.HUD_Messages");
                _gameAuthorityType = asm.GetType("Il2CppWartide.GameAuthority");

                if (pcType == null) { MelonLogger.Warning("[ChatPatches] PlayerController not found"); return; }
                if (hudType == null) { MelonLogger.Warning("[ChatPatches] HUD_Messages not found"); return; }

                // Cache GameAuthority reflection
                if (_gameAuthorityType != null)
                {
                    _gaInstanceProp = _gameAuthorityType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    _getMappingsMethod = _gameAuthorityType.GetMethod("GetPlayerConnectionMappings", BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache PlayerController reflection
                _rpcBroadcastMethod = pcType.GetMethod("RpcBroadcastChatMessage", FLAGS);

                // Cache PlayerConnectionMapping reflection
                var mappingType = asm.GetType("Il2CppWartide.PlayerConnectionMapping");
                if (mappingType != null)
                {
                    _mappingControllerProp = mappingType.GetProperty("Controller", BindingFlags.Public | BindingFlags.Instance);
                    _mappingDisplayNameProp = mappingType.GetProperty("DisplayName", BindingFlags.Public | BindingFlags.Instance);
                    _mappingPlayerIdProp = mappingType.GetProperty("PlayerId", BindingFlags.Public | BindingFlags.Instance);
                }

                // Cache HUD_Messages reflection
                _addChatMessageMethod = hudType.GetMethod("AddChatMessage", FLAGS);
                _hudInstanceProp = hudType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                _hudInstanceField = hudType.GetField("Instance", BindingFlags.Public | BindingFlags.Static);

                // Find Mirror's NetworkReader.ReadString method at runtime
                ResolveReadStringMethod();

                // --- Harmony patches ---
                TryPatch(harmony, pcType, "CommandSendChatMessage",
                    nameof(Prefix_CommandSendChat), null, "PC.CommandSendChat");

                TryPatch(harmony, hudType, "AddChatMessage",
                    nameof(Prefix_AddChatMessage), null, "HUD.AddChatMessage");

                // --- Native IL2CPP hook on InvokeUserCode_ ---
                InstallNativeHook(pcType);

                MelonLogger.Msg("[ChatPatches] Chat patches applied");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ChatPatches] Error applying patches: {ex.Message}");
            }
        }

        private static void ResolveReadStringMethod()
        {
            try
            {
                // Find NetworkReader type in any loaded assembly
                var allTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } });

                var readerType = allTypes.FirstOrDefault(t =>
                    t.FullName == "Mirror.NetworkReader" || t.FullName == "Il2CppMirror.NetworkReader");

                if (readerType != null)
                {
                    _networkReaderType = readerType;
                    // Cache the IntPtr constructor for wrapping native pointers
                    _networkReaderPtrCtor = readerType.GetConstructor(new[] { typeof(IntPtr) });
                    // Try instance method first
                    _readerReadStringMethod = readerType.GetMethod("ReadString",
                        BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);

                    if (_readerReadStringMethod != null)
                        return;
                }

                // Try extension method: NetworkReaderExtensions.ReadString(NetworkReader)
                var extTypes = allTypes.Where(t =>
                    t.Name.Contains("NetworkReader") && t.Name.Contains("Extension"));
                foreach (var extType in extTypes)
                {
                    var method = extType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(m => m.Name == "ReadString" && m.GetParameters().Length == 1);
                    if (method != null)
                    {
                        _readerReadStringMethod = method;
                        return;
                    }
                }

                MelonLogger.Warning("[ChatPatches] NetworkReader.ReadString not found — native hook will use fallback");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ChatPatches] Error resolving ReadString: {ex.Message}");
            }
        }

        private static unsafe void InstallNativeHook(Type pcType)
        {
            try
            {
                // Target: InvokeUserCode_CommandSendChatMessage__String
                // This is the static method that Mirror's delegate calls for Command dispatch
                var nativeField = pcType.GetField(
                    "NativeMethodInfoPtr_InvokeUserCode_CommandSendChatMessage__String_Protected_Static_Void_NetworkBehaviour_NetworkReader_NetworkConnectionToClient_0",
                    BindingFlags.NonPublic | BindingFlags.Static);

                if (nativeField == null)
                {
                    MelonLogger.Warning("[ChatPatches] NativeMethodInfoPtr for InvokeUserCode_CommandSendChat not found");
                    return;
                }

                IntPtr methodInfoPtr = (IntPtr)nativeField.GetValue(null)!;
                if (methodInfoPtr == IntPtr.Zero)
                {
                    MelonLogger.Warning("[ChatPatches] InvokeUserCode NativeMethodInfoPtr is zero");
                    return;
                }

                // Il2CppMethodInfo.methodPointer is at offset 0
                IntPtr methodPtr = *(IntPtr*)methodInfoPtr;
                if (methodPtr == IntPtr.Zero)
                {
                    MelonLogger.Warning("[ChatPatches] InvokeUserCode native method pointer is zero");
                    return;
                }

                // Create hook delegate (prevent GC)
                _hookDelegate = new d_InvokeUserCode_CommandSendChat(NativeHook_InvokeUserCode);
                IntPtr hookPtr = Marshal.GetFunctionPointerForDelegate(_hookDelegate);

                // Attach native hook
                IntPtr originalPtr = methodPtr;
                #pragma warning disable CS0618
                MelonUtils.NativeHookAttach((IntPtr)(&originalPtr), hookPtr);
#pragma warning restore CS0618
                _originalInvokeUserCode = Marshal.GetDelegateForFunctionPointer<d_InvokeUserCode_CommandSendChat>(originalPtr);

                MelonLogger.Msg("[ChatPatches] Native hook installed for chat commands");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ChatPatches] Failed to install native hook: {ex}");
            }
        }

        /// <summary>
        /// Native hook for InvokeUserCode_CommandSendChatMessage__String.
        /// This is the TRUE Mirror dispatch entry point for incoming chat Commands.
        /// Parameters: obj=PlayerController, reader=NetworkReader, senderConn=connection, methodInfo=IL2CPP method
        /// </summary>
        private static void NativeHook_InvokeUserCode(IntPtr obj, IntPtr reader, IntPtr senderConn, IntPtr methodInfo)
        {
            try
            {
                // Read the message string from the NetworkReader
                string message = ReadStringFromReader(reader);

                if (string.IsNullOrEmpty(message))
                    return;

                // Resolve sender from PlayerController instance
                string senderName = "Player";
                uint senderAccountId = 0;
                ResolveSenderFromInstance(obj, ref senderName, ref senderAccountId);

                // Broadcast to all connected clients (including host-client)
                BroadcastChat(senderAccountId, senderName, message);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ChatPatches] NativeHook error: {ex}");
                // Fallback: call original
                try { _originalInvokeUserCode?.Invoke(obj, reader, senderConn, methodInfo); }
                catch { }
            }
        }

        /// <summary>
        /// Read a string from a NetworkReader IntPtr by wrapping it and calling ReadString.
        /// </summary>
        private static string ReadStringFromReader(IntPtr readerPtr)
        {
            if (readerPtr == IntPtr.Zero || _readerReadStringMethod == null) return "";

            try
            {
                // Create a properly typed NetworkReader wrapper from the native pointer.
                // Il2CppInterop-generated types have a (IntPtr) constructor for this purpose.
                object? readerObj = null;
                if (_networkReaderPtrCtor != null)
                {
                    readerObj = _networkReaderPtrCtor.Invoke(new object[] { readerPtr });
                }

                if (readerObj == null)
                {
                    MelonLogger.Warning("[ChatPatches] Failed to construct NetworkReader wrapper");
                    return "";
                }

                if (_readerReadStringMethod.IsStatic)
                {
                    // Extension method: ReadString(NetworkReader reader)
                    var result = _readerReadStringMethod.Invoke(null, new object[] { readerObj });
                    return result?.ToString() ?? "";
                }
                else
                {
                    // Instance method: reader.ReadString()
                    var result = _readerReadStringMethod.Invoke(readerObj, null);
                    return result?.ToString() ?? "";
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ChatPatches] ReadStringFromReader failed: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Resolve sender name/ID by matching a native PlayerController instance pointer.
        /// </summary>
        private static void ResolveSenderFromInstance(IntPtr instancePtr, ref string name, ref uint accountId)
        {
            try
            {
                var gaInstance = _gaInstanceProp?.GetValue(null);
                if (gaInstance == null || _getMappingsMethod == null) return;

                var mappings = _getMappingsMethod.Invoke(gaInstance, null);
                if (mappings == null) return;

                int count = IL2CppArrayHelper.GetLen(mappings);
                var itemProp = IL2CppArrayHelper.GetItemProperty(mappings);

                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        var mapping = itemProp?.GetValue(mappings, new object[] { i });
                        if (mapping == null) continue;

                        var controller = _mappingControllerProp?.GetValue(mapping);
                        if (controller == null) continue;

                        // Compare IL2CPP native pointers
                        IntPtr controllerPtr = controller is Il2CppObjectBase il2cppObj
                            ? IL2CPP.Il2CppObjectBaseToPtr(il2cppObj)
                            : IntPtr.Zero;

                        if (controllerPtr == instancePtr)
                        {
                            var n = _mappingDisplayNameProp?.GetValue(mapping)?.ToString();
                            if (!string.IsNullOrEmpty(n)) name = n;
                            accountId = (uint)(_mappingPlayerIdProp?.GetValue(mapping) ?? 0u);
                            return;
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        // ================================================================
        // Harmony patches
        // ================================================================

        private static void TryPatch(HarmonyLib.Harmony harmony, Type type, string methodName,
            string? prefixName, string? postfixName, string label)
        {
            try
            {
                var method = type.GetMethod(methodName, FLAGS);
                if (method == null)
                {
                    MelonLogger.Warning($"[ChatPatches] {label}: method '{methodName}' not found");
                    return;
                }

                var prefix = prefixName != null
                    ? new HarmonyLib.HarmonyMethod(typeof(ChatPatches).GetMethod(prefixName, FLAGS))
                    : null;
                var postfix = postfixName != null
                    ? new HarmonyLib.HarmonyMethod(typeof(ChatPatches).GetMethod(postfixName, FLAGS))
                    : null;

                harmony.Patch(method, prefix: prefix, postfix: postfix);
                MelonLogger.Msg($"[ChatPatches] {label}: PATCHED OK");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ChatPatches] {label}: patch FAILED — {ex.Message}");
            }
        }

        public static bool Prefix_CommandSendChat(object __instance, string __0)
        {
            try
            {
                string message = __0;
                if (string.IsNullOrEmpty(message)) return false;

                MelonLogger.Msg($"[ChatPatches] Chat: '{GetLocalSteamName()}' says: {message}");

                if (IsServerActive())
                {
                    BroadcastChat(GetLocalSteamId(), GetLocalSteamName(), message);
                    return false;
                }
                else
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ChatPatches] Error in CommandSendChat: {ex}");
                return true;
            }
        }

        public static bool Prefix_AddChatMessage(object __instance, uint __0, string __1, string __2, bool __3)
        {
            if (_insideOurAddChat) return true;

            string senderName = __1;
            string message = __2;
            bool ownMessage = __3;
            uint accountId = __0;

            if (senderName != "Splayer" && !string.IsNullOrEmpty(senderName))
                return true;

            if (ownMessage)
            {
                string fixedName = GetLocalSteamName();
                uint fixedId = GetLocalSteamId();
                CallAddChatMessage(__instance, fixedId, fixedName, message, true);
                return false;
            }
            else
            {
                string fixedName = ResolveNameByAccountId(accountId);
                CallAddChatMessage(__instance, accountId, fixedName, message, false);
                return false;
            }
        }

        // ================================================================
        // Shared helpers
        // ================================================================

        private static void CallAddChatMessage(object hudInstance, uint accountId,
            string senderName, string message, bool ownMessage)
        {
            if (_addChatMessageMethod == null) return;
            try
            {
                _insideOurAddChat = true;
                _addChatMessageMethod.Invoke(hudInstance, new object[] { accountId, senderName, message, ownMessage });
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ChatPatches] CallAddChatMessage failed: {ex.Message}");
            }
            finally
            {
                _insideOurAddChat = false;
            }
        }

        private static void ShowLocalChatMessage(uint accountId, string senderName,
            string message, bool ownMessage)
        {
            try
            {
                object? hudInstance = _hudInstanceProp?.GetValue(null);
                if (hudInstance == null)
                    hudInstance = _hudInstanceField?.GetValue(null);

                if (hudInstance == null)
                {
                    MelonLogger.Warning("[ChatPatches] HUD_Messages.Instance is null");
                    return;
                }

                CallAddChatMessage(hudInstance, accountId, senderName, message, ownMessage);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ChatPatches] ShowLocalChatMessage failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Broadcasts a chat message to connected clients via RPC.
        /// </summary>
        private static void BroadcastChat(uint senderAccountId, string senderName, string message)
        {
            var gaInstance = _gaInstanceProp?.GetValue(null);
            if (gaInstance == null || _getMappingsMethod == null || _rpcBroadcastMethod == null) return;

            var mappings = _getMappingsMethod.Invoke(gaInstance, null);
            if (mappings == null) return;

            int count = IL2CppArrayHelper.GetLen(mappings);
            var itemProp = IL2CppArrayHelper.GetItemProperty(mappings);

            int sent = 0;
            for (int i = 0; i < count; i++)
            {
                try
                {
                    var mapping = itemProp?.GetValue(mappings, new object[] { i });
                    if (mapping == null) continue;

                    var controller = _mappingControllerProp?.GetValue(mapping);
                    if (controller == null) continue;

                    var connProp = controller.GetType().GetProperty("connectionToClient", FLAGS);
                    var conn = connProp?.GetValue(controller);
                    if (conn == null) continue;

                    _rpcBroadcastMethod.Invoke(controller, new object[]
                    {
                        conn, senderAccountId, senderName, message, false
                    });
                    sent++;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[ChatPatches] Broadcast to player {i} failed: {ex.Message}");
                }
            }
            MelonLogger.Msg($"[ChatPatches] Broadcast to {sent} players");
        }

        private static string ResolveNameByAccountId(uint accountId)
        {
            try
            {
                var gaInstance = _gaInstanceProp?.GetValue(null);
                if (gaInstance == null || _getMappingsMethod == null) return "Player";

                var mappings = _getMappingsMethod.Invoke(gaInstance, null);
                if (mappings == null) return "Player";

                int count = IL2CppArrayHelper.GetLen(mappings);
                var itemProp = IL2CppArrayHelper.GetItemProperty(mappings);

                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        var mapping = itemProp?.GetValue(mappings, new object[] { i });
                        if (mapping == null) continue;

                        var pid = (uint)(_mappingPlayerIdProp?.GetValue(mapping) ?? 0u);
                        if (pid == accountId)
                        {
                            var name = _mappingDisplayNameProp?.GetValue(mapping)?.ToString();
                            if (!string.IsNullOrEmpty(name)) return name;
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return "Player";
        }

        private static string GetLocalSteamName()
        {
            if (_cachedSteamName != null) return _cachedSteamName;
            try { _cachedSteamName = Steamworks.SteamFriends.GetPersonaName() ?? "Unknown"; }
            catch { _cachedSteamName = "Unknown"; }
            return _cachedSteamName;
        }

        private static uint GetLocalSteamId()
        {
            if (_cachedSteamId != 0) return _cachedSteamId;
            _cachedSteamId = StringToUintFNV1a.Compute(GetLocalSteamName());
            return _cachedSteamId;
        }

        private static bool IsServerActive()
        {
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                var nsType = assemblies
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                    .FirstOrDefault(t => t.FullName == "Mirror.NetworkServer" || t.FullName == "Il2CppMirror.NetworkServer");
                if (nsType == null) return false;

                var activeProp = nsType.GetProperty("active", BindingFlags.Public | BindingFlags.Static);
                return activeProp != null && (bool)(activeProp.GetValue(null) ?? false);
            }
            catch { return false; }
        }
    }
}
