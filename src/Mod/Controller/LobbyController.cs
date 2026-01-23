using SiroccoLobby.Model;
using SiroccoLobby.Services;
using MelonLoader;
using UnityEngine;
using SteamLobbyLib;
namespace SiroccoLobby.Controller
{
    using System.Linq;
    using Il2CppInterop.Runtime;
    using Il2CppInterop.Runtime.InteropTypes;
    using SiroccoLobby.Helpers;
    using SiroccoLobby.Services.Helpers;

    public sealed class LobbyController : ILobbyEvents
    {
        private readonly LobbyState _state;
        private readonly ISteamLobbyService _steam;
        private readonly SteamLobbyServiceWrapper? _wrapper;
        private readonly ProtoLobbyIntegration _protoLobby; // Added
        private readonly MelonLogger.Instance _log;
        private readonly CaptainSelectionController? _captainController;

    private bool _loggedCaptainResetException;
    private bool _loggedLocalSteamIdException;
    private bool _loggedSelectCaptainForwardException;
    private bool _loggedEndLobbyQuitException;

        public LobbyController(
            LobbyState state,
            ISteamLobbyService steam,
            ProtoLobbyIntegration protoLobby, // Added
            MelonLogger.Instance log,
            SteamLobbyServiceWrapper? wrapper = null,
            CaptainSelectionController? captainController = null)
        {
            _state = state;
            _steam = steam;
            _protoLobby = protoLobby; // Added
            _log = log;
            _wrapper = wrapper;
            _captainController = captainController;
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

            // Reset captain selection controller state for this lobby
            try
            {
                _captainController?.Reset();
            }
            catch (System.Exception ex)
            {
                if (!_loggedCaptainResetException)
                {
                    _loggedCaptainResetException = true;
                    _log.Warning($"[LobbyController] CaptainSelectionController.Reset failed: {ex.Message}");
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
            // Current counts
            _state.CurrentLobbyMemberCount = _steam.GetMemberCount(lobbyId);
            _state.CurrentLobbyMaxPlayers = _steam.GetMemberLimit(lobbyId);
        }

        // Build a UI-friendly cache of lobby summaries so Views don't call Steam directly
        public void RebuildLobbyCache()
        {
            var list = new System.Collections.Generic.List<SiroccoLobby.Model.LobbySummary>();
            foreach (var lobbyId in _state.CachedLobbies)
            {
                string name = _steam.GetLobbyData(lobbyId, "name");
                if (string.IsNullOrEmpty(name)) name = "Unnamed Lobby";

                int current = _steam.GetMemberCount(lobbyId);
                int max = _steam.GetMemberLimit(lobbyId);

                list.Add(new SiroccoLobby.Model.LobbySummary
                {
                    LobbyId = lobbyId,
                    Name = name,
                    CurrentPlayers = current,
                    MaxPlayers = max,
                    IsFull = current >= max
                });
            }

            _state.UpdateAvailableLobbies(list);
        }

        // Update a single lobby summary (cheap, incremental) and upsert it into state
        public void UpdateLobbySummary(object lobbyId)
        {
            try
            {
                string name = _steam.GetLobbyData(lobbyId, "name");
                if (string.IsNullOrEmpty(name)) name = "Unnamed Lobby";

                int current = _steam.GetMemberCount(lobbyId);
                int max = _steam.GetMemberLimit(lobbyId);

                var summary = new SiroccoLobby.Model.LobbySummary
                {
                    LobbyId = lobbyId,
                    Name = name,
                    CurrentPlayers = current,
                    MaxPlayers = max,
                    IsFull = current >= max
                };

                _state.UpdateOrAddLobbySummary(summary);
            }
            catch (System.Exception ex)
            {
                _log.Error($"UpdateLobbySummary failed for {lobbyId}: {ex.Message}");
            }
        }

        // Pending batch support for incremental updates (moved from Plugin)
        private readonly System.Collections.Generic.HashSet<ulong> _pendingLobbyUpdates = new System.Collections.Generic.HashSet<ulong>();
        private float _pendingBatchAt = 0f;
        private const float BATCH_DEBOUNCE_SECONDS = 0.2f;

        public void AddPendingLobbyUpdate(ulong lobbyId)
        {
            _pendingLobbyUpdates.Add(lobbyId);
            _pendingBatchAt = Time.realtimeSinceStartup;
        }

        public void ProcessPendingBatch()
        {
            if (_pendingLobbyUpdates.Count == 0) return;
            if (Time.realtimeSinceStartup - _pendingBatchAt < BATCH_DEBOUNCE_SECONDS) return;

            var toProcess = _pendingLobbyUpdates.ToArray();
            _pendingLobbyUpdates.Clear();

            foreach (var id in toProcess)
            {
                UpdateLobbySummary(id);
            }
        }

        // Refresh member list using the mod wrapper (maps DTO -> mod model)
        public void RefreshMembers(object lobbyId)
        {
            if (_wrapper == null) return;
            var members = _wrapper.GetLobbyMembersModel(lobbyId) ?? System.Linq.Enumerable.Empty<LobbyMember>();
            _state.UpdateMembers(members.ToList());
        }

        // ILobbyEvents implementation (receive callbacks from the library)
        public void OnLobbyListReceived(System.Collections.Generic.List<LobbyData> lobbies)
        {
            if (_state.ShowDebugUI) _log.Msg($"[Events] Lobby list received: {lobbies.Count} lobbies");
            _state.UpdateLobbyList(lobbies.Select(l => (object)l.Id.Value));
            RebuildLobbyCache();
        }

        public void OnLobbyJoined(LobbyId lobbyId)
        {
            if (_state.ShowDebugUI) _log.Msg($"[Events] Joined lobby: {lobbyId.Value}");
            OnLobbyEntered(lobbyId.Value);
            // Trigger immediate member refresh
            RefreshMembers(lobbyId.Value);
        }

        public void OnLobbyDataUpdated(LobbyId lobbyId)
        {
            if (_state.CurrentLobby == null) return;
            ulong current = 0;
            if (_state.CurrentLobby is ulong u) current = u;
            else if (_state.CurrentLobby is Steamworks.CSteamID c) current = c.m_SteamID;

            if (current != lobbyId.Value) return;

            if (_state.ShowDebugUI) _log.Msg($"[Events] Current lobby updated");
            RefreshLobbyData();
            RefreshMembers(lobbyId.Value);
            AddPendingLobbyUpdate(lobbyId.Value);
        }

        public void OnLobbyMemberChanged(LobbyId lobbyId, LobbyId memberId, Steamworks.EChatMemberStateChange change)
        {
            if (_state.CurrentLobby == null) return;
            ulong current = 0;
            if (_state.CurrentLobby is ulong u) current = u;
            else if (_state.CurrentLobby is Steamworks.CSteamID c) current = c.m_SteamID;

            if (current != lobbyId.Value) return;

            if (_state.ShowDebugUI) _log.Msg($"[Events] Member {change}");
            
            // Check if a captain left during captain mode
            if (_state.CaptainModeEnabled && (change == Steamworks.EChatMemberStateChange.k_EChatMemberStateChangeLeft ||
                                               change == Steamworks.EChatMemberStateChange.k_EChatMemberStateChangeDisconnected ||
                                               change == Steamworks.EChatMemberStateChange.k_EChatMemberStateChangeKicked ||
                                               change == Steamworks.EChatMemberStateChange.k_EChatMemberStateChangeBanned))
            {
                string leftMemberId = memberId.Value.ToString();
                bool captainLeft = string.Equals(leftMemberId, _state.CaptainTeamA) || string.Equals(leftMemberId, _state.CaptainTeamB);
                
                if (captainLeft)
                {
                    _log.Warning("[Captain Mode] Captain left - cancelling captain mode");
                    ToggleCaptainMode(false); // Cancel captain mode
                }
            }
            
            RefreshMembers(lobbyId.Value);
            AddPendingLobbyUpdate(lobbyId.Value);
        }

        // Allow views to request simple info from controller (avoids view calling _steam directly)
        public string GetLocalSteamIdString()
        {
            var id = _steam.GetLocalSteamId();
            return id?.ToString() ?? string.Empty;
        }

        public ulong GetLocalSteamIdULong()
        {
            var id = _steam.GetLocalSteamId();
            if (id == null) return 0UL;
            if (id is ulong ul) return ul;
            // Try Steamworks type
            try
            {
                if (id is Steamworks.CSteamID cs) return cs.m_SteamID;
            }
            catch (System.Exception ex)
            {
                if (!_loggedLocalSteamIdException)
                {
                    _loggedLocalSteamIdException = true;
                    _log.Warning($"[LobbyController] Failed reading local SteamID via Steamworks.CSteamID: {ex.Message}");
                }
            }

            if (ulong.TryParse(id.ToString(), out var parsed)) return parsed;
            return 0UL;
        }

        /// <summary>
        /// Compare a Steam id (which may be stored as ulong, CSteamID, or string) against the local Steam id.
        /// Views should call this instead of trying to inspect Steam types themselves.
        /// </summary>
        public bool IsLocalSteamId(object? steamId)
        {
            if (steamId == null) return false;

            // Fast path: stored as ulong
            if (steamId is ulong ul) return ul == GetLocalSteamIdULong();

            // Steamworks type
            try
            {
                if (steamId is Steamworks.CSteamID cs) return cs.m_SteamID == GetLocalSteamIdULong();
            }
            catch (System.Exception ex)
            {
                if (!_loggedLocalSteamIdException)
                {
                    _loggedLocalSteamIdException = true;
                    _log.Warning($"[LobbyController] Failed reading SteamID via Steamworks.CSteamID: {ex.Message}");
                }
            }

            // Try to parse string representations
            if (ulong.TryParse(steamId.ToString(), out var parsed)) return parsed == GetLocalSteamIdULong();

            return false;
        }

        // New, explicit operations that separate concerns between Steam, network and local state
        public enum LobbyEndMode
        {
            StartGame,
            UserLeave,
            ApplicationQuit
        }

        public void ExitSteamLobby(object? lobbyId = null)
        {
            var target = lobbyId ?? _state.CurrentLobby;
            if (target == null) return;
            if (_state.ShowDebugUI) _log.Msg($"Exiting Steam lobby: {target}");
            try
            {
                _steam.LeaveLobby(target);
            }
            catch (System.Exception ex)
            {
                _log.Error($"Error while exiting Steam lobby: {ex.Message}");
            }
        }

        public void ShutdownLobbyNetworkIfOwned(bool? wasHost = null)
        {
            if (_protoLobby == null) return;

            bool host = wasHost ?? _state.IsHost;
            // If we're not host and not connected, nothing to do
            if (!host && !_protoLobby.IsConnected) return;

            if (_state.ShowDebugUI) _log.Msg("Shutting down lobby network (owned)");
            try
            {
                _protoLobby.ShutdownNetwork(host);
            }
            catch (System.Exception ex)
            {
                _log.Error($"Error while shutting down network: {ex.Message}");
            }
        }

        public void ClearLobbyState()
        {
            if (_state.ShowDebugUI) _log.Msg("Clearing local lobby state");
            _state.ClearLobby();
        }

        // High-level API encoding intent instead of mechanics
        public void EndLobby(LobbyEndMode mode)
        {
            // Capture current values to avoid races with Steam callbacks
            var lobby = _state.CurrentLobby;
            var wasHost = _state.IsHost;

            switch (mode)
            {
                case LobbyEndMode.StartGame:
                    // Leave Steam (prevent hangs), keep network running for gameplay, clear UI state
                    ExitSteamLobby(lobby);
                    ClearLobbyState();
                    break;

                case LobbyEndMode.UserLeave:
                    // User explicitly left: exit Steam, shutdown network if we own it, clear state
                    ExitSteamLobby(lobby);
                    ShutdownLobbyNetworkIfOwned(wasHost);
                    ClearLobbyState();
                    break;

                case LobbyEndMode.ApplicationQuit:
                    // Best-effort cleanup during app quit
                    try { ExitSteamLobby(lobby); }
                    catch (System.Exception ex)
                    {
                        if (!_loggedEndLobbyQuitException)
                        {
                            _loggedEndLobbyQuitException = true;
                            _log.Warning($"[LobbyController] ExitSteamLobby failed during quit: {ex.Message}");
                        }
                    }

                    try { ShutdownLobbyNetworkIfOwned(wasHost); }
                    catch (System.Exception ex)
                    {
                        if (!_loggedEndLobbyQuitException)
                        {
                            _loggedEndLobbyQuitException = true;
                            _log.Warning($"[LobbyController] ShutdownLobbyNetworkIfOwned failed during quit: {ex.Message}");
                        }
                    }
                    ClearLobbyState();
                    break;
            }
        }

        // Backwards-compatible wrapper for existing callers. Prefer EndLobby(LobbyEndMode).
        public void LeaveLobby(bool shutdownNetwork = true)
        {
            if (shutdownNetwork) EndLobby(LobbyEndMode.UserLeave);
            else EndLobby(LobbyEndMode.StartGame);
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

            // Forward selection to captain controller if present
            try
            {
                _captainController?.SelectCaptain(captainIndex);
            }
            catch (System.Exception ex)
            {
                if (!_loggedSelectCaptainForwardException)
                {
                    _loggedSelectCaptainForwardException = true;
                    _log.Warning($"[LobbyController] CaptainSelectionController.SelectCaptain failed: {ex.Message}");
                }
            }
        }

        // Captain Mode Methods
        
        public void ToggleCaptainMode(bool enabled)
        {
            if (!_state.IsHost)
            {
                _log.Warning("[Captain Mode] Only host can toggle captain mode");
                return;
            }
            
            // Validate constraints
            if (enabled)
            {
                // Must have 4+ players and no one ready
                if (_state.Members.Count() < 4)
                {
                    _log.Warning("[Captain Mode] Need at least 4 players to enable captain mode");
                    return;
                }
                
                if (_state.Members.Any(m => m.IsReady))
                {
                    _log.Warning("[Captain Mode] Cannot enable captain mode when players are ready");
                    return;
                }
                
                _state.CaptainModeEnabled = true;
                _state.CaptainModePhase = CaptainModePhase.AssigningCaptains;
                _log.Msg("[Captain Mode] Enabled - assign captains to begin draft");
            }
            else
            {
                _state.CaptainModeEnabled = false;
                _state.CaptainModePhase = CaptainModePhase.None;
                _state.CaptainTeamA = null;
                _state.CaptainTeamB = null;
                _state.CurrentPickingTeam = 1;
                _state.PickedPlayers.Clear();
                _log.Msg("[Captain Mode] Disabled");
            }
            
            // Sync to Steam lobby metadata
            if (_state.CurrentLobby != null)
            {
                _steam.SetLobbyData(_state.CurrentLobby, "captain_mode", enabled ? "1" : "0");
                _steam.SetLobbyData(_state.CurrentLobby, "captain_phase", ((int)_state.CaptainModePhase).ToString());
                _steam.SetLobbyData(_state.CurrentLobby, "captain_team_a", _state.CaptainTeamA ?? "");
                _steam.SetLobbyData(_state.CurrentLobby, "captain_team_b", _state.CaptainTeamB ?? "");
            }
        }
        
        public void AssignCaptain(int team, string steamId)
        {
            if (!_state.IsHost)
            {
                _log.Warning("[Captain Mode] Only host can assign captains");
                return;
            }
            
            if (!_state.CaptainModeEnabled)
            {
                _log.Warning("[Captain Mode] Captain mode not enabled");
                return;
            }
            
            if (team == 1)
            {
                _state.CaptainTeamA = steamId;
            }
            else if (team == 2)
            {
                _state.CaptainTeamB = steamId;
            }
            
            // Sync to Steam
            if (_state.CurrentLobby != null)
            {
                _steam.SetLobbyData(_state.CurrentLobby, $"captain_team_{(team == 1 ? "a" : "b")}", steamId);
            }
            
            // Check if both captains assigned -> move to drafting phase
            if (!string.IsNullOrEmpty(_state.CaptainTeamA) && !string.IsNullOrEmpty(_state.CaptainTeamB))
            {
                _state.CaptainModePhase = CaptainModePhase.Drafting;
                _state.CurrentPickingTeam = 1; // Team A picks first
                
                if (_state.CurrentLobby != null)
                {
                    _steam.SetLobbyData(_state.CurrentLobby, "captain_phase", ((int)_state.CaptainModePhase).ToString());
                }
                
                _log.Msg("[Captain Mode] Both captains assigned - drafting phase started");
                _state.AddFeedMessage("Draft started!");
            }
            
            _log.Msg($"[Captain Mode] Assigned captain for team {team}: {GetPlayerName(steamId) ?? steamId}");
            string captainName = GetPlayerName(steamId) ?? "Unknown";
            _state.AddFeedMessage($"Captain {(team == 1 ? "A" : "B")}: {captainName}");
        }
        
        public void PickPlayer(string? steamId)
        {
            if (string.IsNullOrEmpty(steamId))
            {
                _log.Warning("[Captain Mode] Invalid Steam ID for pick");
                return;
            }
            
            if (!_state.CaptainModeEnabled || _state.CaptainModePhase != CaptainModePhase.Drafting)
            {
                _log.Warning("[Captain Mode] Not in drafting phase");
                return;
            }
            
            // Verify the local player is the current picking captain
            string? currentCaptainId = _state.CurrentPickingTeam == 1 ? _state.CaptainTeamA : _state.CaptainTeamB;
            if (!IsLocalSteamId(currentCaptainId))
            {
                _log.Warning("[Captain Mode] Not your turn to pick");
                return;
            }
            
            // Add to picked players list
            _state.PickedPlayers.Add(steamId);
            
            // Assign to current captain's team
            int targetTeam = _state.CurrentPickingTeam;
            if (_state.CurrentLobby != null)
            {
                _steam.SetLobbyMemberData(_state.CurrentLobby, "team", targetTeam.ToString());
                
                // Sync picked player to lobby metadata (for other clients)
                // We'll store the picked list as comma-separated Steam IDs
                string pickedList = string.Join(",", _state.PickedPlayers);
                _steam.SetLobbyData(_state.CurrentLobby, "picked_players", pickedList);
            }
            
            _log.Msg($"[Captain Mode] Team {targetTeam} captain picked: {GetPlayerName(steamId) ?? steamId}");
            
            // Add to feed
            string pickedName = GetPlayerName(steamId) ?? "Unknown";
            string teamLabel = targetTeam == 1 ? "Team A" : "Team B";
            _state.AddFeedMessage($"{teamLabel}: {pickedName}");
            
            // Snake draft: switch picking team
            // Pattern: A, B, B, A, A, B, B, A...
            int pickNumber = _state.PickedPlayers.Count;
            int pairsComplete = (pickNumber - 1) / 2; // How many AB pairs done
            bool isSecondInPair = (pickNumber % 2) == 0;
            
            if (isSecondInPair)
            {
                // Stay on same team (B picks twice, or A picks twice)
                // Don't switch
            }
            else
            {
                // Switch team (after A's first pick, or after B's double pick)
                _state.CurrentPickingTeam = _state.CurrentPickingTeam == 1 ? 2 : 1;
            }
            
            // Sync picking team to lobby
            if (_state.CurrentLobby != null)
            {
                _steam.SetLobbyData(_state.CurrentLobby, "current_picking_team", _state.CurrentPickingTeam.ToString());
            }
            
            // Check if draft complete (all non-captain players picked)
            int totalPlayers = _state.Members.Count();
            int captains = 2;
            int playersNeedingPick = totalPlayers - captains;
            
            if (_state.PickedPlayers.Count >= playersNeedingPick)
            {
                // Draft complete
                _state.CaptainModePhase = CaptainModePhase.Complete;
                
                if (_state.CurrentLobby != null)
                {
                    _steam.SetLobbyData(_state.CurrentLobby, "captain_phase", ((int)_state.CaptainModePhase).ToString());
                }
                
                _log.Msg("[Captain Mode] Draft complete! Ready for game start.");
                _state.AddFeedMessage("Draft complete!");
            }
        }
        
        private string? GetPlayerName(string? steamId)
        {
            if (string.IsNullOrEmpty(steamId)) return null;
            var member = _state.Members.FirstOrDefault(m => string.Equals(m.SteamId, steamId));
            return member?.Name;
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

        public uint HashprotoLobbyPlayerId(string protoLobbyPlayerId)
        {
            uint rpcId = StringToUintFNV1a.Compute(protoLobbyPlayerId);
            MelonLogger.Msg($"[ProtoTrace] RPC hash = {rpcId}");
            return rpcId;
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

                // 1. Mirror: Ready() - sends ready state + adds player to game
                _protoLobby.CallNetworkClientReady(_state.SelectedCaptainIndex, _state.SelectedTeam);

                // 2. DO NOT call CompleteProtoLobbyClient() here!
                // Client should wait for host's RpcNotifyGameStarted() before completing.
                // TODO: Implement OnGameStartReceived() handler to complete client flow when host starts game
                // _protoLobby.CompleteProtoLobbyClient(); // REMOVED - called too early
            }
            else
            {
                // Unready path
                _state.HasCalledAddPlayer = false;
                _log.Msg("[Client] Unready Resetting AddPlayer latch");
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
            
            // Check captain mode draft completion
            if (_state.CaptainModeEnabled && _state.CaptainModePhase != CaptainModePhase.Complete)
            {
                _log.Warning("[Host] Cannot start game - captain mode draft not complete");
                return;
            }

            _log.Msg("[Host] Starting Game...");

            if (_protoLobby == null){_log.Error("[Host] ProtoLobby is null");return;}

            _log.Msg("[Host] Completing ProtoLobby - sending game start RPC to clients...");
            
            // CRITICAL: Send game start RPC BEFORE destroying Steam lobby
            // CompleteProtoLobbyServer() sends RpcNotifyGameStarted() to all clients
            _protoLobby.CompleteProtoLobbyServer();
            
            // THEN clean up Steam lobby after clients receive the signal
            var lobby = _state.CurrentLobby;
            ExitSteamLobby(lobby);
            ClearLobbyState();

            _state.ShowDebugUI = false;
        }
    }
}
