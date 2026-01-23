using System;

namespace SiroccoLobby.Model
{
    public sealed class LobbySummary
    {
        public object LobbyId { get; set; } = null!;
        public string Name { get; set; } = string.Empty;
        public int CurrentPlayers { get; set; }
        public int MaxPlayers { get; set; }
        public bool IsFull { get; set; }
    }
}
