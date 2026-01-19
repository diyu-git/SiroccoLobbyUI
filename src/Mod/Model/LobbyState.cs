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

        // Lobby Syncing
        public IReadOnlyList<LobbyMember> Members => _members;
        private readonly List<LobbyMember> _members = new List<LobbyMember>();

        public bool IsLocalReady { get; set; } = false;
        public bool HasCalledAddPlayer { get; set; } = false; // Track if we've registered with Mirror networking

        public int SelectedCaptainIndex { get; set; } = 0;
        public int SelectedTeam { get; set; } = 2;

        public string? CachedLobbyName { get; set; }
        public string? HostSteamId { get; set; }
        
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
        }

        public void UpdateLobbyList(IEnumerable<object> lobbies)
        {
            _cachedLobbies.Clear();
            _cachedLobbies.AddRange(lobbies);
        }
        
        public void UpdateMembers(IEnumerable<LobbyMember> members)
        {
            _members.Clear();
            _members.AddRange(members);
        }
    }
}
