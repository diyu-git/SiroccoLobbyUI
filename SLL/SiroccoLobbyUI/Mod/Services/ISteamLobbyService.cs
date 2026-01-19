using System.Collections.Generic;

namespace SiroccoLobby.Services
{
    public interface ISteamLobbyService
    {
        object GetLocalSteamId();
        void RequestLobbyList();
        IEnumerable<object> GetCachedLobbies(int max = 20);

        void CreateLobby(int visibility, int maxPlayers);
        void JoinLobby(object lobbyId);
        void LeaveLobby(object lobbyId);

        object GetLobbyOwner(object lobbyId);
        string GetLobbyName(object? lobbyId = null);
        string GetLobbyData(object lobbyId, string key);
        string GetSteamIDString(object steamId);
        void SetLobbyData(object lobbyId, string key, string value);

        int GetMemberCount(object lobbyId);
        int GetMemberLimit(object lobbyId);
        
        // Member Data
        object GetLobbyMemberByIndex(object lobbyId, int index);
        void SetLobbyMemberData(object lobbyId, string key, string value);
        string GetLobbyMemberData(object lobbyId, object userId, string key);
        string GetLocalPersonaName();
        string GetFriendPersonaName(object userId);
        
        // Helper
        bool CSteamIDEquals(object id1, object id2);
        object CSteamIDNil();
    }
}
