using MelonLoader;
using UnityEngine;
using SteamLobbyLib;
using SiroccoLobby.Model;
using SiroccoLobby.Controller;
using SiroccoLobby.Services;
using SiroccoLobby.UI;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using Steamworks;

[assembly: MelonInfo(typeof(SiroccoLobby.Plugin), "Sirocco Lobby UI", "1.0.0", "Diyu")]
[assembly: MelonGame("LunchboxEntertainment", "Sirocco")]

namespace SiroccoLobby
{
    public class Plugin : MelonMod, ILobbyEvents
    {
        private SteamLobbyManager? _steamManager;
        private LobbyState? _state;
        private LobbyController? _controller;
        private LobbyUIRoot? _ui;
        private ProtoLobbyIntegration? _protoLobby;
        /* private CaptainSelectionController? _captainController; */
        private SteamReflectionBridge? _reflectionBridge;
        private SteamLobbyServiceWrapper? _serviceWrapper;
        
        private bool _showUI = false;
        private bool _canShowUI = false;

        private bool _isSteamInitialized = false;
        
        // CONFIG: Production Version (No Patching)
        // Set to true only if you need low-level IL2CPP tracing
        private const bool ENABLE_TRACING = false;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Sirocco Lobby UI initializing (Built on SLL)...");
            Debug.Log("[ProtoLobby] Sirocco Lobby UI initializing (Built on SLL)...");
            
            // Apply Harmony patches (Tracing only)
            if (ENABLE_TRACING)
            {
                HarmonyPatches.Apply(HarmonyInstance);
            }
            else
            {
                LoggerInstance.Msg("Production Build: Tracing patches disabled.");
            }
            
            // Initialize services
            _reflectionBridge = new SteamReflectionBridge(LoggerInstance);
            _state = new LobbyState();
            _protoLobby = new ProtoLobbyIntegration();
            
            // Create SteamLobbyManager with this plugin as event handler
            // We use the extension method to create a logger adapter
            _steamManager = new SteamLobbyManager(this, new MelonTraceLogger(LoggerInstance));
            
            try 
            {
                _steamManager.Initialize();
                _isSteamInitialized = true;
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Error($"Failed to initialize SteamLobbyManager: {ex.Message}");
                // We keep _isSteamInitialized false so we don't crash in OnUpdate
            }

            // Create the wrapper that the controllers expect
            _serviceWrapper = new SteamLobbyServiceWrapper(_steamManager, LoggerInstance);

            // Create controller
            _controller = new LobbyController(_state, _serviceWrapper, _protoLobby, LoggerInstance);
            
            // Create UI
            // LobbyRoomView needs ProtoLobbyIntegration
            var browserView = new LobbyBrowserView(_state, _controller, _serviceWrapper, LoggerInstance);
            var roomView = new LobbyRoomView(_state, _controller, _serviceWrapper, _protoLobby, LoggerInstance);
            _ui = new LobbyUIRoot(_state, browserView, roomView);
            
            LoggerInstance.Msg("Sirocco Lobby UI initialized!");
        }

        public override void OnUpdate()
        {
            if (_isSteamInitialized)
            {
                _steamManager?.Tick();
            }
            _controller?.OnUpdate();
            
            // F5 toggle
            if (Input.GetKeyDown(KeyCode.F5))
            {
                // Simple toggle logic
                if (!_canShowUI)
                {
                   // Try to init if game is ready
                   if (_reflectionBridge?.IsGameSteamReady() == true)
                   {
                        _canShowUI = true;
                        _showUI = true;
                        if (_state != null) _state.ShowDebugUI = true;
                        
                        // Initialize ProtoLobby
                        if (_protoLobby?.Initialize() == true)
                        {
                            _protoLobby.InitializeCaptainSelection();
                            
                            // Sync defaults FROM game (Source of Truth)
                            if (_state != null)
                            {
                                // Captain
                                int gameCap = _protoLobby.GetSelectedCaptainIndex();
                                if (gameCap >= 0) _state.SelectedCaptainIndex = gameCap;

                                // Team (Game 0/1 -> UI 1/2)
                                int gameTeam = _protoLobby.GetSelectedTeamIndex();
                                if (gameTeam >= 0) _state.SelectedTeam = gameTeam + 1;
                            }
                            if (_serviceWrapper != null)
                            {
/*                                  _captainController = new CaptainSelectionController(_serviceWrapper, _protoLobby);
                                 if (_captainController.Initialize()) */
                                 
                                     if (_state != null) _state.IsProtoLobbyReady = true;
                                 
                            }
                        }
                   }
                }
                else
                {
                    _showUI = !_showUI;
                    if (_state != null) _state.ShowDebugUI = _showUI;
                    if (_showUI && _isSteamInitialized) 
                    {
                        // Refresh when opening
                        _steamManager?.RequestLobbyList(); 
                    }
                }
            }
        }

