using SiroccoLobby.Model;
using SiroccoLobby.Services;
using SiroccoLobby.Controller;
using UnityEngine;
using MelonLoader;
using System.Linq;

namespace SiroccoLobby.UI
{
    public sealed class LobbyRoomView
    {
        private readonly LobbyState _state;
        private readonly LobbyController _controller;
        private readonly MelonLogger.Instance _log;
        private readonly ProtoLobbyIntegration _protoLobby;
        private long _lastLogTime = 0;

        // Copy feedback state
        private bool _showCopiedFeedback = false;
        private float _copiedFeedbackUntil = 0f;
        
        // Captain selector state
        private bool _showCaptainSelector = false;
        private int _captainSelectorForTeam = 1; // 1 = Team A, 2 = Team B
        private Rect _captainSelectorRect = new Rect(200, 150, 300, 400);

        public LobbyRoomView(
            LobbyState state, 
            LobbyController controller,
            ProtoLobbyIntegration protoLobby,
            MelonLogger.Instance log)
        {
            _state = state;
            _controller = controller;
            _protoLobby = protoLobby;
            _log = log;
        }

        // Window state
        // Window state - Smaller Size for Dropdown
        private Rect _captainWindowRect = new Rect(300, 200, 250, 300);
        private const int CAPTAIN_WINDOW_ID = 1001;
        
        // P2 Fix: Cache slot strings to reduce allocations
        private static readonly string[] _slotLabels = System.Linq.Enumerable.Range(1, 20).Select(i => $"Slot {i}").ToArray();

