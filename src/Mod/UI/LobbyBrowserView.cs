using SiroccoLobby.Model;
using SiroccoLobby.Services;
using SiroccoLobby.Controller;
using UnityEngine;
using MelonLoader;

namespace SiroccoLobby.UI
{
    public sealed class LobbyBrowserView
    {
        private readonly LobbyState _state;
        private readonly LobbyController _controller;
        private readonly MelonLogger.Instance _log;

        private Vector2 _scrollPosition;

        public LobbyBrowserView(
            LobbyState state, 
            LobbyController controller, 
            MelonLogger.Instance log)
        {
            _state = state;
            _controller = controller;
            _log = log;
        }

        public void Draw()
        {
            if (!_state.ShowDebugUI) return;

            // Use the same root BoxStyle as RoomView for consistency
            GUILayout.BeginVertical(LobbyStyles.BoxStyle ?? GUI.skin.box);

            try 
            {
                // Shared header
                SharedUIComponents.DrawHeader(
                    _state, 
                    _log,
                    "[Help] F5: Toggle UI | ESC: Cancel Actions",
                    showPlayerCount: false
                );

                // Centered Header
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("LOBBY BROWSER", LobbyStyles.HeaderStyle); 
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
                
                // Buttons and Count - Horizontal Layout
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Refresh", LobbyStyles.ButtonStyle, GUILayout.Width(150), GUILayout.Height(35)))
                {
                    _controller.RefreshLobbyList();
                }
                GUILayout.Space(5);
                if (GUILayout.Button("Create Lobby", LobbyStyles.ButtonStyle, GUILayout.Width(150), GUILayout.Height(35)))
                {
                    _controller.CreateLobby();
                }
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{_state.CachedLobbies.Count} lobbies found", LobbyStyles.HeaderStyle);
                GUILayout.EndHorizontal();
                
                GUILayout.Space(10);
                
                _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.ExpandHeight(true));
                
                if (_state.AvailableLobbies.Count == 0)
                {
                    GUILayout.Label("No lobbies found. Create one to start a battle!", LobbyStyles.HeaderStyle);
                }
                else
                {
                    foreach (var lobby in _state.AvailableLobbies)
                    {
                        GUILayout.BeginHorizontal(LobbyStyles.LobbyCardStyle);
                        string lobbyName = lobby.Name;
                        int currentPlayers = lobby.CurrentPlayers;
                        int maxPlayers = lobby.MaxPlayers;
                        
                        GUILayout.Label($"{lobbyName}", GUILayout.Width(300));
                        GUILayout.FlexibleSpace();
                        GUILayout.Label($"{currentPlayers}/{maxPlayers}", GUILayout.Width(100));
                        
                        bool isFull = lobby.IsFull;
                        
                        if (isFull)
                        {
                            // Show disabled FULL button (not clickable)
                            GUI.enabled = false;
                            GUILayout.Button("FULL", LobbyStyles.ButtonDisabled ?? LobbyStyles.ButtonStyle, GUILayout.Width(100));
                            GUI.enabled = true;
                        }
                        else
                        {
                            // Always allow joining - RefreshLobbyData() after join will get authoritative host ID
                            if (GUILayout.Button("Join", LobbyStyles.ButtonStyle, GUILayout.Width(100)))
                            {
                                _controller.JoinLobby(lobby.LobbyId, lobby.HostSteamId);
                            }
                        }
                        
                        GUILayout.EndHorizontal();
                    }
                }
                
                GUILayout.EndScrollView();
                
                GUILayout.Space(10);
                
                // Bottom Info Bar
                GUILayout.BeginHorizontal();
                GUILayout.Label("Press F5 to close | Naval warfare awaits, Captain!", LobbyStyles.SteamIdStyle);
                GUILayout.FlexibleSpace();
                GUILayout.Label($"My Steam ID: {_controller.GetLocalSteamIdString()}", LobbyStyles.SteamIdStyle);
                GUILayout.EndHorizontal();
            }
            catch (System.Exception ex)
            {
                 GUILayout.Label($"UI Error: {ex.Message}");
                 _log.Error($"LobbyBrowserView Error: {ex.Message}");
            }
            finally
            {
                GUILayout.EndVertical(); // Close BoxStyle
            }
        }
    }
}
