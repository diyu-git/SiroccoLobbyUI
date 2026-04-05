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

        private bool _hasWarnedMissingTesterValidation;

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

        /// <summary>
        /// Disables AI brains on dummy players by setting
        /// SimulationManager._isUsingDummyPlayersWithBrains = false.
        /// Gets SimulationManager via GameAuthority.GetSimulationManager().
        /// </summary>
        public bool DisableDummyBrains()
        {
            try
            {
                var gaInstance = _reflection.GameAuthorityInstance;
                var gaType = _reflection.GameAuthorityType;
                if (gaInstance == null || gaType == null) return false;

                var getSimMethod = gaType.GetMethod("GetSimulationManager", BindingFlags.Public | BindingFlags.Instance);
                if (getSimMethod == null) return false;

                var simInstance = getSimMethod.Invoke(gaInstance, null);
                if (simInstance == null) return false;

                var prop = simInstance.GetType().GetProperty("_isUsingDummyPlayersWithBrains", BindingFlags.Public | BindingFlags.Instance);
                if (prop == null) return false;

                prop.SetValue(simInstance, false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool IsServerActive => _networkServerActiveProp != null && (bool)(_networkServerActiveProp.GetValue(null) ?? false);

        /// <summary>
        /// Gets player info from GameAuthority.GetPlayerConnectionMappings().
        /// Returns DisplayName, IsTeamA, and CaptainTypeID.Value for each registered player.
        /// </summary>
        public List<(string Name, bool IsTeamA, bool IsReady, bool IsConnected, string CaptainId)> GetGamePlayerStatus()
        {
            var result = new List<(string, bool, bool, bool, string)>();
            try
            {
                var gaInstance = _reflection.GameAuthorityInstance;
                var gaType = _reflection.GameAuthorityType;
                if (gaInstance == null || gaType == null) return result;

                var getMappingsMethod = gaType.GetMethod("GetPlayerConnectionMappings", BindingFlags.Public | BindingFlags.Instance);
                if (getMappingsMethod == null) return result;

                var mappings = getMappingsMethod.Invoke(gaInstance, null);
                if (mappings == null) return result;

                int length = IL2CppArrayHelper.GetLen(mappings);
                var itemProp = IL2CppArrayHelper.GetItemProperty(mappings);

                for (int i = 0; i < length; i++)
                {
                    object? mapping = null;
                    try { mapping = itemProp?.GetValue(mappings, new object[] { i }); }
                    catch { continue; }
                    if (mapping == null) continue;

                    var displayNameProp = mapping.GetType().GetProperty("DisplayName", BindingFlags.Public | BindingFlags.Instance);
                    var isTeamAProp = mapping.GetType().GetProperty("IsTeamA", BindingFlags.Public | BindingFlags.Instance);
                    var captainTypeIDProp = mapping.GetType().GetProperty("CaptainTypeID", BindingFlags.Public | BindingFlags.Instance);

                    string name = displayNameProp?.GetValue(mapping)?.ToString() ?? "";
                    bool isTeamA = (bool)(isTeamAProp?.GetValue(mapping) ?? false);

                    string captainId = "";
                    if (captainTypeIDProp != null)
                    {
                        try
                        {
                            var typeId = captainTypeIDProp.GetValue(mapping);
                            if (typeId != null)
                            {
                                var valueProp = typeId.GetType().GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
                                if (valueProp != null)
                                    captainId = valueProp.GetValue(typeId)?.ToString() ?? "";
                                else
                                {
                                    var valueField = typeId.GetType().GetField("Value", BindingFlags.Public | BindingFlags.Instance);
                                    if (valueField != null)
                                        captainId = valueField.GetValue(typeId)?.ToString() ?? "";
                                }
                            }
                        }
                        catch { }
                    }

                    if (string.IsNullOrEmpty(name)) continue;
                    result.Add((name, isTeamA, true, true, captainId));
                }
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Warning($"[GameStatus] Error: {ex.Message}");
            }
            return result;
        }

        /// <summary>
        /// Gets Steam IDs of players connected via Steam P2P transport.
        /// On the server: reads GetAllConnections().
        /// On the client: reads GetClientConnectionInfo() for the host connection.
        /// </summary>
        public List<(ulong SteamId, string Name)> GetP2PConnectedPlayers()
        {
            var result = new List<(ulong, string)>();
            try
            {
                if (_reflection.GetSteamP2PTransportMethod == null || _reflection.NetworkManagerInstance == null)
                    return result;

                var transport = _reflection.GetSteamP2PTransportMethod.Invoke(_reflection.NetworkManagerInstance, null);
                if (transport == null) return result;

                // Server: iterate all connections
                var getAllMethod = transport.GetType().GetMethod("GetAllConnections", BindingFlags.Public | BindingFlags.Instance);
                if (getAllMethod != null)
                {
                    var connections = getAllMethod.Invoke(transport, null);
                    if (connections != null)
                    {
                        var valuesProperty = connections.GetType().GetProperty("Values");
                        var values = valuesProperty?.GetValue(connections);
                        if (values != null)
                        {
                            var enumerator = values.GetType().GetMethod("GetEnumerator")?.Invoke(values, null);
                            if (enumerator != null)
                            {
                                var moveNext = enumerator.GetType().GetMethod("MoveNext");
                                var currentProp = enumerator.GetType().GetProperty("Current");
                                if (moveNext != null && currentProp != null)
                                {
                                    while ((bool)(moveNext.Invoke(enumerator, null) ?? false))
                                    {
                                        var steamId = ExtractSteamIdFromConnection(currentProp.GetValue(enumerator));
                                        if (steamId != 0) result.Add((steamId, ResolveSteamName(steamId)));
                                    }
                                }
                            }
                        }
                    }
                }

                // Client: get server connection
                if (result.Count == 0)
                {
                    var getClientConnMethod = transport.GetType().GetMethod("GetClientConnectionInfo", BindingFlags.Public | BindingFlags.Instance);
                    if (getClientConnMethod != null)
                    {
                        var clientConn = getClientConnMethod.Invoke(transport, null);
                        var steamId = ExtractSteamIdFromConnection(clientConn);
                        if (steamId != 0) result.Add((steamId, ResolveSteamName(steamId)));
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Warning($"[P2P] Error discovering connections: {ex.Message}");
            }
            return result;
        }

        private static ulong ExtractSteamIdFromConnection(object? connection)
        {
            if (connection == null) return 0;
            try
            {
                var prop = connection.GetType().GetProperty("RemoteSteamId", BindingFlags.Public | BindingFlags.Instance);
                if (prop == null) return 0;
                var remoteSteamId = prop.GetValue(connection);
                if (remoteSteamId == null) return 0;

                var field = remoteSteamId.GetType().GetField("m_SteamID", BindingFlags.Public | BindingFlags.Instance);
                if (field != null) return (ulong)(field.GetValue(remoteSteamId) ?? 0UL);
                if (ulong.TryParse(remoteSteamId.ToString(), out var parsed)) return parsed;
            }
            catch { }
            return 0;
        }

        private static string ResolveSteamName(ulong steamId)
        {
            try
            {
                var name = Steamworks.SteamFriends.GetFriendPersonaName(new Steamworks.CSteamID(steamId));
                if (!string.IsNullOrEmpty(name)) return name;
            }
            catch { }
            return steamId.ToString();
        }

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
                    // Try using the game's IntegrateWithProtoLobby method for Steam P2P
                    if (TryIntegrateWithProtoLobby(address))
                    {
                        MelonLoader.MelonLogger.Msg($"[NetworkIntegrationService] Connected via IntegrateWithProtoLobby to: {address}");
                    }
                    else
                    {
                        // Fallback to manual connection
                        ConnectToRemoteServer(address);
                    }
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

        private bool TryIntegrateWithProtoLobby(string hostSteamId)
        {
            // First, try the game's ConnectToSteamID method (from the in-game UI button)
            if (_reflection.TesterInstance != null)
            {
                var connectMethod = _reflection.ConnectToSteamIDMethod;
                if (connectMethod == null && _reflection.TesterType != null)
                {
                    connectMethod = _reflection.TesterType.GetMethod("ConnectToSteamID",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                }

                if (connectMethod != null)
                {
                    try
                    {
                        InvokeSafe(connectMethod, _reflection.TesterInstance, hostSteamId);
                        MelonLoader.MelonLogger.Msg($"[NetworkIntegrationService] Connected via ConnectToSteamID({hostSteamId})");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        MelonLoader.MelonLogger.Warning($"[NetworkIntegrationService] ConnectToSteamID failed: {ex.Message}");
                    }
                }
            }
            
            // Fallback: Replicate ConnectToSteamID logic manually
            if (_reflection.NetworkManagerInstance == null)
            {
                MelonLoader.MelonLogger.Error("[NetworkIntegrationService] NetworkManager not found!");
                return false;
            }
            
            try
            {
                // Step 1: Enable Steam P2P
                if (_reflection.TesterInstance != null && _reflection.TesterType != null)
                {
                    var enableMethod = _reflection.TesterType.GetMethod("EnableSteamP2P",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (enableMethod != null)
                        InvokeSafe(enableMethod, _reflection.TesterInstance);
                }

                // Step 2: Set NetworkAddress to Steam ID
                var networkManagerType = _reflection.NetworkManagerInstance.GetType();
                var networkAddressField = networkManagerType.GetField("NetworkAddress",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (networkAddressField != null)
                {
                    networkAddressField.SetValue(_reflection.NetworkManagerInstance, hostSteamId);
                }
                else if (_reflection.NetworkAddressProp != null)
                {
                    _reflection.NetworkAddressProp.SetValue(_reflection.NetworkManagerInstance, hostSteamId);
                }
                else
                {
                    MelonLoader.MelonLogger.Error("[NetworkIntegrationService] Cannot set host Steam ID — no NetworkAddress field or property.");
                    return false;
                }

                // Step 3: Call StartClientOnly()
                var startClientOnlyMethod = networkManagerType.GetMethod("StartClientOnly",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (startClientOnlyMethod != null)
                {
                    InvokeSafe(startClientOnlyMethod, _reflection.NetworkManagerInstance);
                }
                else
                {
                    MelonLoader.MelonLogger.Warning("[NetworkIntegrationService] StartClientOnly method not found!");
                    return false;
                }

                // Step 4: Set GameAuthority to ClientOnly mode
                if (_reflection.GameAuthorityInstance != null)
                {
                    var setClientOnlyMethod = _reflection.GameAuthorityType?.GetMethod("SetClientOnlyMode",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                    if (setClientOnlyMethod != null)
                        InvokeSafe(setClientOnlyMethod, _reflection.GameAuthorityInstance);
                }

                MelonLoader.MelonLogger.Msg("[NetworkIntegrationService] Steam P2P connection sequence completed");
                return true;
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error($"[NetworkIntegrationService] Failed to replicate ConnectToSteamID: {ex.Message}");
                return false;
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
                // In the normal production flow this tester object often does not exist.
                // Treat it as optional/debug-only and avoid error noise.
                if (!_hasWarnedMissingTesterValidation)
                {
                    _hasWarnedMissingTesterValidation = true;
                    MelonLoader.MelonLogger.Msg(
                        "[NetworkIntegrationService] ValidatePlayersReadyForGameStart skipped: " +
                        "SteamP2PNetworkTester not available (expected in production flow).");

                    // One-time: attempt a dump to help us discover the real readiness authority.
                    TryDumpProtoLobbyGraphOnce("ValidatePlayersReadyForGameStart:missing-tester");
                }

                // Neutral default: do not block gameplay on an optional debug component.
                return true;
            }

            bool result = false;
            try
            {
                result = InvokeSafe(_reflection.ValidatePlayersReadyMethod, _reflection.TesterInstance) as bool? ?? false;
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
                    // Diagnostic for dev/debug only; users don't need to see this every ready-toggle.
                    #if DEBUG
                    MelonLoader.MelonLogger.Warning(
                        "[NetworkIntegrationService] Cannot update player status yet (tester reflection not ready). " +
                        $"TesterInstance={( _reflection.TesterInstance != null)}, CachedInfoField={( _reflection.CachedInfoField != null)}, PlayersListProp={( _reflection.PlayersListProp != null)}");
                    #endif

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
                    _reflection.TesterInstance = TryFindUnityObjectInstance(_reflection.TesterType);

                if (_reflection.TesterInstance == null)
                    return;

                if (_reflection.IntegrateWithProtoLobbyMethod != null)
                {
                    try { InvokeSafe(_reflection.IntegrateWithProtoLobbyMethod, _reflection.TesterInstance); }
                    catch { }
                }

                if (_reflection.DisplayProtoLobbyPlayerStatusMethod != null)
                {
                    try { InvokeSafe(_reflection.DisplayProtoLobbyPlayerStatusMethod, _reflection.TesterInstance); }
                    catch { }
                }

                ProtoLobbyDumper.Dump(_reflection.TesterInstance, label: $"SteamP2PNetworkTester graph ({reason})");

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
            if (_reflection.NetworkManagerInstance == null)
            {
                MelonLoader.MelonLogger.Error("[NetworkIntegrationService] NetworkManager instance is NULL — cannot connect!");
                return;
            }

            bool connected = false;

            // Option 1: Use NetworkManager.StartClient(Uri) — proper Mirror pattern for Steam P2P
            if (!connected && _reflection.StartClientWithUriMethod != null)
            {
                try
                {
                    var il2cppUriType = System.Type.GetType("Il2CppSystem.Uri, Il2Cppmscorlib");
                    var uriConstructor = il2cppUriType?.GetConstructor(new[] { typeof(string) });
                    if (uriConstructor != null)
                    {
                        var il2cppUri = uriConstructor.Invoke(new object[] { address });
                        InvokeSafe(_reflection.StartClientWithUriMethod, _reflection.NetworkManagerInstance, il2cppUri);
                        connected = true;
                    }
                }
                catch (Exception ex)
                {
                    MelonLoader.MelonLogger.Warning($"[NetworkIntegrationService] StartClient(Uri) failed: {ex.Message}");
                }
            }

            // Option 2: Set address property and call StartClient() with no parameters
            if (!connected && _reflection.StartClientMethod != null)
            {
                try
                {
                    _reflection.NetworkAddressProp?.SetValue(_reflection.NetworkManagerInstance, address);
                    InvokeSafe(_reflection.StartClientMethod, _reflection.NetworkManagerInstance);
                    connected = true;
                }
                catch (Exception ex)
                {
                    MelonLoader.MelonLogger.Warning($"[NetworkIntegrationService] StartClient() failed: {ex.Message}");
                }
            }

            // Option 3: Fallback to NetworkClient.Connect(address) — may connect to localhost
            if (!connected)
            {
                MelonLoader.MelonLogger.Warning("[NetworkIntegrationService] Using NetworkClient.Connect fallback (may connect to localhost!)");

                var method = _reflection.ConnectMethod;
                if (method == null || method.GetParameters().Length < 1)
                {
                    MelonLoader.MelonLogger.Error("[NetworkIntegrationService] Connect method not found or invalid signature.");
                    return;
                }

                InvokeSafe(method, null, address);
            }

            _authTrace.TrackStates(IsClientConnected, SafeGetIsAuthenticated(), "after-connect-attempt");

            MelonLoader.MelonLogger.Msg($"[NetworkIntegrationService] Connected to remote server: {address}");
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