        public void Draw()
        {
            if (!_state.ShowDebugUI) return;

            // Update copy feedback timer
            if (_showCopiedFeedback && Time.realtimeSinceStartup > _copiedFeedbackUntil)
            {
                _showCopiedFeedback = false;
            }

            // ENSURE BALANCED STACK: Start root vertical
            GUILayout.BeginVertical(LobbyStyles.BoxStyle ?? GUI.skin.box);

            try 
            {
                // Shared header with player count and leave lobby cleanup
                int currentP = _state.CurrentLobby != null ? _state.CurrentLobbyMemberCount : 0;
                int maxP = _state.CurrentLobby != null ? _state.CurrentLobbyMaxPlayers : 0;
                SharedUIComponents.DrawHeader(
                    _state, 
                    _log,
                    "[Help] F5: Toggle UI | Ready: Toggle ready status | Teams: Click TEAM A/B header",
                    onClose: () => _controller.EndLobby(LobbyController.LobbyEndMode.UserLeave),
                    showPlayerCount: true,
                    currentPlayers: currentP,
                    maxPlayers: maxP
                );

                // 1. Title Row
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{_state.CachedLobbyName}", LobbyStyles.HeaderStyle);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                
                GUILayout.Space(10);
                
                // 2. Main Body (Horizontal Row containing Sidebar and Content)
                GUILayout.BeginHorizontal();
                
                // Left Sidebar
                GUILayout.BeginVertical(GUILayout.Width(170));
                if (GUILayout.Button("Leave Lobby", LobbyStyles.ButtonStyle, GUILayout.Height(35)))
                {
                    _controller.EndLobby(SiroccoLobby.Controller.LobbyController.LobbyEndMode.UserLeave);
                }

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Copy Host ID", LobbyStyles.ButtonStyle, GUILayout.Height(30)))
                {
                    GUIUtility.systemCopyBuffer = _state.HostSteamId;
                    _log.Msg($"Copied host Steam ID: {_state.HostSteamId}");
                    _showCopiedFeedback = true;
                    _copiedFeedbackUntil = Time.realtimeSinceStartup + 2f; // Show for 2 seconds
                }
                if (_showCopiedFeedback)
                {
                    GUILayout.Label("✓ Copied!", LobbyStyles.SuccessFeedbackStyle, GUILayout.Width(60));
                }
                GUILayout.EndHorizontal();
                
                GUILayout.Space(8);
                
                // Captain Mode Section (Host Only)
                if (_state.IsHost)
                {
                    GUILayout.Label("CAPTAIN MODE", LobbyStyles.HeaderStyle);
                    
                    // Enable/Disable toggle
                    bool canEnableCaptainMode = !_state.Members.Any(m => m.IsReady) && _state.Members.Count() >= 4;
                    GUI.enabled = canEnableCaptainMode;
                    
                    bool newCaptainMode = GUILayout.Toggle(_state.CaptainModeEnabled, "Enable Captain Mode");
                    if (newCaptainMode != _state.CaptainModeEnabled)
                    {
                        _controller.ToggleCaptainMode(newCaptainMode);
                    }
                    
                    GUI.enabled = true;
                    
                    if (!canEnableCaptainMode && !_state.CaptainModeEnabled)
                    {
                        GUILayout.Label("Need 4+ players, no ready", LobbyStyles.SteamIdStyle);
                    }
                    
                    // Captain assignment dropdowns (only when enabled)
                    if (_state.CaptainModeEnabled)
                    {
                        GUILayout.Space(5);
                        
                        // Team A Captain
                        string captainAName = GetPlayerName(_state.CaptainTeamA) ?? "Select Captain A";
                        if (GUILayout.Button($"Captain A: {captainAName}", LobbyStyles.ButtonStyle, GUILayout.Height(30)))
                        {
                            _showCaptainSelector = true;
                            _captainSelectorForTeam = 1;
                        }
                        
                        // Team B Captain
                        string captainBName = GetPlayerName(_state.CaptainTeamB) ?? "Select Captain B";
                        if (GUILayout.Button($"Captain B: {captainBName}", LobbyStyles.ButtonStyle, GUILayout.Height(30)))
                        {
                            _showCaptainSelector = true;
                            _captainSelectorForTeam = 2;
                        }
                    }
                    
                    GUILayout.Space(8);
                }
                
                string capDisplay = (_state.IsProtoLobbyReady && _protoLobby.IsReady) 
                    ? _protoLobby.GetCaptainName(_state.SelectedCaptainIndex) 
                    : $"Captain {_state.SelectedCaptainIndex + 1}";

                if (GUILayout.Button($"CAPTAIN: {capDisplay.Split(' ').Last()}", LobbyStyles.ButtonStyle, GUILayout.Height(35))) // Compact name
                {
                    _state.ShowCaptainDropdown = !_state.ShowCaptainDropdown;
                }
                
                // FIX: Removed GUILayoutUtility.GetLastRect() as it's stripped in Il2Cpp and causes a crash.
                // We use a fixed offset adjacent to the sidebar instead.
                _captainWindowRect = new Rect(200, 80, 250, 350);
                
                GUILayout.EndVertical();

                GUILayout.Space(10);
                
                // Right Content Area (Rosters)
                GUILayout.BeginVertical();
                
                GUILayout.Space(5);
                
                // Rosters
                DrawRosters();
                
                GUILayout.EndVertical();
                
                GUILayout.EndHorizontal(); // End Main Body
                
                GUILayout.FlexibleSpace();
                
                // 3. Footer Area - Draft UI or Ready/Start buttons
                if (_state.CaptainModeEnabled && _state.CaptainModePhase == CaptainModePhase.Drafting)
                {
                    DrawDraftInterface();
                }
                else
                {
                    DrawReadyStartButtons();
                }
                
                // Connection Info
                GUILayout.BeginHorizontal();
                GUILayout.Label("P2P Connection Active | F5 Navigation", LobbyStyles.SteamIdStyle);
                GUILayout.FlexibleSpace();
                GUILayout.Label($"Host ID: {_state.HostSteamId}", LobbyStyles.SteamIdStyle);
                GUILayout.EndHorizontal();
            }
            catch (System.Exception ex)
            {
                GUILayout.Label($"[UI ERROR] Render Failed. See log.", LobbyStyles.HeaderStyle);
                if (Event.current.type == EventType.Repaint)
                {
                    long now = System.DateTime.Now.Ticks / 10000000;
                    if (now > _lastLogTime + 5) { _log.Error($"[UI Room] Error: {ex.Message}"); _lastLogTime = now; } // Throttle log
                }
            }
            finally
            {
                // ALWAYS close the root vertical to prevent IMGUI layout corruption
                GUILayout.EndVertical(); 
            }

            // Dropdown Overlay - Draw OUTSIDE the main vertical to avoid layout shifting
            if (_state.ShowCaptainDropdown)
            {
                try 
                {
                    GUI.Box(_captainWindowRect, "Select Captain", LobbyStyles.WindowStyle ?? GUI.skin.window);
                    GUILayout.BeginArea(_captainWindowRect);
                    GUILayout.Space(25);
                    DrawCaptainWindowContent(0);
                    GUILayout.EndArea();
                }
                catch (System.Exception ex)
                {
                    // Throttle overlay errors: this is UI-only and can happen during layout/anchor changes.
                    if (Event.current.type == EventType.Repaint)
                    {
                        long now = System.DateTime.Now.Ticks / 10000000;
                        if (now > _lastLogTime + 5)
                        {
                            _log.Error($"[UI Room] Captain overlay error: {ex.Message}");
                            _lastLogTime = now;
                        }
                    }
                }
            }
            
            // Captain Selector Overlay (for captain mode assignment)
            if (_showCaptainSelector)
            {
                try
                {
                    string title = _captainSelectorForTeam == 1 ? "Select Captain for Team A" : "Select Captain for Team B";
                    GUI.Box(_captainSelectorRect, title, LobbyStyles.WindowStyle ?? GUI.skin.window);
                    GUILayout.BeginArea(_captainSelectorRect);
                    GUILayout.Space(25);
                    DrawCaptainSelectorContent();
                    GUILayout.EndArea();
                }
                catch (System.Exception ex)
                {
                    // Throttle overlay errors
                    if (Event.current.type == EventType.Repaint)
                    {
                        long now = System.DateTime.Now.Ticks / 10000000;
                        if (now > _lastLogTime + 5)
                        {
                            _log.Error($"[UI Room] Captain selector error: {ex.Message}");
                            _lastLogTime = now;
                        }
                    }
                }
            }
        }

