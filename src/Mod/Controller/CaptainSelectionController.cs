using System;
using System.Collections.Generic;
using SiroccoLobby.Services;
using MelonLoader;

namespace SiroccoLobby.Controller
{
    /// <summary>
    /// Simplified captain selection controller for Steam lobby integration
    /// Works with reflection-based ProtoLobbyIntegration
    /// </summary>
    public class CaptainSelectionController
    {
        private ProtoLobbyIntegration _protoLobby;
        private ISteamLobbyService _steamLobby;
        private bool _initialized;
        
        // Store captain selections for each player in the lobby
        private Dictionary<ulong, int> _playerCaptainSelections = new Dictionary<ulong, int>();

        public CaptainSelectionController(ISteamLobbyService steamLobby, ProtoLobbyIntegration protoLobby)
        {
            _steamLobby = steamLobby;
            _protoLobby = protoLobby;
        }

        /// <summary>
        /// Initialize the captain selection system
        /// </summary>
        public bool Initialize()
        {
            if (_initialized) return true;

            if (!_protoLobby.Initialize())
            {
                MelonLogger.Error("[CaptainSelection] Failed to initialize ProtoLobby");
                return false;
            }

            // Initialize the captain selection dropdown
            // NOTE: The actual game-side captain dropdown initialization (InitializeProtoLobbyCaptainSelection)
            // is performed by the ProtoLobbyIntegration service. The controller should NOT invoke the
            // game's initialize method directly to avoid timing/race issues. The controller's role is to
            // prepare UI state and interact with the service (get names, set username, apply selections).

            // Set the local player's username
            try
            {
                var localId = _steamLobby.GetLocalSteamId();
                string playerName = _steamLobby.GetFriendPersonaName(localId);
                _protoLobby.SetUserName(playerName);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CaptainSelection] Could not set username: {ex.Message}");
            }

            //MelonLogger.Msg($"[CaptainSelection] Initialized with {_protoLobby.GetCaptainCount()} captains available");
            _initialized = true;
            return true;
        }

        /// <summary>
        /// Get the list of available captains for UI display
        /// </summary>
        public List<string> GetCaptainNames()
        {
            var names = new List<string>();
            int count = _protoLobby.GetCaptainCount();
            
            for (int i = 0; i < count; i++)
            {
                names.Add(_protoLobby.GetCaptainName(i));
            }
            
            return names;
        }

        /// <summary>
        /// Local player selects a captain
        /// </summary>
        public void SelectCaptain(int captainIndex)
        {
            if (!_protoLobby.IsReady)
            {
                MelonLogger.Warning("[CaptainSelection] ProtoLobby not ready");
                return;
            }

            // Track local selection (helps even before we model remote players)
            try
            {
                var local = _steamLobby.GetLocalSteamId();
                if (local != null && ulong.TryParse(local.ToString(), out var localUlong))
                {
                    _playerCaptainSelections[localUlong] = captainIndex;
                }
            }
            catch
            {
                // Best-effort only
            }

            // Set in the game's ProtoLobby system
            _protoLobby.SetSelectedCaptain(captainIndex);

            MelonLogger.Msg($"[CaptainSelection] Selected captain {captainIndex}");
        }

        /// <summary>
        /// Get a player's selected captain
        /// </summary>
        public int GetPlayerCaptainSelection(ulong steamId)
        {
            if (_playerCaptainSelections.TryGetValue(steamId, out int captainIndex))
            {
                return captainIndex;
            }

            return -1;
        }

        /// <summary>
        /// Check if all players have selected captains (simplified - checks local cache only)
        /// </summary>
        public bool AllPlayersReady()
        {
            // Simplified: just check if we have any selections
            return _playerCaptainSelections.Count > 0;
        }


        /// <summary>
        /// Handle when a lobby member updates their data
        /// </summary>
        public void OnLobbyMemberDataUpdated(ulong steamId)
        {
            int captainIndex = GetPlayerCaptainSelection(steamId);
            if (captainIndex != -1)
            {
                MelonLogger.Msg($"[CaptainSelection] Player {steamId} selected captain {captainIndex}");
            }
        }

        /// <summary>
        /// Reset captain selections
        /// </summary>
        public void Reset()
        {
            _playerCaptainSelections.Clear();
            // IMPORTANT: don't force the native captain selection here.
            // Reset() is used when entering/leaving lobbies; mutating the game's ProtoLobby selection
            // can clobber selections at the wrong time (and fights UI polling in LobbyController).
        }
    } 
}
