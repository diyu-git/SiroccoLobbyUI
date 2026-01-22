using SiroccoLobby.Model;
using SiroccoLobby.Services;
using MelonLoader;
using UnityEngine;
namespace SiroccoLobby.Controller
{
    using System.Linq;
    using Il2CppInterop.Runtime;
    using Il2CppInterop.Runtime.InteropTypes;
    using SiroccoLobby.Helpers;

    public sealed class LobbyController
    {
        private readonly LobbyState _state;
        private readonly ISteamLobbyService _steam;
        private readonly ProtoLobbyIntegration _protoLobby; // Added
        private readonly MelonLogger.Instance _log;

        public LobbyController(
            LobbyState state,
            ISteamLobbyService steam,
            ProtoLobbyIntegration protoLobby, // Added
            MelonLogger.Instance log)
        {
            _state = state;
            _steam = steam;
            _protoLobby = protoLobby; // Added
            _log = log;
        }

        public void RefreshLobbyList()
        {
            _steam.RequestLobbyList();
             _log.Msg("Lobby refresh requested.");
        }

        public void CreateLobby()
        {
            if (_state.ShowDebugUI) _log.Msg("Creating lobby...");
            
            // 1. Start the game server (Mirror + Steam P2P networking)
            if (_protoLobby != null && _protoLobby.IsReady)
            {
                _protoLobby.TriggerSinglePlayer();
                if (_state.ShowDebugUI) _log.Msg("[Host] Game server started (Server mode via StartSinglePlayer)");
            }
            else
            {
                _log.Error("[Host] ProtoLobby not ready! Cannot start game server.");
            }
            
            // 2. Create Steam lobby for matchmaking/discovery
            _steam.CreateLobby(2, 10);

            _state.IsSearchingForHostedLobby = true;
            if (_state.ShowDebugUI) _log.Msg("Waiting for Lobby Creation...");
        }

        public void JoinLobby(object lobbyId)
        {
            // CRITICAL FIX: Get and store host Steam ID BEFORE joining
            // This will be used for Riptide P2P connection in OnLobbyEntered
            _state.HostSteamId = _steam.GetLobbyOwner(lobbyId)?.ToString() ?? "";
            
            _log.Msg($"[Client] Joining lobby hosted by SteamID64: {_state.HostSteamId}");
            
            if (_state.ShowDebugUI) _log.Msg($"Joining lobby: {lobbyId}");
            _steam.JoinLobby(lobbyId); // Trigger API
            // Event will trigger OnLobbyEntered via Plugin
        }

        public void OnLobbyEntered(object lobbyId)
        {
            _state.CurrentLobby = lobbyId;
            
            // Initial Data Refresh
            RefreshLobbyData();
            
            _state.ViewState = LobbyUIState.Room;
            _state.IsSearchingForHostedLobby = false; // Clear searching flag

            if (_state.ShowDebugUI) _log.Msg($"[Client] UI Switched to Room. Host: {_state.HostSteamId} (IsHost: {_state.IsHost})");

             // Initialize Local State
             _state.IsLocalReady = false;

             // Initial Data Push (if we are in the lobby)
             // Force a sync from native state if available
             if (_protoLobby != null && _protoLobby.IsReady)
             {
                 int nativeTeam = _protoLobby.GetSelectedTeamIndex();
                 if (nativeTeam >= 0) _state.SelectedTeam = nativeTeam + 1;
                 
                 int nativeCap = _protoLobby.GetSelectedCaptainIndex();
                 if (nativeCap >= 0) _state.SelectedCaptainIndex = nativeCap;
             }

             _steam.SetLobbyMemberData(lobbyId, "team", _state.SelectedTeam.ToString());
             _steam.SetLobbyMemberData(lobbyId, "captain_index", _state.SelectedCaptainIndex.ToString());
             _steam.SetLobbyMemberData(lobbyId, "is_ready", "False");
             
             // CRITICAL: If client, connect to game server via Riptide P2P
             if (!_state.IsHost)
             {
                 // CRITICAL FIX: Use HostSteamId (SteamID64 string) for Riptide connection
                 if (string.IsNullOrEmpty(_state.HostSteamId))
                 {
                     _log.Error("[Client] HostSteamId is null! Cannot connect to game server.");
                     _log.Error("[Client] This should have been set in JoinLobby()");
                     return;
                 }
                 
                 _log.Msg($"[Client] Initiating Riptide P2P connection to host {_state.HostSteamId}...");
                 if (_protoLobby != null && _protoLobby.IsReady)
                 {
                     _protoLobby.ConnectToGameServer(_state.HostSteamId);
                 }
                 else
                 {
                     _log.Error("[Client] ProtoLobby not ready! Cannot connect to game server.");
                 }
             }
        }