        private void DrawCaptainSelectorContent()
        {
            GUILayout.BeginVertical();
            
            GUILayout.Label("Select a player to be captain:", LobbyStyles.HeaderStyle);
            GUILayout.Space(10);
            
            // Get the other captain's Steam ID to prevent duplicates
            string? otherCaptain = _captainSelectorForTeam == 1 ? _state.CaptainTeamB : _state.CaptainTeamA;
            
            // List all lobby members
            foreach (var member in _state.Members)
            {
                if (member.SteamId == null) continue;
                
                // Disable if already the other captain
                bool isOtherCaptain = !string.IsNullOrEmpty(otherCaptain) && string.Equals(member.SteamId, otherCaptain);
                
                GUI.enabled = !isOtherCaptain;
                
                string buttonLabel = member.Name ?? "Unknown";
                if (isOtherCaptain) buttonLabel += " (Other Captain)";
                
                if (GUILayout.Button(buttonLabel, LobbyStyles.ButtonStyle, GUILayout.Height(35)))
                {
                    _controller.AssignCaptain(_captainSelectorForTeam, member.SteamId?.ToString() ?? "");
                    _showCaptainSelector = false;
                    _log.Msg($"[Captain Mode] Assigned {member.Name} as Captain {(_captainSelectorForTeam == 1 ? "A" : "B")}");
                }
                
                GUI.enabled = true;
            }
            
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Cancel", LobbyStyles.ButtonStyle, GUILayout.Height(35)))
            {
                _showCaptainSelector = false;
            }
            
            GUILayout.EndVertical();
        }

        private void DrawReadyStartButtons()
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            GUILayout.BeginVertical();
            
