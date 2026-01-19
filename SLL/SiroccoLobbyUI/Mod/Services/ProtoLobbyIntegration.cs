using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime;

namespace SiroccoLobby.Services
{
    /// <summary>
    /// Reflection-based integration service for the game's ProtoLobby captain selection system
    /// Uses reflection to avoid compile-time dependencies on game assemblies
    /// OPTIMIZED: Caches all PropertyInfo/MethodInfo objects for performance
    /// </summary>
    public class ProtoLobbyIntegration
    {
        private object? _gameAuthority;
        private PropertyInfo? _isSinglePlayerProp;
        private Type? _gameAuthorityType;
        private bool _isInitialized = false;

        public bool IsConnected 
        {
            get
            {
                if (_networkClientType == null) return false;
                try
                {
                    // Mirror uses 'active' for running client and 'isConnected' for established connection
                    var prop = _networkClientType.GetProperty("active", BindingFlags.Public | BindingFlags.Static);
                    if (prop == null) prop = _networkClientType.GetProperty("isConnected", BindingFlags.Public | BindingFlags.Static);
                    return (bool)(prop?.GetValue(null) ?? false);
                }
                catch { return false; }
            }
        }

        public bool IsServerActive
        {
            get
            {
                if (_networkServerType == null) return false;
                try
                {
                    var prop = _networkServerType.GetProperty("active", BindingFlags.Public | BindingFlags.Static);
                    return (bool)(prop?.GetValue(null) ?? false);
                }
                catch { return false; }
            }
        }

        private Type? _networkServerType;

        // ✅ PERFORMANCE: Cached reflection objects
        private PropertyInfo? _captainsListProp;
        private PropertyInfo? _selectedIndexProp;
        private PropertyInfo? _selectedCaptainProp;
        private PropertyInfo? _userNameProp;
        private PropertyInfo? _teamSelectionIndexProp;
        private MethodInfo? _initCaptainSelectionMethod;
        private MethodInfo? _completeProtoLobbyMethod;
        private MethodInfo? _completeProtoLobbyClientMethod;
        private Type? _dropdownType;

        // Tester / P2P Sync Reflection
        private Type? _testerType;
        private object? _testerInstance;
        private FieldInfo? _cachedInfoField;
        private PropertyInfo? _playersListProp;
        private Type? _playerStatusInfoType;
        private PropertyInfo? _psIsReadyProp;
        private PropertyInfo? _psIsTeamAProp;
        private PropertyInfo? _psIsConnectedProp;
        
        // ✅ PERFORMANCE: Cached captain name property (found at runtime)
        private PropertyInfo? _captainNameProp;
        private bool _hasLoggedCaptainDebug = false;
        private bool _captainNameSearchFailed = false; // Negative caching
 
        /// <summary>
        /// Initialize the ProtoLobby integration by finding the GameAuthority instance
        /// </summary>
        // Network Manager Integration
        private object? _networkManagerInstance;
        private MethodInfo? _startSinglePlayerP2PMethod;
        private MethodInfo? _startSinglePlayerMethod;
        private MethodInfo? _finishStartHostMethod;
        private MethodInfo? _startHostClientMethod;
        
        // Network Client Integration
        private Type? _networkClientType;
        private MethodInfo? _connectMethod;
        private MethodInfo? _connectLocalServerMethod;
        private MethodInfo? _disconnectMethod;
        private MethodInfo? _networkClientReadyMethod;
        private MethodInfo? _networkClientAddPlayerMethod;

        
        // SteamP2PNetworkTester Integration
        private MethodInfo? _validatePlayersReadyMethod;
        private MethodInfo? _stopClientMethod;
        private MethodInfo? _stopServerMethod;
        private MethodInfo? _stopHostMethod;

