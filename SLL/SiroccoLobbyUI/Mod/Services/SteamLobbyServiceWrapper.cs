using SteamLobbyLib;
using MelonLoader;
using System.Collections.Generic;
using Steamworks;
using System.Linq;

namespace SiroccoLobby.Services
{
    public class SteamLobbyServiceWrapper : ISteamLobbyService
    {
        private readonly SteamLobbyManager _manager;
        private readonly MelonLogger.Instance _log;

        public SteamLobbyServiceWrapper(SteamLobbyManager manager, MelonLogger.Instance log)
        {
            _manager = manager;
            _log = log;
        }

        private CSteamID ToSteamID(object obj)
        {
            if (obj is ulong ul) return new CSteamID(ul);
            if (obj is CSteamID c) return c;
            if (obj == null) return CSteamID.Nil;
            return CSteamID.Nil;
        }

        public object GetLocalSteamId() => SteamUser.GetSteamID().m_SteamID;
        
        public void RequestLobbyList() => _manager.RequestLobbyList();
        
        public IEnumerable<object> GetCachedLobbies(int max = 20)
        {
            return _manager.CachedLobbies
                .Take(max)
                .Select(l => (object)l.Id.Value);
        }

        public void CreateLobby(int visibility, int maxPlayers)
        {
            // SLL currently defaults to Public. ignoring visibility param for now.
            _manager.CreateLobby(maxPlayers);
        }

        public void JoinLobby(object lobbyId)
        {
            // JoinLobby in Manager takes LobbyId
            _manager.JoinLobby(new LobbyId(ToSteamID(lobbyId).m_SteamID));
        }

        public void LeaveLobby(object lobbyId)
        {
            // Manager LeaveLobby takes no args (leaves current)
            _manager.LeaveLobby();
        }

        public object GetLobbyOwner(object lobbyId)
        {
            return SteamMatchmaking.GetLobbyOwner(ToSteamID(lobbyId)).m_SteamID;
        }

        public string GetLobbyName(object? lobbyId = null)
        {
            CSteamID id;
            if (lobbyId != null) 
            {
                id = ToSteamID(lobbyId);
            }
            else if (_manager.CurrentLobby != null)
            {
                id = _manager.CurrentLobby.Value.ToSteamId();
            }
            else
            {
                id = CSteamID.Nil;
            }

            if (id == CSteamID.Nil) return "Lobby";

            var name = SteamMatchmaking.GetLobbyData(id, "name");
            if (string.IsNullOrEmpty(name)) return $"Lobby {id.m_SteamID}";
            return name;
        }

        public string GetLobbyData(object lobbyId, string key)
        {
            // Use generic SteamMatchmaking directly as Manager only returns struct
            return SteamMatchmaking.GetLobbyData(ToSteamID(lobbyId), key);
        }

        public string GetSteamIDString(object steamId)
        {
            if (steamId is CSteamID cid) return cid.m_SteamID.ToString();
            if (steamId is ulong ul) return ul.ToString();
            return steamId?.ToString() ?? "";
        }

        public void SetLobbyData(object lobbyId, string key, string value)
        {
             SteamMatchmaking.SetLobbyData(ToSteamID(lobbyId), key, value);
        }

        public int GetMemberCount(object lobbyId)
        {
            return SteamMatchmaking.GetNumLobbyMembers(ToSteamID(lobbyId));
        }

        public int GetMemberLimit(object lobbyId)
        {
            return SteamMatchmaking.GetLobbyMemberLimit(ToSteamID(lobbyId));
        }

        public object GetLobbyMemberByIndex(object lobbyId, int index)
        {
            return SteamMatchmaking.GetLobbyMemberByIndex(ToSteamID(lobbyId), index).m_SteamID;
        }

        public void SetLobbyMemberData(object lobbyId, string key, string value)
        {
            SteamMatchmaking.SetLobbyMemberData(ToSteamID(lobbyId), key, value);
        }

        public string GetLobbyMemberData(object lobbyId, object userId, string key)
        {
            return SteamMatchmaking.GetLobbyMemberData(ToSteamID(lobbyId), ToSteamID(userId), key);
        }

        public string GetLocalPersonaName()
        {
            return SteamFriends.GetPersonaName();
        }

        public string GetFriendPersonaName(object userId)
        {
            return SteamFriends.GetFriendPersonaName(ToSteamID(userId));
        }

        public bool CSteamIDEquals(object id1, object id2)
        {
            return ToSteamID(id1) == ToSteamID(id2);
        }

        public object CSteamIDNil()
        {
            return CSteamID.Nil.m_SteamID;
        }
    }
}
