using SiroccoLobby.Model;
using SiroccoLobby.Controller;
using UnityEngine;
using MelonLoader;

namespace SiroccoLobby.UI
{
    public sealed class LobbyUIRoot
    {
        private readonly LobbyState _state;
        private readonly LobbyBrowserView _browser;
        private readonly LobbyRoomView _room;


        public LobbyUIRoot(LobbyState state, LobbyBrowserView browser, LobbyRoomView room)
        {
            _state = state;
            _browser = browser;
            _room = room;
        }

        public void Draw()
        {
            LobbyStyles.Init();

            // Dynamic Window Sizing (Percentage of Screen)
            float windowWidth = Mathf.Clamp(Screen.width * 0.65f, 1050, 1400); 
            float windowHeight = Mathf.Clamp(Screen.height * 0.65f, 600, 800); // Further reduced as requested
            
            float x = (Screen.width - windowWidth) / 2;
            float y = (Screen.height - windowHeight) / 2;

            try
            {
                GUILayout.BeginArea(new Rect(x, y, windowWidth, windowHeight));
                // Use the new WindowStyle for the main container
                GUILayout.BeginVertical(LobbyStyles.WindowStyle ?? GUI.skin.window); 

                if (_state.ViewState == LobbyUIState.Room)
                {
                    _room.Draw();
                }
                else
                {
                    _browser.Draw();
                }
                
                GUILayout.EndVertical();
            }
            finally
            {
                GUILayout.EndArea();
            }
        }
    }
}