        public override void OnApplicationQuit()
        {
            LoggerInstance.Msg("[Shutdown] Application Quitting - Cleaning up lobby...");
            _controller?.LeaveLobby();
        }

        public override void OnGUI()
        {
            if (_showUI && _state?.ShowDebugUI == true)
            {
                _ui?.Draw();
            }
        }

        // --- ILobbyEvents Implementation ---

        public void OnLobbyListReceived(List<LobbyData> lobbies)
        {
            if (_state?.ShowDebugUI == true) LoggerInstance.Msg($"[Events] Lobby list received: {lobbies.Count} lobbies");
            _state?.UpdateLobbyList(lobbies.Select(l => (object)l.Id.Value));
        }

        public void OnLobbyJoined(LobbyId lobbyId)
        {
            if (_state?.ShowDebugUI == true) LoggerInstance.Msg($"[Events] Joined lobby: {lobbyId.Value}");
            _controller?.OnLobbyEntered(lobbyId.Value); 
            // RefreshMembers called via OnUpate/Tick eventually or explicit? 
            // _controller checks state. RefreshMembers updates members list.
            RefreshMembers(lobbyId);
        }

        public void OnLobbyDataUpdated(LobbyId lobbyId)
        {
            if (_state?.CurrentLobby != null)
            {
                ulong current = 0;
                if (_state.CurrentLobby is ulong u) current = u;
                else if (_state.CurrentLobby is CSteamID c) current = c.m_SteamID;
                
                if (current == lobbyId.Value)
                {
                    if (_state?.ShowDebugUI == true) LoggerInstance.Msg($"[Events] Current lobby updated");
                    _controller?.RefreshLobbyData(); // Update name/host info
                    RefreshMembers(lobbyId);
                }
            }
        }

        public void OnLobbyMemberChanged(LobbyId lobbyId, LobbyId memberId, EChatMemberStateChange change)
        {
             if (_state?.CurrentLobby != null)
            {
                 ulong current = 0;
                 if (_state.CurrentLobby is ulong u) current = u;
                 else if (_state.CurrentLobby is CSteamID c) current = c.m_SteamID;

                 if (current == lobbyId.Value)
                 {
                    LoggerInstance.Msg($"[Events] Member {change}");
                    RefreshMembers(lobbyId);
                 }
            }
        }

        private void RefreshMembers(LobbyId lobbyId)
        {
            var members = new List<LobbyMember>();
            CSteamID steamLobbyId = new CSteamID(lobbyId.Value);
            
            int count = SteamMatchmaking.GetNumLobbyMembers(steamLobbyId);
            var ownerId = SteamMatchmaking.GetLobbyOwner(steamLobbyId);

            for (int i = 0; i < count; i++)
            {
                var userId = SteamMatchmaking.GetLobbyMemberByIndex(steamLobbyId, i);
                if (userId == CSteamID.Nil) continue;

                string teamStr = SteamMatchmaking.GetLobbyMemberData(steamLobbyId, userId, "team");
                string captainStr = SteamMatchmaking.GetLobbyMemberData(steamLobbyId, userId, "captain_index");
                string readyStr = SteamMatchmaking.GetLobbyMemberData(steamLobbyId, userId, "is_ready");
                string name = SteamFriends.GetFriendPersonaName(userId);

                int.TryParse(teamStr, out int team);
                if (team == 0) team = 1;
                
                int.TryParse(captainStr, out int captain);
                bool.TryParse(readyStr, out bool isReady);

                members.Add(new LobbyMember
                {
                    SteamId = userId.m_SteamID,
                    Name = name,
                    Team = team,
                    CaptainIndex = captain,
                    IsHost = (userId == ownerId),
                    IsReady = isReady
                });
            }
            
            _state?.UpdateMembers(members);
        }
    }

    public class MelonTraceLogger : ITraceLogger
    {
        private readonly MelonLogger.Instance _logger;
        public MelonTraceLogger(MelonLogger.Instance logger) => _logger = logger;
        public void Log(string category, string message) => _logger.Msg($"[{category}] {message}");
    }
}
