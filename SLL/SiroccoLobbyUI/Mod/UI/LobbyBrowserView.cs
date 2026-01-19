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
        private readonly ISteamLobbyService _steam;
        private readonly MelonLogger.Instance _log;

        private Vector2 _scrollPosition;

        public LobbyBrowserView(
            LobbyState state, 
            LobbyController controller, 
            ISteamLobbyService steam,
            MelonLogger.Instance log)
        {
            _state = state;
            _controller = controller;
            _steam = steam;
            _log = log;
        }

        public void Draw()
        {
            if (!_state.ShowDebugUI) return;

            // Use the same root BoxStyle as RoomView for consistency
            GUILayout.BeginVertical(LobbyStyles.BoxStyle ?? GUI.skin.box);

            try 
            {
                // Centered Header
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("LOBBY BROWSER", LobbyStyles.TitleStyle); 
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
                
                if (_state.CachedLobbies.Count == 0)
                {
                    GUILayout.Label("No lobbies found. Create one to start a battle!", LobbyStyles.HeaderStyle);
                }
                else
                {
                    foreach (var lobbyId in _state.CachedLobbies)
                    {
                        GUILayout.BeginHorizontal(LobbyStyles.LobbyCardStyle);
                        
                        string lobbyName = _steam.GetLobbyData(lobbyId, "name");
                        if (string.IsNullOrEmpty(lobbyName)) lobbyName = "Unnamed Lobby";
                        
                        int currentPlayers = _steam.GetMemberCount(lobbyId);
                        int maxPlayers = _steam.GetMemberLimit(lobbyId);
                        
                        GUILayout.Label($"{lobbyName}", GUILayout.Width(300));
                        GUILayout.FlexibleSpace();
                        GUILayout.Label($"{currentPlayers}/{maxPlayers}", GUILayout.Width(100));
                        
                        bool isFull = currentPlayers >= maxPlayers;
                        GUI.enabled = !isFull;
                        if (GUILayout.Button(isFull ? "FULL" : "Join", LobbyStyles.ButtonStyle, GUILayout.Width(100)))
                        {
                            _controller.JoinLobby(lobbyId);
                        }
                        GUI.enabled = true;
                        
                        GUILayout.EndHorizontal();
                    }
                }
                
                GUILayout.EndScrollView();
                
                GUILayout.Space(10);
                
                // Bottom Info Bar
                GUILayout.BeginHorizontal();
                GUILayout.Label("Press F5 to close | Naval warfare awaits, Captain!", LobbyStyles.SteamIdStyle);
                GUILayout.FlexibleSpace();
                GUILayout.Label($"My Steam ID: {_steam.GetLocalSteamId()}", LobbyStyles.SteamIdStyle);
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
