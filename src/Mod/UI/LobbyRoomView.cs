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

            // ENSURE BALANCED STACK: Start root vertical
            GUILayout.BeginVertical(LobbyStyles.BoxStyle ?? GUI.skin.box);

            try 
            {
                // 1. Title Row
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{_state.CachedLobbyName}", LobbyStyles.TitleStyle ?? GUI.skin.label);
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
                if (GUILayout.Button("Copy Host ID", LobbyStyles.ButtonStyle, GUILayout.Height(30)))
                {
                    GUIUtility.systemCopyBuffer = _state.HostSteamId;
                    _log.Msg($"Copied host Steam ID: {_state.HostSteamId}");
                }
                
                GUILayout.Space(8);
                
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
                
                // 3. Footer Area
                GUILayout.BeginHorizontal(LobbyStyles.BoxStyle ?? GUI.skin.box);
                int currentP = _state.CurrentLobby != null ? _state.CurrentLobbyMemberCount : 0;
                int maxP = _state.CurrentLobby != null ? _state.CurrentLobbyMaxPlayers : 0;
                GUILayout.Label($"Players: {currentP}/{maxP}", LobbyStyles.HeaderStyle);
                
                GUILayout.FlexibleSpace();
                
                // Ready/Start
                if (GUILayout.Button(_state.IsLocalReady ? "READY!" : "NOT READY", LobbyStyles.ButtonStyle, GUILayout.Width(150), GUILayout.Height(35)))
                {
                    _controller.ToggleReady();
                }
                
                if (_state.IsHost)
                {
                    GUILayout.Space(10);
                    GUI.enabled = _state.IsLocalReady;
                    if (GUILayout.Button("START GAME", LobbyStyles.ButtonStyle, GUILayout.Width(150), GUILayout.Height(35)))
                    {
                        _controller.StartGame();
                    }
                    GUI.enabled = true;
                }
                GUILayout.EndHorizontal();
                
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
            // Header is now the button to join the team
            GUIStyle? t1Style = (_state.SelectedTeam == 1) ? (LobbyStyles.TeamSelectedStyle ?? LobbyStyles.HeaderStyle) : (LobbyStyles.TeamUnselectedStyle ?? LobbyStyles.HeaderStyle);
            if (GUILayout.Button("TEAM A", t1Style ?? GUI.skin.button, GUILayout.Height(30)))
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
            // Header is now the button to join the team
            GUIStyle? t2Style = (_state.SelectedTeam == 2) ? (LobbyStyles.TeamSelectedStyle ?? LobbyStyles.HeaderStyle) : (LobbyStyles.TeamUnselectedStyle ?? LobbyStyles.HeaderStyle);
            if (GUILayout.Button("TEAM B", t2Style ?? GUI.skin.button, GUILayout.Height(30)))
            {
                _controller.SelectCaptainAndTeam(_state.SelectedCaptainIndex, 2);
            }
            
            for (int i = 0; i < 5; i++)
            {
                var member = i < team2Members.Count ? team2Members[i] : null;
                DrawPlayerSlot(i + 5, 2, member);
            }
            GUILayout.EndVertical();
            
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
    }
}