            // Check if ready is locked due to captain mode drafting
            bool canReady = !_state.CaptainModeEnabled || _state.CaptainModePhase == CaptainModePhase.Complete;
            
            if (!canReady)
            {
                // Locked during draft
                GUI.enabled = false;
                GUILayout.Button("Waiting for draft...", LobbyStyles.ReadyButtonNotReady ?? LobbyStyles.ButtonStyle, GUILayout.Width(400), GUILayout.Height(50));
                GUI.enabled = true;
            }
            else
            {
                // Normal ready button
                GUIStyle? readyStyle = _state.IsLocalReady 
                    ? (LobbyStyles.ReadyButtonReady ?? LobbyStyles.ButtonStyle)
                    : (LobbyStyles.ReadyButtonNotReady ?? LobbyStyles.ButtonStyle);

                if (GUILayout.Button(_state.IsLocalReady ? "✓ READY!" : "NOT READY", readyStyle ?? GUI.skin.button, GUILayout.Width(400), GUILayout.Height(50)))
                {
                    _controller.ToggleReady();
                }
            }
            
            if (_state.IsHost)
            {
                GUILayout.Space(8);
                
                // Can only start if ready AND (captain mode not enabled OR draft complete)
                bool canStart = _state.IsLocalReady && 
                               (!_state.CaptainModeEnabled || _state.CaptainModePhase == CaptainModePhase.Complete);
                
                GUI.enabled = canStart;
                GUIStyle? startStyle = canStart
                    ? (LobbyStyles.StartGameButton ?? LobbyStyles.ButtonStyle)
                    : (LobbyStyles.ButtonDisabled ?? LobbyStyles.ButtonStyle);

                if (GUILayout.Button("START GAME", startStyle ?? GUI.skin.button, GUILayout.Width(400), GUILayout.Height(55)))
                {
                    _controller.StartGame();
                }
                GUI.enabled = true;
            }
            
