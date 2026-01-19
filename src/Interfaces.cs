using System.Collections.Generic;
using Steamworks;

namespace SteamLobbyLib;

public interface ILobbyEvents
{
    public void OnLobbyListReceived(List<LobbyData> lobbies);
    public void OnLobbyJoined(LobbyId lobbyId);
    public void OnLobbyDataUpdated(LobbyId lobbyId);
    public void OnLobbyMemberChanged(LobbyId lobbyId, LobbyId memberId, EChatMemberStateChange change);
}

public interface ITraceLogger
{
    void Log(string category, string message);
}