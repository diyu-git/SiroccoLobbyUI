using System.Collections.Generic;
using Steamworks;
using SteamLobbyLib; // For LobbyId extension methods if any, or just use wrapper

namespace SteamLobbyLib
{
    // Partial class to add helper methods for MelonLoader compatibility
    // and to expose the unified API that the Mod expects
    public partial class SteamLobbyManager
    {
        // Wrapper methods using "object" to match what the Mod controllers might expect
        // from the previous interface, OR we update the controllers to use CSteamID.
        // For easiest migration, we can provide these adaptors using object, 
        // OR we can simple CAST inside the implementation if we change the Interface.
        
        // Actually, let's keep it clean.
        // We will expose methods that match ISteamLobbyService signature roughly but using native types where convenient
        // and rely on our new Plugin implementation to bridge it.
        
        public CSteamID GetLocalSteamId() => SteamUser.GetSteamID();

        public CSteamID GetLobbyMemberByIndex(CSteamID lobbyId, int index)
        {
            return SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, index);
        }

        public string GetLobbyMemberData(CSteamID lobbyId, CSteamID userId, string key)
        {
            return SteamMatchmaking.GetLobbyMemberData(lobbyId, userId, key);
        }

        public void SetLobbyMemberData(CSteamID lobbyId, string key, string value)
        {
            SteamMatchmaking.SetLobbyMemberData(lobbyId, key, value);
        }

        public string GetFriendPersonaName(CSteamID steamId)
        {
            return SteamFriends.GetFriendPersonaName(steamId);
        }
        
        public static bool CSteamIDEquals(CSteamID a, CSteamID b)
        {
             return a == b;
        }

        public static CSteamID CSteamIDNil()
        {
            return CSteamID.Nil;
        }
    }
}