        public void RefreshLobbyData()
        {
            if (_state.CurrentLobby == null) return;
            var lobbyId = _state.CurrentLobby;

            // Host Detection
            var ownerId = _steam.GetLobbyOwner(lobbyId);
            var localId = _steam.GetLocalSteamId();
            _state.IsHost = _steam.CSteamIDEquals(ownerId, localId);

            // Host Steam ID: Prefer metadata, fallback to Owner ID
            var hostIdStr = _steam.GetLobbyData(lobbyId, "host_steam_id");
            if (string.IsNullOrEmpty(hostIdStr) && ownerId != null)
            {
                 hostIdStr = _steam.GetSteamIDString(ownerId); 
            }
            _state.HostSteamId = hostIdStr;

            // Lobby Name - Sync to cache via the new plumbing
            _state.CachedLobbyName = _steam.GetLobbyName(lobbyId);
        }

        public void LeaveLobby(bool shutdownNetwork = true)
        {
            if (_state.CurrentLobby != null)
            {
                if (_state.IsHost)
                {
                    _log.Msg("Host leaving lobby. Server may need manual shutdown (F1).");
                }

                if (_state.ShowDebugUI) _log.Msg($"Leaving lobby: {_state.CurrentLobby} (ShutdownNetwork={shutdownNetwork})");
                _steam.LeaveLobby(_state.CurrentLobby);
                
                if (shutdownNetwork)
                {
                     bool wasHost = _state.IsHost;
                    _protoLobby.ShutdownNetwork(wasHost);
                }
                
                _state.ClearLobby();
            }
        }
        
        public void SelectCaptainAndTeam(int captainIndex, int teamIndex)
        {
             // 1. Update Local State
             _state.SelectedCaptainIndex = captainIndex;
             _state.SelectedTeam = teamIndex;
             
             // 2. Sync to Steam
             if (_state.CurrentLobby != null)
             {
                 _steam.SetLobbyMemberData(_state.CurrentLobby, "team", teamIndex.ToString());
                 _steam.SetLobbyMemberData(_state.CurrentLobby, "captain_index", captainIndex.ToString());
             }
             
             // 3. Sync to Game (Native)
             // This ensures clicking OUR button updates the internal game state
             if (_protoLobby != null && _protoLobby.IsReady)
             {
                 _protoLobby.SetSelectedCaptain(captainIndex);
                 _protoLobby.SetSelectedTeam(teamIndex);
             }
             
             // 4. Auto-unready when changing team/captain
             if (_state.IsLocalReady)
             {
                 _log.Msg("[Gameplay] Team/Captain changed - clearing ready status");
                 _state.IsLocalReady = false;
                 
                 if (_state.CurrentLobby != null)
                 {
                     _steam.SetLobbyMemberData(_state.CurrentLobby, "is_ready", "False");
                 }
                 
                 if (_protoLobby != null && _protoLobby.IsReady)
                 {
                     _protoLobby.SetReady(false);
                 }
                 
                 // If we were host-ready, we just unreadied.
                 // If we were client-ready (and called Ready()), we might need to send NotReady?
                 // But valid clients can't unready easily in some flows?
                 // Vanilla OnGUI_Lobby allows toggling ready.
             }
             
             _log.Msg($"Selected Captain {captainIndex}, Team {teamIndex}");
        }

