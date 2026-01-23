using System.Collections.Generic;

namespace SiroccoLobby.Model
{
    public sealed class LobbyState
    {
        public LobbyUIState ViewState { get; set; } = LobbyUIState.Browser;

        public object? CurrentLobby { get; set; }
        public bool IsHost { get; set; }

        public IReadOnlyList<object> CachedLobbies => _cachedLobbies;
        private readonly List<object> _cachedLobbies = new List<object>();

        // UI-friendly snapshot of available lobbies populated by the controller
        public IReadOnlyList<LobbySummary> AvailableLobbies => _availableLobbies;
        private readonly List<LobbySummary> _availableLobbies = new List<LobbySummary>();

        // Lobby Syncing
        public IReadOnlyList<LobbyMember> Members => _members;
        private readonly List<LobbyMember> _members = new List<LobbyMember>();

        public bool IsLocalReady { get; set; } = false;
        public bool HasCalledAddPlayer { get; set; } = false; // Track if we've registered with Mirror networking

        public int SelectedCaptainIndex { get; set; } = 0;
        public int SelectedTeam { get; set; } = 2;

        // Captain Mode State
        public bool CaptainModeEnabled { get; set; } = false;
        public CaptainModePhase CaptainModePhase { get; set; } = CaptainModePhase.None;
        public string? CaptainTeamA { get; set; } = null; // Steam ID of Team A captain
        public string? CaptainTeamB { get; set; } = null; // Steam ID of Team B captain
        public int CurrentPickingTeam { get; set; } = 1; // 1 or 2, which captain is currently picking
        public List<string> PickedPlayers { get; } = new List<string>(); // Steam IDs of players who have been picked
        
        // Lobby Feed (for activity log)
        public IReadOnlyList<string> LobbyFeed => _lobbyFeed;
        private readonly List<string> _lobbyFeed = new List<string>();
        
        public void AddFeedMessage(string message)
        {
            _lobbyFeed.Add(message);
            // Keep only last 10 messages
            if (_lobbyFeed.Count > 10)
            {
                _lobbyFeed.RemoveAt(0);
            }
        }

        public string? CachedLobbyName { get; set; }
        public string? HostSteamId { get; set; }
        
        // Current lobby counts (populated by controller)
        public int CurrentLobbyMemberCount { get; set; } = 0;
        public int CurrentLobbyMaxPlayers { get; set; } = 0;
        
        // UI Helpers
        public bool ShowCaptainDropdown { get; set; }
        public bool IsSearchingForHostedLobby { get; set; }
        public bool ShowDebugUI { get; set; } = false;
        public bool IsProtoLobbyReady { get; set; } = false;

        public void ClearLobby()
        {
            CurrentLobby = null;
            IsHost = false;
            CachedLobbyName = null;
            HostSteamId = null;
            ViewState = LobbyUIState.Browser;
            IsSearchingForHostedLobby = false;
            _members.Clear();
            
            // Clear captain mode state
            CaptainModeEnabled = false;
            CaptainModePhase = CaptainModePhase.None;
            CaptainTeamA = null;
            CaptainTeamB = null;
            CurrentPickingTeam = 1;
            PickedPlayers.Clear();
            _lobbyFeed.Clear();
        }

        public void UpdateLobbyList(IEnumerable<object> lobbies)
        {
            _cachedLobbies.Clear();
            _cachedLobbies.AddRange(lobbies);
        }
        
        public void UpdateAvailableLobbies(IEnumerable<LobbySummary> summaries)
        {
            _availableLobbies.Clear();
            _availableLobbies.AddRange(summaries);
        }

        public void UpdateOrAddLobbySummary(LobbySummary summary)
        {
            for (int i = 0; i < _availableLobbies.Count; i++)
            {
                if (Equals(_availableLobbies[i].LobbyId, summary.LobbyId))
                {
                    _availableLobbies[i] = summary;
                    return;
                }
            }
            _availableLobbies.Add(summary);
        }
        
        public void UpdateMembers(IEnumerable<LobbyMember> members)
        {
            _members.Clear();
            _members.AddRange(members);
        }
    }
}
