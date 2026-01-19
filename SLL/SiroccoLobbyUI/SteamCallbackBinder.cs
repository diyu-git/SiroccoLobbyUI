using System.Collections.Generic;
using Steamworks;

namespace SteamLobbyLib;

internal static class SteamCallbackBinder
{
    private static ILobbyEvents? _events;
    private static SteamLobbyManager? _manager;

    private static Callback<LobbyMatchList_t>? _onLobbyMatchList;
    private static Callback<LobbyEnter_t>? _onLobbyEnter;
    private static Callback<LobbyDataUpdate_t>? _onLobbyDataUpdate;
    private static Callback<LobbyChatUpdate_t>? _onLobbyChatUpdate;

    public static void Bind(ILobbyEvents events, SteamLobbyManager manager)
    {
        _events = events;
        _manager = manager;

        _onLobbyMatchList = Callback<LobbyMatchList_t>.Create(result =>
        {
            var lobbies = new List<LobbyData>();
            for (var i = 0; i < result.m_nLobbiesMatching; i++)
            {
                var steamId = SteamMatchmaking.GetLobbyByIndex(i);
                var lobbyId = LobbyId.FromSteamId(steamId);
                var data = manager.GetLobbyData(lobbyId);
                lobbies.Add(data);
            }

            manager.SetCachedLobbies(lobbies);
            events.OnLobbyListReceived(lobbies);
        });

        _onLobbyEnter = Callback<LobbyEnter_t>.Create(result =>
        {
            var lobbyId = LobbyId.FromSteamId((CSteamID)result.m_ulSteamIDLobby);
            manager.SetCurrentLobby(lobbyId);
            events.OnLobbyJoined(lobbyId);
        });

        _onLobbyDataUpdate = Callback<LobbyDataUpdate_t>.Create(result =>
        {
            if (result.m_bSuccess != 1) return;
            var lobbyId = LobbyId.FromSteamId((CSteamID)result.m_ulSteamIDLobby);
            events.OnLobbyDataUpdated(lobbyId);
        });

        _onLobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(result =>
        {
            var lobbyId = LobbyId.FromSteamId((CSteamID)result.m_ulSteamIDLobby);
            var memberId = LobbyId.FromSteamId((CSteamID)result.m_ulSteamIDUserChanged);
            var change = (EChatMemberStateChange)result.m_rgfChatMemberStateChange;
            events.OnLobbyMemberChanged(lobbyId, memberId, change);
        });
    }
}
