using UnityEngine;
using MelonLoader;

namespace SiroccoLobby.UI
{
    /// <summary>
    /// Reusable UI components shared across views
    /// </summary>
    public static class SharedUIComponents
    {
        /// <summary>
        /// Draws the standard header with centered title and help/close buttons
        /// </summary>
        /// <param name="state">Lobby state for controlling UI visibility</param>
        /// <param name="log">Logger for help messages</param>
        /// <param name="helpMessage">Custom help message to display when ? is clicked</param>
        /// <param name="onClose">Callback to execute when X button is clicked (for cleanup)</param>
        /// <param name="showPlayerCount">Whether to show player count below header</param>
        /// <param name="currentPlayers">Current player count (if showPlayerCount is true)</param>
        /// <param name="maxPlayers">Max player count (if showPlayerCount is true)</param>
        public static void DrawHeader(
            Model.LobbyState state, 
            MelonLogger.Instance log,
            string helpMessage,
            System.Action? onClose = null,
            bool showPlayerCount = false,
            int currentPlayers = 0,
            int maxPlayers = 0)
        {
            // Title Bar - Use absolute positioning for perfect centering
            GUILayout.BeginHorizontal();
            
            // Top-right controls (pushed to far right)
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("?", LobbyStyles.ButtonStyle, GUILayout.Width(30), GUILayout.Height(30)))
            {
                log.Msg(helpMessage);
            }
            if (GUILayout.Button("X", LobbyStyles.ButtonStyle, GUILayout.Width(30), GUILayout.Height(30)))
            {
                state.ShowDebugUI = false;
                onClose?.Invoke(); // Execute cleanup callback if provided
            }
            GUILayout.EndHorizontal();
            
            // Centered title on its own row
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical();
            GUILayout.Label("SIROCCO", LobbyStyles.TitleStyle);
            GUILayout.Label("Naval Command", LobbyStyles.TitleStyle);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            
            // Optional player count below title, right-aligned
            if (showPlayerCount)
            {
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label($"Players: {currentPlayers}/{maxPlayers}", LobbyStyles.HeaderStyle);
                GUILayout.EndHorizontal();
            }
            
            GUILayout.Space(5);
        }
    }
}