        public void ToggleReady_Host()
        {
            // Flip local ready state
            _state.IsLocalReady = !_state.IsLocalReady;
            _log.Msg($"[Host] Toggling Ready State to: {_state.IsLocalReady}");

            // Update Steam lobby metadata
            if (_state.CurrentLobby != null)
                _steam.SetLobbyMemberData(_state.CurrentLobby, "is_ready", _state.IsLocalReady.ToString());

            // Update native proto-lobby UI (visual only)
            if (_protoLobby != null && _protoLobby.IsReady)
                _protoLobby.SetReady(_state.IsLocalReady);

            // Match in-game flow: OnGUI_Lobby() calls NetworkClient.Ready()
            // AddPlayer is called automatically after Ready() completes
            if (_state.IsLocalReady)
            {
                _protoLobby?.CallNetworkClientReady(_state.SelectedCaptainIndex, _state.SelectedTeam);
            }
        }

        public void ToggleReady()
        {
            // Host constraint
            if (_state.IsHost && !_state.IsLocalReady)
            {
                var unreadyClients = _state.Members
                    .Where(m => !m.IsHost && !m.IsReady)
                    .ToList();

                if (unreadyClients.Count > 0)
                {
                    _log.Msg($"[Gameplay] Host cannot ready yet! Waiting for {unreadyClients.Count} client(s).");
                    return;
                }
            }

            // Flip ready state
            _state.IsLocalReady = !_state.IsLocalReady;
            _log.Msg($"Toggling Ready State to: {_state.IsLocalReady}");

            // Steam lobby sync
            if (_state.CurrentLobby != null)
                _steam.SetLobbyMemberData(_state.CurrentLobby, "is_ready", _state.IsLocalReady.ToString());

            // Sync to native proto-lobby UI
            if (_protoLobby != null && _protoLobby.IsReady)
                _protoLobby.SetReady(_state.IsLocalReady);

            //
            // HOST PATH
            //
            if (_state.IsHost)
            {
                // Host logic: Trigger Network Ready/AddPlayer when passing Ready check
                if (_state.IsLocalReady)
                {
                     _protoLobby?.CallNetworkClientReady(_state.SelectedCaptainIndex, _state.SelectedTeam);
                }
                return;
            }

            //
            // CLIENT PATH
            //
            if (_state.IsLocalReady)
            {
                // CRITICAL CHECK: Verify NetworkClient is connected before calling Ready()
                if (_protoLobby == null || !_protoLobby.IsReady)
                {
                    _log.Error("[Client] ProtoLobby not ready! Cannot complete ready flow.");
                    _state.IsLocalReady = false; // Revert ready state
                    if (_state.CurrentLobby != null)
                        _steam.SetLobbyMemberData(_state.CurrentLobby, "is_ready", "False");
                    return;
                }
                
                if (!_protoLobby.IsConnected)
                {
                    _log.Error("[Client] NetworkClient not connected! Cannot call Ready().");
                    _log.Error("[Client] Wait for P2P connection to establish before readying up.");
                    _state.IsLocalReady = false; // Revert ready state
                    if (_state.CurrentLobby != null)
                        _steam.SetLobbyMemberData(_state.CurrentLobby, "is_ready", "False");
                    return;
                }
                
                _log.Msg("[Client] Ready pressed – running vanilla Ready flow");

                // 1. Native: CompleteProtoLobbyClient()
                _protoLobby.CompleteProtoLobbyClient();

                // 2. Mirror: Ready()
                _protoLobby.CallNetworkClientReady(_state.SelectedCaptainIndex, _state.SelectedTeam);

                // 3. SteamP2P validation
                _protoLobby.ValidatePlayersReadyForGameStart();
            }
            else
            {
                // Unready path
                _state.HasCalledAddPlayer = false;
                _log.Msg("[Client] Unready – Resetting AddPlayer latch");
            }
        }



