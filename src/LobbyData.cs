using System;

namespace SteamLobbyLib
{
    [Serializable]
    public class LobbyData
    {
        public LobbyId Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int MaxPlayers { get; set; }
        public int CurrentPlayers { get; set; }
    }
}

