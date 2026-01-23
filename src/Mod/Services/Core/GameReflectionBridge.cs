using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace SiroccoLobby.Services.Core
{
    /// <summary>
    /// Immutable, typed-accessor bridge for all game-side reflection.
    /// Discovers and caches required types/fields/properties at construction.
    /// </summary>
    public class GameReflectionBridge
    {
        // Cached types, instances, properties, and methods
    public Type? GameAuthorityType { get; private set; }
    public object? GameAuthorityInstance { get; private set; }
    public PropertyInfo? IsSinglePlayerProp { get; private set; }
    public PropertyInfo? CaptainsListProp { get; private set; }
    public PropertyInfo? SelectedIndexProp { get; private set; }
    public PropertyInfo? SelectedCaptainProp { get; private set; }
    public PropertyInfo? UserNameProp { get; private set; }
    public PropertyInfo? TeamSelectionIndexProp { get; private set; }
    public MethodInfo? InitCaptainSelectionMethod { get; private set; }
    public MethodInfo? CompleteProtoLobbyMethod { get; private set; }
    public MethodInfo? CompleteProtoLobbyClientMethod { get; private set; }
    public Type? DropdownType { get; private set; }

    public object? NetworkManagerInstance { get; private set; }
    public MethodInfo? StartSinglePlayerP2PMethod { get; private set; }
    public MethodInfo? StartSinglePlayerMethod { get; private set; }
    public MethodInfo? FinishStartHostMethod { get; private set; }
    public MethodInfo? StartHostClientMethod { get; private set; }
    public MethodInfo? StopClientMethod { get; private set; }
    public MethodInfo? StopServerMethod { get; private set; }
    public MethodInfo? StopHostMethod { get; private set; }

    public object? AuthenticatorInstance { get; private set; }
    public MethodInfo? OnClientAuthenticateMethod { get; private set; }
    public Type? AuthRequestMessageType { get; private set; }

    public Type? NetworkClientType { get; private set; }
    public MethodInfo? ConnectMethod { get; private set; }
    public MethodInfo? ConnectLocalServerMethod { get; private set; }
    public MethodInfo? DisconnectMethod { get; private set; }
    public MethodInfo? NetworkClientReadyMethod { get; private set; }
    public MethodInfo? NetworkClientAddPlayerMethod { get; private set; }
    public PropertyInfo? NetworkClientIsAuthenticatedProp { get; private set; }

    public Type? NetworkServerType { get; private set; }

    public Type? TesterType { get; private set; }
    public object? TesterInstance { get; set; }
    public FieldInfo? CachedInfoField { get; private set; }
    public PropertyInfo? PlayersListProp { get; private set; }
    public Type? PlayerStatusInfoType { get; private set; }
    public PropertyInfo? PsIsReadyProp { get; private set; }
    public PropertyInfo? PsIsTeamAProp { get; private set; }
    public PropertyInfo? PsIsConnectedProp { get; private set; }
    public MethodInfo? ValidatePlayersReadyMethod { get; private set; }

    public MethodInfo? IntegrateWithProtoLobbyMethod { get; private set; }
    public MethodInfo? CompleteSteamP2PProtoLobbyMethod { get; private set; }
    public MethodInfo? DisplayProtoLobbyPlayerStatusMethod { get; private set; }

        public bool IsValid { get; private set; }

        public GameReflectionBridge()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var assemblyCSharp = assemblies.FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            if (assemblyCSharp == null)
            {
                MelonLoader.MelonLogger.Warning("[GameReflectionBridge] Assembly-CSharp not found");
                IsValid = false;
                return;
            }

            InitializeGameAuthority(assemblyCSharp);
            InitializeNetworkManager(assemblyCSharp);
            InitializeNetworkClient(assemblies);
            InitializeNetworkServer(assemblies);
            InitializeTester(assemblyCSharp);

            IsValid = GameAuthorityType != null && NetworkManagerInstance != null && NetworkClientType != null;
        }

        private void InitializeGameAuthority(Assembly assemblyCSharp)
        {
            GameAuthorityType = assemblyCSharp.GetType("Il2CppWartide.GameAuthority");
            if (GameAuthorityType == null)
            {
                MelonLoader.MelonLogger.Warning("[GameReflectionBridge] GameAuthorityType not found.");
                return;
            }
            var instanceProp = GameAuthorityType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (instanceProp != null) GameAuthorityInstance = instanceProp.GetValue(null);
            else MelonLoader.MelonLogger.Warning("[GameReflectionBridge] GameAuthority.Instance property not found.");

            CaptainsListProp = GameAuthorityType.GetProperty("_protoLobbyDropdownAvailableCaptains", BindingFlags.Public | BindingFlags.Instance);
            if (CaptainsListProp == null) MelonLoader.MelonLogger.Warning("[GameReflectionBridge] _protoLobbyDropdownAvailableCaptains property not found.");
            SelectedIndexProp = GameAuthorityType.GetProperty("_protoLobbyCaptainSelectedIndex", BindingFlags.Public | BindingFlags.Instance);
            SelectedCaptainProp = GameAuthorityType.GetProperty("_protoLobbyClientSelectedCaptain", BindingFlags.Public | BindingFlags.Instance);
            UserNameProp = GameAuthorityType.GetProperty("_protoLobbyUserName", BindingFlags.Public | BindingFlags.Instance);
            TeamSelectionIndexProp = GameAuthorityType.GetProperty("_lobbyTeamSelectionIndex", BindingFlags.Public | BindingFlags.Instance);
            InitCaptainSelectionMethod = GameAuthorityType.GetMethod("InitializeProtoLobbyCaptainSelection", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            IsSinglePlayerProp = GameAuthorityType.GetProperty("_isSinglePlayer", BindingFlags.Public | BindingFlags.Instance);
            DropdownType = GameAuthorityType.GetNestedType("ProtoLobbyCaptainDropdown", BindingFlags.Public | BindingFlags.NonPublic);
            CompleteProtoLobbyMethod = GameAuthorityType.GetMethod("CompleteProtoLobbyServer", BindingFlags.Public | BindingFlags.Instance);
            CompleteProtoLobbyClientMethod = GameAuthorityType.GetMethod("CompleteProtoLobbyClient", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private void InitializeNetworkManager(Assembly assemblyCSharp)
        {
            var managerType = assemblyCSharp.GetType("Il2CppWartide.WartideNetworkManager");
            if (managerType == null)
            {
                MelonLoader.MelonLogger.Warning("[GameReflectionBridge] WartideNetworkManager type not found.");
                return;
            }
            var instanceProp = managerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (instanceProp != null)
                NetworkManagerInstance = instanceProp.GetValue(null);
            else
                MelonLoader.MelonLogger.Warning("[GameReflectionBridge] WartideNetworkManager.Instance property not found.");

            StartSinglePlayerP2PMethod = managerType.GetMethod("StartSinglePlayerWithSteamP2P", BindingFlags.Public | BindingFlags.Instance);
            StartSinglePlayerMethod = managerType.GetMethod("StartSinglePlayer", BindingFlags.Public | BindingFlags.Instance);
            FinishStartHostMethod = managerType.GetMethod("FinishStartHost", BindingFlags.Public | BindingFlags.Instance);
            StartHostClientMethod = managerType.GetMethod("StartHostClient", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            StopClientMethod = managerType.GetMethod("StopClient", BindingFlags.Public | BindingFlags.Instance);
            StopServerMethod = managerType.GetMethod("StopServer", BindingFlags.Public | BindingFlags.Instance);
            StopHostMethod = managerType.GetMethod("StopHost", BindingFlags.Public | BindingFlags.Instance);

            // Authenticator discovery
            // In standard Mirror, NetworkManager has a public field `authenticator`.
            // In this IL2CPP title, the member may be non-public, renamed, or declared on a base type.
            // So we search fields/properties by common name(s) and ensure we can invoke OnClientAuthenticate.
            AuthenticatorInstance = TryGetAuthenticatorInstance(managerType, NetworkManagerInstance);
            if (AuthenticatorInstance != null)
            {
                OnClientAuthenticateMethod = AuthenticatorInstance
                    .GetType()
                    .GetMethod("OnClientAuthenticate", BindingFlags.Public | BindingFlags.Instance);
                AuthRequestMessageType = AuthenticatorInstance
                    .GetType()
                    .GetNestedType("AuthRequestMessage", BindingFlags.Public | BindingFlags.NonPublic);

                if (OnClientAuthenticateMethod == null)
                    MelonLoader.MelonLogger.Warning("[GameReflectionBridge] Authenticator found, but OnClientAuthenticate() not found.");
            }
            else
            {
                // This may be expected early in startup or in modes where the manager/authenticator isn't constructed yet.
                // Keep signal low here; NetworkIntegrationService will attempt a best-effort auth trigger later when connecting.
                MelonLoader.MelonLogger.Msg("[GameReflectionBridge] Authenticator instance not found on WartideNetworkManager (searched fields/properties + base types)." );
            }
        }

        private static object? TryGetAuthenticatorInstance(Type managerType, object? managerInstance)
        {
            if (managerInstance == null)
                return null;

            const BindingFlags allInst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            string[] candidateNames = { "authenticator", "Authenticator", "m_Authenticator" };

            for (Type? t = managerType; t != null; t = t.BaseType)
            {
                foreach (var name in candidateNames)
                {
                    // Try field
                    var f = t.GetField(name, allInst);
                    if (f != null)
                    {
                        var v = f.GetValue(managerInstance);
                        if (v != null) return v;
                    }

                    // Try property
                    var p = t.GetProperty(name, allInst);
                    if (p != null && p.GetIndexParameters().Length == 0)
                    {
                        try
                        {
                            var v = p.GetValue(managerInstance);
                            if (v != null) return v;
                        }
                        catch
                        {
                            // ignore and keep searching
                        }
                    }
                }
            }

            // Last-resort heuristic: find any member whose type appears to be a Mirror authenticator.
            // We keep it extremely conservative: must have OnClientAuthenticate() method.
            for (Type? t = managerType; t != null; t = t.BaseType)
            {
                foreach (var f in t.GetFields(allInst))
                {
                    if (f.FieldType == null) continue;
                    if (f.FieldType.GetMethod("OnClientAuthenticate", BindingFlags.Public | BindingFlags.Instance) == null)
                        continue;

                    var v = f.GetValue(managerInstance);
                    if (v != null) return v;
                }

                foreach (var p in t.GetProperties(allInst))
                {
                    if (p.GetIndexParameters().Length != 0) continue;
                    if (p.PropertyType == null) continue;
                    if (p.PropertyType.GetMethod("OnClientAuthenticate", BindingFlags.Public | BindingFlags.Instance) == null)
                        continue;

                    try
                    {
                        var v = p.GetValue(managerInstance);
                        if (v != null) return v;
                    }
                    catch
                    {
                        // ignore and keep searching
                    }
                }
            }

            return null;
        }

        private void InitializeNetworkClient(Assembly[] assemblies)
        {
            NetworkClientType = assemblies
                .SelectMany(a => SafeGetTypes(a))
                .FirstOrDefault(t => t.FullName == "Mirror.NetworkClient" || t.FullName == "Il2CppMirror.NetworkClient");
            if (NetworkClientType == null)
            {
                MelonLoader.MelonLogger.Warning("[GameReflectionBridge] NetworkClient type not found in any assembly!");
                return;
            }
            var methods = NetworkClientType.GetMethods(BindingFlags.Public | BindingFlags.Static);
            ConnectMethod = methods.FirstOrDefault(m => m.Name == "Connect" && m.GetParameters().Length >= 1);
            ConnectLocalServerMethod = methods.FirstOrDefault(m => m.Name == "ConnectLocalServer" && m.GetParameters().Length == 0);
            DisconnectMethod = methods.FirstOrDefault(m => m.Name == "Disconnect" && m.GetParameters().Length == 0);
            NetworkClientReadyMethod = methods.FirstOrDefault(m => m.Name == "Ready" && m.GetParameters().Length == 0);
            NetworkClientAddPlayerMethod = methods.FirstOrDefault(m => m.Name == "AddPlayer" && m.GetParameters().Length == 5)
                ?? methods.FirstOrDefault(m => m.Name == "AddPlayer" && m.GetParameters().Length == 0);

            // Auth gating
            // NetworkClientType can be null if Mirror types weren't discovered.
            NetworkClientIsAuthenticatedProp = NetworkClientType?.GetProperty("isAuthenticated", BindingFlags.Public | BindingFlags.Static);
        }

        private void InitializeNetworkServer(Assembly[] assemblies)
        {
            NetworkServerType = assemblies
                .SelectMany(a => SafeGetTypes(a))
                .FirstOrDefault(t => t.FullName == "Mirror.NetworkServer" || t.FullName == "Il2CppMirror.NetworkServer");
            if (NetworkServerType == null)
                MelonLoader.MelonLogger.Warning("[GameReflectionBridge] NetworkServer type not found in any assembly!");
        }

        private void InitializeTester(Assembly assemblyCSharp)
        {
            TesterType = assemblyCSharp.GetType("Il2CppWartide.Testing.SteamP2PNetworkTester");
            if (TesterType == null)
            {
                MelonLoader.MelonLogger.Warning("[GameReflectionBridge] SteamP2PNetworkTester type not found.");
                return;
            }

            // Best-effort: find a live instance if the MonoBehaviour exists in the current scene.
            // This may legitimately be null early in startup.
            TesterInstance = TryFindUnityObjectInstance(TesterType);

            CachedInfoField = TesterType.GetField("cachedConnectionInfo", BindingFlags.NonPublic | BindingFlags.Instance);
            var infoType = TesterType.GetNestedType("ConnectionStatusInfo", BindingFlags.Public) ?? assemblyCSharp.GetType("Il2CppWartide.Testing.SteamP2PNetworkTester+ConnectionStatusInfo");
            if (infoType != null)
            {
                PlayersListProp = infoType.GetProperty("Players", BindingFlags.Public | BindingFlags.Instance);
            }
            PlayerStatusInfoType = TesterType.GetNestedType("PlayerStatusInfo", BindingFlags.Public) ?? assemblyCSharp.GetType("Il2CppWartide.Testing.SteamP2PNetworkTester+PlayerStatusInfo");
            if (PlayerStatusInfoType != null)
            {
                PsIsReadyProp = PlayerStatusInfoType.GetProperty("IsReady", BindingFlags.Public | BindingFlags.Instance);
                PsIsTeamAProp = PlayerStatusInfoType.GetProperty("IsTeamA", BindingFlags.Public | BindingFlags.Instance);
                PsIsConnectedProp = PlayerStatusInfoType.GetProperty("IsConnected", BindingFlags.Public | BindingFlags.Instance);
            }
            ValidatePlayersReadyMethod = TesterType?.GetMethod("ValidatePlayersReadyForGameStart", BindingFlags.Public | BindingFlags.Instance);

            // Proto-lobby related methods on the tester.
            IntegrateWithProtoLobbyMethod = TesterType?.GetMethod("IntegrateWithProtoLobby", BindingFlags.Public | BindingFlags.Instance);
            CompleteSteamP2PProtoLobbyMethod = TesterType?.GetMethod("CompleteSteamP2PProtoLobby", BindingFlags.Public | BindingFlags.Instance);
            DisplayProtoLobbyPlayerStatusMethod = TesterType?.GetMethod("DisplayProtoLobbyPlayerStatus", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private static object? TryFindUnityObjectInstance(Type unityObjectType)
        {
            try
            {
                // IMPORTANT: UnityEngine.Object/Resources are not in Assembly-CSharp.
                // Look them up from any loaded assembly (typically UnityEngine.CoreModule).
                var unityObject = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Select(a => a.GetType("UnityEngine.Object", throwOnError: false))
                    .FirstOrDefault(t => t != null);

                if (unityObject != null)
                {
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
                }

                // Fallback: Resources.FindObjectsOfTypeAll(Type) to catch inactive/hidden objects.
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

        // Example RequireX helper for critical methods
        public MethodInfo RequireConnectMethod() => ConnectMethod ?? throw new InvalidOperationException("NetworkClient.Connect not found");
        public object RequireGameAuthorityInstance() => GameAuthorityInstance ?? throw new InvalidOperationException("GameAuthority.Instance not found");
        public Type RequireGameAuthorityType() => GameAuthorityType ?? throw new InvalidOperationException("GameAuthorityType not found");

        // Helper: get all types from an assembly, safely
        private static IEnumerable<Type> SafeGetTypes(Assembly a)
        {
            try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
        }
    }
}
