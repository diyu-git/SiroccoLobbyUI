namespace SiroccoLobby.Model
{
    public sealed class LobbyMember
    {
        public object? SteamId { get; set; }
        public string? Name { get; set; }
        public int Team { get; set; }
        public int CaptainIndex { get; set; }
        public bool IsHost { get; set; }
        public bool IsReady { get; set; }
    }
}