        /// <summary>
        /// Initialize the ProtoLobby integration
        /// </summary>
        public bool Initialize()
        {
            if (_isInitialized) return true;
            
            // MelonLogger.Msg("[ProtoLobby] Integration initialized");
            try
            {
                // 1. Find GameAuthority (Keep for Captain logic)
                var assemblyCSharp = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                
                if (assemblyCSharp == null)
                {
                    MelonLogger.Warning("[ProtoLobby] Assembly-CSharp not found");
                    return false;
                }

                _gameAuthorityType = assemblyCSharp.GetType("Il2CppWartide.GameAuthority");
                
                if (_gameAuthorityType != null)
                {
                    // Cache NativeFieldInfoPtrs for raw IL2CPP access via reflection to avoid compile-time dependency
                    _nativeFieldPerfStats = _gameAuthorityType.GetField("NativeFieldInfoPtr__performanceStatistics", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    _nativeFieldPerfTracker = _gameAuthorityType.GetField("NativeFieldInfoPtr__performanceTracker", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    _nativeFieldHasStats = _gameAuthorityType.GetField("NativeFieldInfoPtr__hasNetworkStatistics", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    _nativeFieldNetManager = _gameAuthorityType.GetField("NativeFieldInfoPtr__networkManager", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    
                    // Also cache the NativeClassPtr for diagnostics
                    _nativeClassPtrField = typeof(Il2CppClassPointerStore<>).MakeGenericType(_gameAuthorityType).GetField("NativeClassPtr", BindingFlags.Static | BindingFlags.Public);

                    var instanceProp = _gameAuthorityType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (instanceProp != null) _gameAuthority = instanceProp.GetValue(null);
                    
                    if (_gameAuthority != null)
                    {
                        // MelonLogger.Msg("[ProtoLobby] Found GameAuthority instance and IL2CPP field pointers.");
                        _captainsListProp = _gameAuthorityType.GetProperty("_protoLobbyDropdownAvailableCaptains", BindingFlags.Public | BindingFlags.Instance);
                        _selectedIndexProp = _gameAuthorityType.GetProperty("_protoLobbyCaptainSelectedIndex", BindingFlags.Public | BindingFlags.Instance);
                        _selectedCaptainProp = _gameAuthorityType.GetProperty("_protoLobbyClientSelectedCaptain", BindingFlags.Public | BindingFlags.Instance);
                        _userNameProp = _gameAuthorityType.GetProperty("_protoLobbyUserName", BindingFlags.Public | BindingFlags.Instance);
                        _teamSelectionIndexProp = _gameAuthorityType.GetProperty("_lobbyTeamSelectionIndex", BindingFlags.Public | BindingFlags.Instance);
                        _initCaptainSelectionMethod = _gameAuthorityType.GetMethod("InitializeProtoLobbyCaptainSelection", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                        _isSinglePlayerProp = _gameAuthorityType.GetProperty("_isSinglePlayer", BindingFlags.Public | BindingFlags.Instance);
                        _dropdownType = _gameAuthorityType.GetNestedType( "ProtoLobbyCaptainDropdown", BindingFlags.Public | BindingFlags.NonPublic );
                    }
                }

                // Initialize SteamP2PNetworkTester Reflection
                if (_testerType == null)
                {
                    _testerType = assemblyCSharp.GetType("Il2CppWartide.Testing.SteamP2PNetworkTester");
                    if (_testerType != null)
                    {
                        _cachedInfoField = _testerType.GetField("cachedConnectionInfo", BindingFlags.NonPublic | BindingFlags.Instance);
                        
                        // Nested types are often found via GetNestedType or just GetType with +
                        var infoType = _testerType.GetNestedType("ConnectionStatusInfo", BindingFlags.Public) ?? assemblyCSharp.GetType("Il2CppWartide.Testing.SteamP2PNetworkTester+ConnectionStatusInfo");
                        if (infoType != null)
                        {
                            _playersListProp = infoType.GetProperty("Players", BindingFlags.Public | BindingFlags.Instance);
                        }

                        _playerStatusInfoType = _testerType.GetNestedType("PlayerStatusInfo", BindingFlags.Public) ?? assemblyCSharp.GetType("Il2CppWartide.Testing.SteamP2PNetworkTester+PlayerStatusInfo");
                        if (_playerStatusInfoType != null)
                        {
                            _psIsReadyProp = _playerStatusInfoType.GetProperty("IsReady", BindingFlags.Public | BindingFlags.Instance);
                            _psIsTeamAProp = _playerStatusInfoType.GetProperty("IsTeamA", BindingFlags.Public | BindingFlags.Instance);
                            _psIsConnectedProp = _playerStatusInfoType.GetProperty("IsConnected", BindingFlags.Public | BindingFlags.Instance);
                        }
                    }
                }

                // Try to find instance
                if (_testerInstance == null)
                {
                    // FindObjectOfType via Unity Object (Requires Il2CppSystem.Type for call)
                    var il2cppType = Il2CppSystem.Type.GetType("Il2CppWartide.Testing.SteamP2PNetworkTester");
                    if (il2cppType != null)
                    {
                        var objects = UnityEngine.Object.FindObjectsOfType(il2cppType);
                        if (objects != null && objects.Length > 0)
                        {
                            _testerInstance = objects[0];
                            MelonLogger.Msg("[ProtoLobby] Found SteamP2PNetworkTester instance.");
                        }
                    }
                }

                // 2. Find WartideNetworkManager
                var managerType = assemblyCSharp.GetType("Il2CppWartide.WartideNetworkManager");
                if (managerType != null)
                {
                    // Try getting static Instance
                    var instanceProp = managerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (instanceProp != null)
                    {
                        _networkManagerInstance = instanceProp.GetValue(null);
                    }

                    // Fallback to FindObjectOfType if Instance is null
                    if (_networkManagerInstance == null)
                    {
                         var il2cppType = Il2CppSystem.Type.GetType("Il2CppWartide.WartideNetworkManager");
                         if (il2cppType != null)
                         {
                             var managers = UnityEngine.Object.FindObjectsOfType(il2cppType);
                             if (managers != null && managers.Length > 0) _networkManagerInstance = managers[0];
                             
                             // Fallback to Instance
                             if (_networkManagerInstance == null)
                             {
                                 var staticInstProp = managerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                                 if (staticInstProp != null) _networkManagerInstance = staticInstProp.GetValue(null);
                             }
                         }
                    }

                    if (_networkManagerInstance != null)
                    {
                        // MelonLogger.Msg("[ProtoLobby] Found WartideNetworkManager instance");
                        
                        // Cache StartSinglePlayerWithSteamP2P
                        _startSinglePlayerP2PMethod = managerType.GetMethod("StartSinglePlayerWithSteamP2P", BindingFlags.Public | BindingFlags.Instance);
                        _startSinglePlayerMethod = managerType.GetMethod("StartSinglePlayer", BindingFlags.Public | BindingFlags.Instance);
                        _finishStartHostMethod = managerType.GetMethod("FinishStartHost", BindingFlags.Public | BindingFlags.Instance);
                        _startHostClientMethod = managerType.GetMethod("StartHostClient", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                        _stopClientMethod = managerType.GetMethod("StopClient", BindingFlags.Public | BindingFlags.Instance);
                        _stopServerMethod = managerType.GetMethod("StopServer", BindingFlags.Public | BindingFlags.Instance);
                        _stopHostMethod = managerType.GetMethod("StopHost", BindingFlags.Public | BindingFlags.Instance);
                        
                        if (_startSinglePlayerMethod == null) MelonLogger.Warning("[ProtoLobby] StartSinglePlayer not found!");
                        // else MelonLogger.Msg("[ProtoLobby] Found StartSinglePlayer method");

                        // if (_stopClientMethod != null) MelonLogger.Msg("[ProtoLobby] Found StopClient method");
                    }
                    else
                    {
                         MelonLogger.Warning("[ProtoLobby] WartideNetworkManager instance not found (tried Instance prop too)");
                    }
                }
                else
                {
                    MelonLogger.Warning("[ProtoLobby] WartideNetworkManager type not found");
                }
                
                // 3. Find NetworkClient for client connections
                // 3. Find NetworkClient for client connections
                // Search all loaded assemblies for Mirror.NetworkClient or Il2CppMirror.NetworkClient
                _networkClientType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => SafeGetTypes(a))
                    .FirstOrDefault(t => t.FullName == "Mirror.NetworkClient" || t.FullName == "Il2CppMirror.NetworkClient");
                
                if (_networkClientType != null)
                {
                    // Use GetMethods().FirstOrDefault to avoid AmbiguousMatchException
                    var methods = _networkClientType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                    
                    // Connect(string) or similar
                    _connectMethod = methods.FirstOrDefault(m => m.Name == "Connect" && m.GetParameters().Length >= 1);
                    
                    // ConnectLocalServer()
                    _connectLocalServerMethod = methods.FirstOrDefault(m => m.Name == "ConnectLocalServer" && m.GetParameters().Length == 0);

                    // Disconnect()
                    _disconnectMethod = methods.FirstOrDefault(m => m.Name == "Disconnect" && m.GetParameters().Length == 0);
                    
                    // Ready() - usually no args
                    _networkClientReadyMethod = methods.FirstOrDefault(m => m.Name == "Ready" && m.GetParameters().Length == 0);
                    
                    // AddPlayer with 5 args (custom Wartide overload?)
                    _networkClientAddPlayerMethod = methods.FirstOrDefault(m => m.Name == "AddPlayer" && m.GetParameters().Length == 5);
                    
                    // Fallback for AddPlayer if 5-arg version not found (e.g. standard Mirror 0-arg)
                    if (_networkClientAddPlayerMethod == null)
                    {
                        MelonLogger.Warning("[ProtoLobby] 5-param AddPlayer not found. Using standard 0-param AddPlayer if available.");
                        _networkClientAddPlayerMethod = methods.FirstOrDefault(m => m.Name == "AddPlayer" && m.GetParameters().Length == 0);
                    }

                    if (_connectMethod != null) MelonLogger.Msg($"[ProtoLobby] Found NetworkClient.Connect ({_connectMethod.GetParameters().Length} params)");
                    if (_networkClientReadyMethod != null) MelonLogger.Msg("[ProtoLobby] Found NetworkClient.Ready");
                    if (_networkClientAddPlayerMethod != null) MelonLogger.Msg($"[ProtoLobby] Found NetworkClient.AddPlayer ({_networkClientAddPlayerMethod.GetParameters().Length} params)");
                }
                else
                {
                     MelonLogger.Error("[ProtoLobby] NetworkClient type not found in any assembly!");
                }

                // 3b. Find NetworkServer for host checks
                _networkServerType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => SafeGetTypes(a))
                    .FirstOrDefault(t => t.FullName == "Mirror.NetworkServer" || t.FullName == "Il2CppMirror.NetworkServer");
                
                if (_networkServerType != null) MelonLogger.Msg("[ProtoLobby] Found NetworkServer type");

                // Find SteamP2PNetworkTester for validation - Robust search
                var existingTesterType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => SafeGetTypes(a))
                    .FirstOrDefault(t => t.FullName == "Il2CppWartide.Testing.SteamP2PNetworkTester");

                if (existingTesterType != null)
                {
                    _validatePlayersReadyMethod = existingTesterType.GetMethod("ValidatePlayersReadyForGameStart",
                        BindingFlags.Public | BindingFlags.Instance);
                    
                    if (_validatePlayersReadyMethod != null)
                    {
                        MelonLogger.Msg("[ProtoLobby] Found ValidatePlayersReadyForGameStart method");
                    }
                }
                else
                {
                     MelonLogger.Warning("[ProtoLobby] SteamP2PNetworkTester (Il2CppWartide.Testing.SteamP2PNetworkTester) type not found!");
                }
                // 4. Keep SteamP2PNetworkTester logic for Client Complete? Or just remove it?
                // User focus is on Server+Client setup. We'll leave the complete method stubbed or try to find it on Manager?
                // Manager doesn't seem to have "CompleteProtoLobby". That might be GameAuthority.
                // Re-enable GameAuthority methods for Client Complete if needed.
                 _completeProtoLobbyMethod = _gameAuthorityType?.GetMethod("CompleteProtoLobbyServer", BindingFlags.Public | BindingFlags.Instance);
                 _completeProtoLobbyClientMethod = _gameAuthorityType?.GetMethod("CompleteProtoLobbyClient", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                
                _isInitialized = true;
                MelonLogger.Msg("[ProtoLobby] Integration initialized");
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ProtoLobby] Failed to initialize: {ex.Message}");
                return false;
            }
        }
        
        public void TriggerSinglePlayerP2P()
        {
            if (_networkManagerInstance != null && _startSinglePlayerP2PMethod != null)
            {
                try
                {
                    MelonLogger.Msg("[ProtoLobby] Invoking StartSinglePlayerWithSteamP2P (Server+Client)...");
                    _startSinglePlayerP2PMethod.Invoke(_networkManagerInstance, null);
                }
                catch(Exception ex)
                {
                    MelonLogger.Error($"[ProtoLobby] Failed to trigger StartSinglePlayerWithSteamP2P: {ex.Message}");
                }
            }

            else
            {
                MelonLogger.Error("[ProtoLobby] Cannot trigger SinglePlayer - NetworkManager or Method not found");
            }
        }

        public void ShutdownNetwork(bool isHost)
        {
            if (_networkManagerInstance == null) return;
            try
            {
               if (isHost && _stopHostMethod != null) 
               {
                   MelonLogger.Msg("[ProtoLobby] Stopping Host...");
                   _stopHostMethod.Invoke(_networkManagerInstance, null);
               }
               else if (!isHost && _stopClientMethod != null)
               {
                   MelonLogger.Msg("[ProtoLobby] Stopping Client...");
                   _stopClientMethod.Invoke(_networkManagerInstance, null);
               }
               else if (_stopServerMethod != null && isHost)
               {
                    MelonLogger.Msg("[ProtoLobby] Stopping Server (Fallback)...");
                   _stopServerMethod.Invoke(_networkManagerInstance, null);
               }
            }
            catch(Exception ex) { MelonLogger.Error($"[ProtoLobby] Shutdown failed: {ex.Message}"); }
        }

        public void TriggerSinglePlayer()
        {
            if (_networkManagerInstance != null && _startSinglePlayerMethod != null)
            {
                try
                {
                    
                    if (_isSinglePlayerProp == null){MelonLogger.Error("[ProtoLobby] IsSinglePlayer property not found");return;}
                    // MelonLogger.Msg("[ProtoLobby] Setting IsSinglePlayer to true...");
                    _isSinglePlayerProp.SetValue(_gameAuthority, true);

                    // MelonLogger.Msg("[ProtoLobby] Invoking StartSinglePlayer (Server)...");
                    _startSinglePlayerMethod.Invoke(_networkManagerInstance, null);
                }
                catch(Exception ex)
                {
                    MelonLogger.Error($"[ProtoLobby] Failed to trigger StartSinglePlayer: {ex.Message}");
                }
            }
            else
            {
                MelonLogger.Error("[ProtoLobby] Cannot trigger SinglePlayer - NetworkManager or Method not found");
            }
        }

        public void StartHostClient()
        {
            if (_networkManagerInstance != null && _startHostClientMethod != null)
            {
                try
                {
                    MelonLogger.Msg("[ProtoLobby] Invoking StartHostClient (Server)...");
                    _startHostClientMethod.Invoke(_networkManagerInstance, null);
                }
                catch(Exception ex)
                {
                    MelonLogger.Error($"[ProtoLobby] Failed to trigger StartHostClient: {ex.Message}");
                }
            }
            else
            {
                MelonLogger.Error("[ProtoLobby] Cannot trigger StartHostClient - NetworkManager or Method not found");
            }
        }

        public void CompleteProtoLobbyClient()
        {
            if (_gameAuthority != null && _completeProtoLobbyClientMethod != null) 
            {
                try
                {
                    MelonLogger.Msg("[ProtoLobby] Invoking CompleteProtoLobbyClient...");
                    _completeProtoLobbyClientMethod.Invoke(_gameAuthority, null);
                }
                catch(Exception ex)
                {
                    MelonLogger.Error($"[ProtoLobby] Failed to complete client: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Connect client to the game server via Steam P2P
        /// This initiates the P2P session request to the host
        /// </summary>
        public void ConnectToGameServer(string? address = null)
        {
            if (_networkClientType == null)
            {
                MelonLogger.Error("[ProtoLobby] NetworkClient type not found! Cannot connect.");
                return;
            }
            
            try
            {
                if (string.IsNullOrEmpty(address))
                {
                    // For Host: Disconnect client first if active, but keep server alive
                    if (IsConnected)
                    {
                        MelonLogger.Msg("[ProtoLobby] Local client already connected. Disconnecting before re-connect...");
                        DisconnectNetworkClient();
                    }

                    // If server is not active, trigger it? 
                    if (!IsServerActive)
                    {
                        MelonLogger.Warning("[ProtoLobby] Server is NOT active. Creating lobby usually triggers this.");
                    }

                    // Connect to Local Server
                    if (_connectLocalServerMethod != null)
                    {
                        MelonLogger.Msg("[ProtoLobby] Connecting to Local Server...");
                        _connectLocalServerMethod.Invoke(null, null);
                        MelonLogger.Msg("[ProtoLobby] Local connection initiated.");
                    }
                    else
                    {
                        MelonLogger.Warning("[ProtoLobby] ConnectLocalServer method not found.");
                    }
                }
                else
                {
                    // Connect to remote address
                    if (_connectMethod != null && _connectMethod.GetParameters().Length >= 1)
                    {
                        MelonLogger.Msg($"[ProtoLobby] Connecting to game server at {address}...");
                        _connectMethod.Invoke(null, new object[] { address });
                    }
                }
            }
            catch (Exception ex)
            {
                var realEx = ex.InnerException ?? ex;
                MelonLogger.Error($"[ProtoLobby] Connection Failed: {realEx.Message}");
                MelonLogger.Error($"[ProtoLobby] Stack: {realEx.StackTrace}");
                if (realEx.InnerException != null) 
                    MelonLogger.Error($"[ProtoLobby] Inner Inner: {realEx.InnerException.Message}");
            }
        }

        private bool _captainsInitialized = false;

         /// <summary>
        /// Initialize the captain selection dropdown
        /// </summary>
        public void InitializeCaptainSelection()
        {
            if (!_isInitialized)
            {
                MelonLogger.Warning("[ProtoLobby] Not initialized, cannot initialize captain selection");
                return;
            }

            if (_captainsInitialized)
            {
                // Already initialized, do not run again to avoid duplicate captains
                return;
            }

            try
            {
                if (_initCaptainSelectionMethod != null)
                {
                    _initCaptainSelectionMethod.Invoke(_gameAuthority, null);
                    _captainsInitialized = true;
                    // MelonLogger.Msg("[ProtoLobby] Captain selection initialized");
                }
                else
                {
                    MelonLogger.Error("[ProtoLobby] Could not find InitializeProtoLobbyCaptainSelection method");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ProtoLobby] Failed to initialize captain selection: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the number of available captains
        /// </summary>
        public int GetCaptainCount()
        {
            if (!_isInitialized || _captainsListProp == null) return 0;

            try
            {
                var captainsList = _captainsListProp.GetValue(_gameAuthority);
                if (captainsList == null) return 0;

                // Get Count property from IL2CPP List
                var countProp = captainsList.GetType().GetProperty("Count");
                if (countProp == null) return 0;

                return (int)(countProp.GetValue(captainsList) ?? 0);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ProtoLobby] Failed to get captain count: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Set the selected captain by index
        /// </summary>
        public void SetSelectedCaptain(int captainIndex)
        {
            if (!_isInitialized || _captainsListProp == null) return;

            try
            {
                var captainsList = _captainsListProp.GetValue(_gameAuthority);
                if (captainsList == null)
                {
                    MelonLogger.Warning("[ProtoLobby] Captains list is null");
                    return;
                }

                // Get count
                var countProp = captainsList.GetType().GetProperty("Count");
                int count = (int)(countProp?.GetValue(captainsList) ?? 0);

                if (captainIndex < 0 || captainIndex >= count)
                {
                    MelonLogger.Warning($"[ProtoLobby] Invalid captain index: {captainIndex} (count: {count})");
                    return;
                }

                // Get the captain at index
                var itemProp = captainsList.GetType().GetProperty("Item");
                var captain = itemProp?.GetValue(captainsList, new object[] { captainIndex });

                // Set selected index
                _selectedIndexProp?.SetValue(_gameAuthority, captainIndex);

                // Set selected captain
                _selectedCaptainProp?.SetValue(_gameAuthority, captain);

                // MelonLogger.Msg($"[ProtoLobby] Selected captain at index {captainIndex}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ProtoLobby] Failed to set selected captain: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the currently selected captain index
        /// </summary>
        public int GetSelectedCaptainIndex()
        {
            if (!_isInitialized || _selectedIndexProp == null) return -1;

            try
            {
                return (int)(_selectedIndexProp?.GetValue(_gameAuthority) ?? -1);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ProtoLobby] Failed to get selected captain index: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// Set the username for the proto lobby
        /// </summary>
        public void SetUserName(string userName)
        {
            if (!_isInitialized || _userNameProp == null) return;
            
            try
            {
                _userNameProp?.SetValue(_gameAuthority, userName);
                // MelonLogger.Msg($"[ProtoLobby] Set username to: {userName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ProtoLobby] Failed to set username: {ex.Message}");
            }
        }

        /// <summary>
        /// Finish starting the host (vanilla does this when host clicks Ready)
        /// </summary>
        public void FinishStartHost()
        {
            if (!_isInitialized)
            {
                MelonLogger.Error("[ProtoLobby] Cannot finish host start - not initialized!");
                return;
            }

            if (_finishStartHostMethod == null)
            {
                MelonLogger.Error("[ProtoLobby] Cannot finish host start - FinishStartHost method not found!");
                return;
            }

            if (_networkManagerInstance == null)
            {
                MelonLogger.Error("[ProtoLobby] Cannot finish host start - WartideNetworkManager instance is null!");
                return;
            }

            try
            {
                // MelonLogger.Msg("[ProtoLobby] Invoking FinishStartHost on WartideNetworkManager...");
                _finishStartHostMethod.Invoke(_networkManagerInstance, null);
                // MelonLogger.Msg("[ProtoLobby] Host startup completed successfully");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ProtoLobby] Failed to finish host start: {ex.Message}");
                MelonLogger.Error($"[ProtoLobby] Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    MelonLogger.Error($"[ProtoLobby] Inner exception: {ex.InnerException.Message}");
                }
            }
        }


        /// <summary>
        /// Complete the proto lobby server setup
        /// </summary>
        public void CompleteProtoLobbyServer(System.Action? onGameStarting = null)
        {
            if (!_isInitialized)
            {
                MelonLogger.Error("[ProtoLobby] Cannot complete - not initialized!");
                return;
            }
            
            if (_completeProtoLobbyMethod == null)
            {
                MelonLogger.Error("[ProtoLobby] Cannot complete - CompleteProtoLobbyServer method not found!");
                return;
            }
            
            if (_gameAuthority == null)
            {
                MelonLogger.Error("[ProtoLobby] Cannot complete - GameAuthority instance is null!");
                return;
            }

            try
            {
                // DEBUG: Inspect fields before crash
                InspectGameAuthorityFields(_gameAuthority as Il2CppObjectBase);

                MelonLogger.Msg("[ProtoLobby] Invoking CompleteProtoLobbyServer on GameAuthority...");
                _completeProtoLobbyMethod?.Invoke(_gameAuthority, null);
                MelonLogger.Msg("[ProtoLobby] Proto lobby server completed successfully");
                
                // Notify callback that game is starting
                onGameStarting?.Invoke();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ProtoLobby] Failed to complete proto lobby server: {ex.Message}");
                MelonLogger.Error($"[ProtoLobby] Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    MelonLogger.Error($"[ProtoLobby] Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        /// <summary>
        /// Get captain name by index (OPTIMIZED with caching)
        /// </summary>
        public string GetCaptainName(int index)
        {
            if (!_isInitialized || _captainsListProp == null) return "Unknown";

            try
            {
                var captainsList = _captainsListProp.GetValue(_gameAuthority);
                if (captainsList == null) return $"Captain {index + 1}";

                // Get count
                var countProp = captainsList.GetType().GetProperty("Count");
                int count = (int)(countProp?.GetValue(captainsList) ?? 0);

                if (index < 0 || index >= count) return "Unknown";

                // Get the captain at index
                var itemProp = captainsList.GetType().GetProperty("Item");
                var captain = itemProp?.GetValue(captainsList, new object[] { index });

                if (captain == null) return $"Captain {index + 1}";

                // ✅ PERFORMANCE: Try cached property first
                if (_captainNameProp != null)
                {
                    var nameValue = _captainNameProp.GetValue(captain);
                    if (nameValue != null && !string.IsNullOrEmpty(nameValue.ToString()))
                    {
                        return nameValue?.ToString() ?? "Unknown";
                    }
                }

                // ✅ PERFORMANCE: Negative caching - stop searching if we already failed
                if (_captainNameSearchFailed) return $"Captain {index + 1}";

                // First time: Find and cache the name property
                var captainType = captain.GetType();
                foreach (var propName in new[] { "name", "Name", "displayName", "DisplayName", "captainName", "CaptainName" })
                {
                    var nameProp = captainType.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                    if (nameProp != null)
                    {
                        var nameValue = nameProp.GetValue(captain);
                        if (nameValue != null && !string.IsNullOrEmpty(nameValue.ToString()))
                        {
                            _captainNameProp = nameProp;  // ✅ CACHE IT!
                            return nameValue?.ToString() ?? "Unknown";
                        }
                    }
                }

                // Fallback to index-based name if list search fails
                // First: Expand search list (using existing captainType variable)
                foreach (var propName in new[] { "labelCaptainName", "name", "Name", "displayName", "DisplayName", "captainName", "CaptainName", "Title", "title", "Header", "header", "LocalizedName", "localizedName" })
                {
                    var nameProp = captainType.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                    if (nameProp != null)
                    {
                        var nameValue = nameProp.GetValue(captain);
                        if (nameValue != null && !string.IsNullOrEmpty(nameValue.ToString()))
                        {
                            _captainNameProp = nameProp;  // ✅ CACHE IT!
                            // MelonLogger.Msg($"[ProtoLobby] Found captain name property: {propName}");
                            return nameValue?.ToString() ?? "Unknown";
                        }
                    }
                }
                
                // Diagnostic: If we get here service hasn't found the name. Log properties ONCE.
                if (!_hasLoggedCaptainDebug)
                {
                    _hasLoggedCaptainDebug = true;
                    _captainNameSearchFailed = true; // ✅ Mark as failed so we don't try again
                    
                    MelonLogger.Warning($"[ProtoLobby] Could not find name property for type {captainType.Name}. Available properties:");
                    foreach(var p in captainType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                         MelonLogger.Warning($" - {p.Name} ({p.PropertyType.Name})");
                    }
                     foreach(var f in captainType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                         MelonLogger.Warning($" - [Field] {f.Name} ({f.FieldType.Name})");
                    }
                }

                // Fallback to index-based name
                return $"Captain {index + 1}";
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ProtoLobby] Failed to get captain name: {ex.Message}");
                return $"Captain {index + 1}";
            }
        }

        /// <summary>
        /// Check if the integration is ready
        /// </summary>
        public bool IsReady => _isInitialized && _gameAuthority != null;

        public void SetSelectedTeam(int teamIndex)
        {
            if (!_isInitialized || _teamSelectionIndexProp == null) return;

            try
            {
                // UI uses 1 (A) and 2 (B). Game dropdown uses 0 (A) and 1 (B).
                int gameTeamIndex = teamIndex - 1;
                if (gameTeamIndex < 0) gameTeamIndex = 0;

                _teamSelectionIndexProp?.SetValue(_gameAuthority, gameTeamIndex);
                
                // ALSO update the deep sync for P2P Tester (Team 0 = Team A, see GameAuthority logic)
                UpdateLocalPlayerStatus(isTeamA: gameTeamIndex == 0);

                MelonLogger.Msg($"[ProtoLobby] Set team selection index to {gameTeamIndex} (from UI {teamIndex})");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ProtoLobby] Failed to set team selection: {ex.Message}");
            }
        }

        public int GetSelectedTeamIndex()
        {
            if (!_isInitialized || _teamSelectionIndexProp == null) return -1;

            try
            {
                return (int)(_teamSelectionIndexProp.GetValue(_gameAuthority) ?? -1);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ProtoLobby] Failed to get team selection index: {ex.Message}");
                return -1;
            }
        }

        public void SetReady(bool isReady)
        {
            MelonLogger.Msg($"[ProtoLobby] SetReady called with: {isReady}");
            UpdateLocalPlayerStatus(isReady: isReady);
        }

        public void DisconnectNetworkClient()
        {
            try
            {
                // Prefer WartideNetworkManager.StopClient() if available as it handles Wartide-specific state
                if (_networkManagerInstance != null && _stopClientMethod != null)
                {
                    MelonLogger.Msg("[ProtoLobby] Calling WartideNetworkManager.StopClient()...");
                    _stopClientMethod.Invoke(_networkManagerInstance, null);
                    return;
                }

                // Fallback to Mirror Disconnect
                if (_networkClientType != null && _disconnectMethod != null) 
                {
                    MelonLogger.Msg("[ProtoLobby] Calling NetworkClient.Disconnect()...");
                    _disconnectMethod.Invoke(null, null);
                }
            }
            catch (Exception ex)
            {
                var realEx = ex.InnerException ?? ex;
                MelonLogger.Error($"[ProtoLobby] Failed to Disconnect: {realEx.Message}");
            }
        }
        
        public object? CreateCaptainDropdown(string name, object typeId)
        {
            if (_gameAuthorityType == null)
            {
                MelonLogger.Error("[ProtoLobby] GameAuthority type not initialized");
                return null;
            }

            // 1. Get nested type
            var _dropdownType = _gameAuthorityType.GetNestedType(
                "ProtoLobbyCaptainDropdown",
                BindingFlags.Public | BindingFlags.NonPublic
            );

            if (_dropdownType == null)
            {
                MelonLogger.Error("[ProtoLobby] Could not find nested type ProtoLobbyCaptainDropdown");
                return null;
            }

            // 2. Get constructor
            var ctor = _dropdownType.GetConstructor(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                Type.EmptyTypes,
                null
            );

            if (ctor == null)
            {
                MelonLogger.Error("[ProtoLobby] Could not find ProtoLobbyCaptainDropdown constructor");
                return null;
            }

            // 3. Instantiate
            var instance = ctor.Invoke(null);

            // 4. Set fields
            _dropdownType.GetProperty("labelCaptainName")?.SetValue(instance, name);
            _dropdownType.GetProperty("captainTypeID")?.SetValue(instance, typeId);

            return instance;
        }




        /// <summary>
        /// Call NetworkClient.Ready() and AddPlayer() - critical for game start
        /// </summary>
        public void CallNetworkClientReady(int captainIndex, int teamIndex)
        {
            if (_networkClientReadyMethod == null || _networkClientAddPlayerMethod == null)
            {
                MelonLogger.Error("[ProtoLobby] NetworkClient methods not found!");
                return;
            }
            
            try
            {
                // Call NetworkClient.Ready()
                // MelonLogger.Msg("[ProtoLobby] Calling NetworkClient.Ready()...");
                _networkClientReadyMethod.Invoke(null, null);
                
                // Call NetworkClient.AddPlayer() with captain and team info
                // Check how many parameters the found AddPlayer method wants
                int paramCount = _networkClientAddPlayerMethod.GetParameters().Length;
                
                if (paramCount == 5)
                {
                    // MelonLogger.Msg($"[ProtoLobby] Calling NetworkClient.AddPlayer(team={teamIndex}, captain={captainIndex})...");
                    
                    // Parameters: bool isTeamA, int captainIndex, uint playerId, string playerName, byte[]? authTicket
                    // UI uses teamIndex 1 for Team A, 2 for Team B.
                    bool isTeamA = (teamIndex == 1);
                    
                    // Get player ID and name from Steam
                    var steamId = Steamworks.SteamUser.GetSteamID();
                    // NOTE: Wartide's AddPlayer signature expects a uint playerId.
                    // This 32-bit truncation is a game engine requirement, not a mod bug.
                    // It means collisions are theoretically possible, but we must ignore the upper 32 bits to match the native signature.
                    uint playerId = (uint)steamId.m_SteamID;
                    string playerName = Steamworks.SteamFriends.GetPersonaName() ?? "Unknown";
                    
                    // CRITICAL: Game uses 1-based captain indices (0 = unassigned)
                    // UI uses 0-based, so add 1 when passing to game
                    int gameCaptainIndex = captainIndex + 1;
                    
                    // Use default(object) to be explicit about the null parameter to suppress warning
                    object?[] parameters = new object?[] { isTeamA, gameCaptainIndex, playerId, playerName, null };
                    _networkClientAddPlayerMethod.Invoke(null, parameters);
                }
                else if (paramCount == 0)
                {
                    MelonLogger.Msg("[ProtoLobby] Calling standard NetworkClient.AddPlayer() (0 params)...");
                    _networkClientAddPlayerMethod.Invoke(null, null);
                }
                else
                {
                    MelonLogger.Warning($"[ProtoLobby] Unsupported AddPlayer signature with {paramCount} params. Attempting 0-param invoke...");
                    _networkClientAddPlayerMethod.Invoke(null, null);
                }
                
                MelonLogger.Msg("[ProtoLobby] NetworkClient.Ready() and AddPlayer() completed");
            }
            catch (Exception ex)
            {
                var realEx = ex.InnerException ?? ex;
                MelonLogger.Error($"[ProtoLobby] Failed to call NetworkClient methods: {realEx.Message}");
                MelonLogger.Error($"[ProtoLobby] Stack trace: {realEx.StackTrace}");
            }
        }
        
        /// <summary>
        /// Validate that all players are ready for game start
        /// </summary>
        public bool ValidatePlayersReadyForGameStart()
        {
            // 1. Ensure we have a tester instance
            if (_testerInstance == null)
            {
                // Find the managed type
                var testerManagedType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => SafeGetTypes(a))
                    .FirstOrDefault(t => t.FullName == "Il2CppWartide.Testing.SteamP2PNetworkTester");

                if (testerManagedType == null)
                {
                    MelonLogger.Error("[ProtoLobby] SteamP2PNetworkTester type not found");
                    return false;
                }

                // Convert to Il2CppSystem.Type
                var il2cppType = Il2CppInterop.Runtime.Il2CppType.From(testerManagedType);

                // Find the IL2CPP instance
                var objects = UnityEngine.Object.FindObjectsOfType(il2cppType);
                if (objects == null || objects.Length == 0)
                {
                    MelonLogger.Error("[ProtoLobby] SteamP2PNetworkTester instance not found");
                    return false;
                }

                _testerInstance = objects[0];
                MelonLogger.Msg("[ProtoLobby] Found SteamP2PNetworkTester instance");
            }

            // 2. Get the method from the *instance's IL2CPP type*
            var testerType = _testerInstance.GetType();
            var validateMethod = testerType.GetMethod(
                "ValidatePlayersReadyForGameStart",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public
            );

            if (validateMethod == null)
            {
                MelonLogger.Error("[ProtoLobby] ValidatePlayersReadyForGameStart not found on tester instance");
                return false;
            }

            // 3. Invoke it
            MelonLogger.Msg("[ProtoLobby] Calling ValidatePlayersReadyForGameStart()...");
            var result = validateMethod.Invoke(_testerInstance, null);

            bool ok = result is bool b && b;

            if (ok)
                MelonLogger.Msg("[ProtoLobby] ✓ ValidatePlayersReadyForGameStart succeeded");
            else
                MelonLogger.Warning("[ProtoLobby] ✗ ValidatePlayersReadyForGameStart returned false");

            return ok;
        }


        private void UpdateLocalPlayerStatus(bool? isReady = null, bool? isTeamA = null)
        {
            if (_testerInstance == null || _cachedInfoField == null || _playersListProp == null)
            {
                // Try to find instance again if we missed it
                var il2cppType = Il2CppSystem.Type.GetType("Il2CppWartide.Testing.SteamP2PNetworkTester");
                if (il2cppType != null)
                {
                     var objects = UnityEngine.Object.FindObjectsOfType(il2cppType);
                     if (objects != null && objects.Length > 0) _testerInstance = objects[0];
                }
                if (_testerInstance == null) return;
            }

            try
            {
                var infoObj = _cachedInfoField?.GetValue(_testerInstance);
                if (infoObj == null) return;

                var playersList = _playersListProp?.GetValue(infoObj) as System.Collections.IEnumerable;
                if (playersList == null) return;

                // Assume first player is local for now, or all connected local players
                foreach (var playerObj in playersList)
                {
                    if (isReady.HasValue && _psIsReadyProp != null)
                        _psIsReadyProp.SetValue(playerObj, isReady.Value);
                    
                    if (isTeamA.HasValue && _psIsTeamAProp != null)
                         _psIsTeamAProp.SetValue(playerObj, isTeamA.Value);
                    
                    // For now, we update ALL players we effectively "own" or the first one. 
                    // In a hacked singleplayer lobby, there's usually only one entry anyway.
                    break; 
                }
                 MelonLogger.Msg($"[ProtoLobby] Updated Player Status: Ready={isReady}, TeamA={isTeamA}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ProtoLobby] Failed to update player status: {ex.Message}");
            }
        }
        /// <summary>
        /// Helper to safely get types from an assembly, ignoring those that fail to load
        /// </summary>
        private static IEnumerable<Type> SafeGetTypes(System.Reflection.Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (System.Reflection.ReflectionTypeLoadException e)
            {
                // Return the types that WERE loaded successfully
                return e.Types.Where(t => t != null).Select(t => t!);
            }
            catch (Exception)
            {
                // If the assembly is totally broken, return empty
                return Enumerable.Empty<Type>();
            }
        }

        private FieldInfo? _nativeFieldPerfStats;
        private FieldInfo? _nativeFieldPerfTracker;
        private FieldInfo? _nativeFieldHasStats;
        private FieldInfo? _nativeFieldNetManager;
        private FieldInfo? _nativeClassPtrField;

        public static void InspectGameAuthorityFields(Il2CppObjectBase? instance)
        {
            MelonLogger.Msg("[ProtoLobby] --- GameAuthority IL2CPP Diagnostics ---");
            if (instance == null)
            {
                MelonLogger.Error("[ProtoLobby] GameAuthority instance is NULL!");
                return;
            }

            try
            {
                IntPtr instancePtr = instance.Pointer;
                // MelonLogger.Msg($"[ProtoLobby] GameAuthority Instance Ptr: {instancePtr:X}");

                // Find the assembly and types via reflection to avoid dependency
                var assemblyCSharp = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                if (assemblyCSharp == null) return;

                var gameAuthorityType = assemblyCSharp.GetType("Il2CppWartide.GameAuthority");
                if (gameAuthorityType == null) return;

                var nativeClassPtrField = typeof(Il2CppClassPointerStore<>).MakeGenericType(gameAuthorityType).GetField("NativeClassPtr", BindingFlags.Static | BindingFlags.Public);
                IntPtr nativeClassPtr = (IntPtr)(nativeClassPtrField?.GetValue(null) ?? IntPtr.Zero);

                if (nativeClassPtr == IntPtr.Zero)
                {
                    MelonLogger.Error("[ProtoLobby] Could not find NativeClassPtr for GameAuthority");
                    return;
                }

                LogField(nativeClassPtr, instancePtr, "_performanceStatistics", "PerformanceStatistics");
                LogField(nativeClassPtr, instancePtr, "_performanceTracker", "PerformanceTracker");
                LogField(nativeClassPtr, instancePtr, "_networkManager", "WartideNetworkManager");
                LogField(nativeClassPtr, instancePtr, "_hasNetworkStatistics", "Boolean");
                
                // MelonLogger.Msg("[ProtoLobby] -----------------------------------------");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ProtoLobby] Error in InspectGameAuthorityFields: {ex.Message}");
            }
        }

        private static unsafe void LogField(IntPtr nativeClassPtr, IntPtr instancePtr, string fieldName, string typeName)
        {
            try
            {
                IntPtr fieldPtr = IL2CPP.GetIl2CppField(nativeClassPtr, fieldName);
                if (fieldPtr == IntPtr.Zero)
                {
                    MelonLogger.Warning($"[ProtoLobby] [Diagnostics] Field {fieldName} not found!");
                    return;
                }

                int offset = (int)IL2CPP.il2cpp_field_get_offset(fieldPtr);
                
                // Read the value at the offset
                if (typeName == "Boolean")
                {
                    bool val = *(bool*)(instancePtr + offset);
                    MelonLogger.Msg($"[ProtoLobby] [Diagnostics] Field: {fieldName} (bool) | Offset: 0x{offset:X} | Value: {val}");
                }
                else
                {
                    IntPtr fieldValPtr = *(IntPtr*)(instancePtr + offset);
                    MelonLogger.Msg($"[ProtoLobby] [Diagnostics] Field: {fieldName} ({typeName}) | Offset: 0x{offset:X} | ValuePtr: 0x{(long)fieldValPtr:X} ({(fieldValPtr == IntPtr.Zero ? "NULL" : "EXISTS")})");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ProtoLobby] [Diagnostics] Error reading field {fieldName}: {ex.Message}");
            }
        }
    }
}
