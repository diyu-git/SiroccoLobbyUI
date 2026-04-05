namespace SiroccoLobby.Model
{
    public sealed class LobbyMember
    {
        public object? SteamId { get; set; }
        public string? Name { get; set; }
        public int Team { get; set; }
        public int CaptainIndex { get; set; } = -1;
        public string? CaptainLabel { get; set; } // Captain name/ID for P2P players
        public bool IsHost { get; set; }
        public bool IsReady { get; set; }
        public bool IsP2POnly { get; set; } // Connected via P2P but not in Steam lobby
    }
}