            GUILayout.EndVertical();
            
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void DrawDraftInterface()
        {
            GUILayout.BeginVertical(LobbyStyles.BoxStyle ?? GUI.skin.box);
            
            // Draft header
            GUILayout.Label("CAPTAIN DRAFT IN PROGRESS", LobbyStyles.TitleStyle);
            GUILayout.Space(10);
            
            // Show current picking captain
            string? currentCaptainId = _state.CurrentPickingTeam == 1 ? _state.CaptainTeamA : _state.CaptainTeamB;
            string currentCaptainName = GetPlayerName(currentCaptainId) ?? "Unknown";
            string teamLabel = _state.CurrentPickingTeam == 1 ? "Team A" : "Team B";
            
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{teamLabel} Captain ({currentCaptainName}) is picking...", LobbyStyles.HeaderStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            
            GUILayout.Space(10);
            
            // Available players (not yet picked, not captains)
            var availablePlayers = _state.Members
                .Where(m => m.SteamId != null && 
                           !string.Equals(m.SteamId, _state.CaptainTeamA) && 
                           !string.Equals(m.SteamId, _state.CaptainTeamB) &&
                           !_state.PickedPlayers.Contains(m.SteamId))
                .ToList();
            
            if (availablePlayers.Count > 0)
            {
                GUILayout.Label("Available Players:", LobbyStyles.HeaderStyle);
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                
                // Show players in grid
                GUILayout.BeginVertical(GUILayout.Width(600));
                int playersPerRow = 3;
                for (int i = 0; i < availablePlayers.Count; i += playersPerRow)
                {
                    GUILayout.BeginHorizontal();
                    
                    for (int j = 0; j < playersPerRow && (i + j) < availablePlayers.Count; j++)
                    {
                        var player = availablePlayers[i + j];
                        
                        // Only captain can pick
                        bool isLocalCaptainPicking = _controller.IsLocalSteamId(currentCaptainId);
                        GUI.enabled = isLocalCaptainPicking;
                        
                        if (GUILayout.Button(player.Name ?? "Unknown", LobbyStyles.ButtonStyle, GUILayout.Width(190), GUILayout.Height(40)))
                        {
                            _controller.PickPlayer(player.SteamId?.ToString());
                        }
                        
                        GUI.enabled = true;
                    }
                    
                    GUILayout.EndHorizontal();
                    GUILayout.Space(5);
                }
                GUILayout.EndVertical();
                
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("All players picked! Finalizing draft...", LobbyStyles.HeaderStyle);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            
            GUILayout.Space(10);
            GUILayout.EndVertical();
        }

        private void DrawCaptainWindowContent(int id)
        {
            GUILayout.BeginVertical();
            
             if (_state.IsProtoLobbyReady && _protoLobby.IsReady)
            {
                int captainCount = _protoLobby.GetCaptainCount();
                
                // Scroll view for list
                // We'll rely on Window's auto-layout for now, or add a scroll view if needed.
                // Assuming < 10 captains, it fits.
                
                for (int i = 0; i < captainCount; i++)
                {
                    string captainName = _protoLobby.GetCaptainName(i);
                    if (GUILayout.Button(captainName, LobbyStyles.ButtonStyle))
                    {
                        _controller.SelectCaptainAndTeam(i, _state.SelectedTeam);
                        _state.ShowCaptainDropdown = false;
                        _log.Msg($"Selected Captain: {captainName}");
                    }
                }
            }
            else
            {
                GUILayout.Label("ProtoLobby not initialized", LobbyStyles.SteamIdStyle);
                GUILayout.Label("Press F5 to initialize game lobby", LobbyStyles.SteamIdStyle);
            }
            
            if (GUILayout.Button("Close", LobbyStyles.ButtonStyle))
            {
                _state.ShowCaptainDropdown = false;
            }
            
            GUILayout.EndVertical();
        }


        private void DrawRosters()
        {
            // Disable interaction if captain dropdown is open
            bool originalEnabled = GUI.enabled;
            if (_state.ShowCaptainDropdown) GUI.enabled = false;

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace(); // Center rosters
            
            // Filter members
            var team1Members = new System.Collections.Generic.List<LobbyMember>();
            var team2Members = new System.Collections.Generic.List<LobbyMember>();
            foreach(var m in _state.Members)
            {
                if (m.Team == 1) team1Members.Add(m);
                else if (m.Team == 2) team2Members.Add(m);
            }

            // TEAM A
            GUILayout.BeginVertical(LobbyStyles.BoxStyle ?? GUI.skin.box, GUILayout.Width(400), GUILayout.ExpandHeight(false));
            
            // Clickable team header with strong visual feedback
            GUIStyle? t1Style = (_state.SelectedTeam == 1) ? (LobbyStyles.TeamSelectedStyle ?? LobbyStyles.HeaderStyle) : (LobbyStyles.TeamUnselectedStyle ?? LobbyStyles.HeaderStyle);
            if (GUILayout.Button("⚓ TEAM A", t1Style ?? GUI.skin.button, GUILayout.Height(35)))
            {
                _controller.SelectCaptainAndTeam(_state.SelectedCaptainIndex, 1);
            }
            
            for (int i = 0; i < 5; i++)
            {
                var member = i < team1Members.Count ? team1Members[i] : null;
                DrawPlayerSlot(i, 1, member);
            }
            GUILayout.EndVertical();
            
            GUILayout.Space(15);
            
            // TEAM B
            GUILayout.BeginVertical(LobbyStyles.BoxStyle ?? GUI.skin.box, GUILayout.Width(400), GUILayout.ExpandHeight(false));
            
            // Clickable team header with strong visual feedback
            GUIStyle? t2Style = (_state.SelectedTeam == 2) ? (LobbyStyles.TeamSelectedStyle ?? LobbyStyles.HeaderStyle) : (LobbyStyles.TeamUnselectedStyle ?? LobbyStyles.HeaderStyle);
            if (GUILayout.Button("⚓ TEAM B", t2Style ?? GUI.skin.button, GUILayout.Height(35)))
            {
                _controller.SelectCaptainAndTeam(_state.SelectedCaptainIndex, 2);
            }
            
            for (int i = 0; i < 5; i++)
            {
                var member = i < team2Members.Count ? team2Members[i] : null;
                DrawPlayerSlot(i + 5, 2, member);
            }
            GUILayout.EndVertical();
            
            GUILayout.Space(15);
            
            // Lobby Feed (right side)
            if (_state.CaptainModeEnabled && _state.LobbyFeed.Count > 0)
            {
                GUILayout.BeginVertical(LobbyStyles.BoxStyle ?? GUI.skin.box, GUILayout.Width(200), GUILayout.ExpandHeight(false));
                GUILayout.Label("ACTIVITY", LobbyStyles.HeaderStyle);
                
                foreach (var message in _state.LobbyFeed)
                {
                    GUILayout.Label(message, LobbyStyles.SteamIdStyle);
                }
                
                GUILayout.EndVertical();
            }
            
            GUILayout.FlexibleSpace(); // Center the rosters
            GUILayout.EndHorizontal();

            GUI.enabled = originalEnabled;
        }

        private void DrawPlayerSlot(int slotIndex, int team, LobbyMember? member)
        {
            // Fixed height to prevent jumpy layout
            GUILayout.BeginHorizontal(LobbyStyles.LobbyCardStyle, GUILayout.Height(36));
            
            string slotLabel = slotIndex >= 0 && slotIndex < _slotLabels.Length ? _slotLabels[slotIndex] : $"Slot {slotIndex + 1}";
            GUILayout.Label($"{slotLabel}", LobbyStyles.PlayerNameStyle, GUILayout.Width(70));
            
            if (member != null)
            {
                // Get Captain Name from ProtoLobby if available
                string capName = "None";
                if (_state.IsProtoLobbyReady && _protoLobby.IsReady && member.CaptainIndex >= 0)
                {
                    capName = _protoLobby.GetCaptainName(member.CaptainIndex);
                }
                else if (member.CaptainIndex >= 0)
                {
                    capName = $"Captain {member.CaptainIndex + 1}";
                }

                string displayName = member.Name ?? "Unknown";
                
                // Add captain indicator for captain mode
                bool isCaptain = _state.CaptainModeEnabled && 
                                 member.SteamId != null && 
                                 (string.Equals(member.SteamId, _state.CaptainTeamA) || string.Equals(member.SteamId, _state.CaptainTeamB));
                if (isCaptain) displayName = "[C] " + displayName;
                
                if (member.IsHost) displayName += " (Host)";
                if (member.SteamId != null && _controller.IsLocalSteamId(member.SteamId)) displayName += " (You)";
                
                // Truncate long names to fit fixed widths
                if (displayName.Length > 16) displayName = displayName.Substring(0, 13) + "...";
                if (capName.Length > 12) capName = capName.Substring(0, 9) + "...";
                
                GUILayout.Label($"{displayName}", LobbyStyles.PlayerNameStyle, GUILayout.Width(140));
                GUILayout.Label($"{capName}", LobbyStyles.PlayerCapStyle, GUILayout.Width(100));
                
                // Zero-margin status label prevents shifting
                GUILayout.Label(member.IsReady ? "[READY]" : "", LobbyStyles.PlayerStatusStyle, GUILayout.Width(70));
            }
            else
            {
                // Same style and fixed width as populated slots for perfect stability
                GUILayout.Label("Empty Slot", LobbyStyles.PlayerCapStyle, GUILayout.Width(310));
            }
            
            GUILayout.EndHorizontal();
        }

        private string? GetPlayerName(string? steamId)
        {
            if (string.IsNullOrEmpty(steamId)) return null;
            
            var member = _state.Members.FirstOrDefault(m => string.Equals(m.SteamId, steamId));
            return member?.Name;
        }
    }
}