        public void ConfirmHostedLobby(object lobbyId)
        {
            _log.Msg($"[Auto-Join] Found OUR lobby! {lobbyId}");
            
            // Publish Server Info FIRST (before OnLobbyEntered reads it)
            var localId = _steam.GetLocalSteamId();
            string hostName = _steam.GetLocalPersonaName();
            if (string.IsNullOrEmpty(hostName)) hostName = "Commander";
            string lobbyName = $"{hostName}'s Lobby";
            _steam.SetLobbyData(lobbyId, "name", lobbyName); 
            _state.CachedLobbyName = lobbyName; // Explicitly cache for local UI persistence

            _log.Msg($"[Auto-Join] _steam.GetLocalSteamId() {localId}");
            _steam.SetLobbyData(lobbyId, "host_steam_id", localId?.ToString() ?? "");
            _log.Msg($"[Auto-Join] Lobby metadata set for {lobbyId}");
                        
            // Now delegate setup to OnLobbyEntered (which will read the name we just set)
            OnLobbyEntered(lobbyId);
        }
        public void OnUpdate()
        {
            // ---------------------------------------------------------
            // 1. POLLING SYNC: Game Native -> UI
            // ---------------------------------------------------------
            if (_protoLobby != null && _protoLobby.IsReady)
            {
                int nativeIndex = _protoLobby.GetSelectedCaptainIndex();
                // If native has a valid selection and it differs from ours
                if (nativeIndex >= 0 && nativeIndex != _state.SelectedCaptainIndex)
                {
                    if (_state.ShowDebugUI) _log.Msg($"[Sync] Detected Native Captain Change: {nativeIndex}");
                    
                    // Update Local State directly (skipping the full SelectCaptainAndTeam to avoid loop, or just handle loop gracefully)
                    _state.SelectedCaptainIndex = nativeIndex;
                    
                    // Update Steam (if in lobby)
                    if (_state.CurrentLobby != null)
                    {
                        _steam.SetLobbyMemberData(_state.CurrentLobby, "captain_index", nativeIndex.ToString());
                    }
                }

                // Team Polling
                int nativeGameTeamIndex = _protoLobby.GetSelectedTeamIndex();
                // Native uses 0/1. UI uses 1/2.
                int uiTeamIndex = nativeGameTeamIndex + 1; 

                if (nativeGameTeamIndex >= 0 && uiTeamIndex != _state.SelectedTeam)
                {
                    if (_state.ShowDebugUI) _log.Msg($"[Sync] Detected Native Team Change: {nativeGameTeamIndex} => UI: {uiTeamIndex}");
                    
                    _state.SelectedTeam = uiTeamIndex;
                    
                    if (_state.CurrentLobby != null)
                    {
                        _steam.SetLobbyMemberData(_state.CurrentLobby, "team", uiTeamIndex.ToString());
                    }
                }
            }

            if (_state.ViewState == LobbyUIState.Browser)
            {
                // Browser Logic handled by LobbyUIRoot
            }
            else if (_state.ViewState == LobbyUIState.Room)
            {
                // Room Logic handled by LobbyUIRoot
            }
        }
        public void StartGame()
        {
            if (!_state.IsHost){_log.Warning("[Client] Only host can start the game");return;}

            _log.Msg("[Host] Starting Game...");

            if (_protoLobby == null){_log.Error("[Host] ProtoLobby is null");return;}

            _log.Msg("[Host] Completing ProtoLobby...");
            
            // Clean up lobby to prevent Steam hangs on shutdown
            LeaveLobby(false);
            
            _protoLobby.CompleteProtoLobbyServer();

            _state.ShowDebugUI = false;
        }
    }
}
