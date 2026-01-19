using SteamLobbyLib;
using Steamworks;

namespace SteamLobbyTester;

internal class SteamLobbyTester : ILobbyEvents
{
    private SteamLobbyManager _manager = null!;
    private readonly ConsoleTraceLogger _logger = new("Tester");

    private static void Main()
    {
        var program = new SteamLobbyTester();
        program.Run();
    }

    private void Run()
    {
        Console.CancelKeyPress += (_, e) =>
        {
            Console.WriteLine("[Shutdown] Ctrl+C detected. Cleaning up...");
            _manager?.Shutdown();
            SteamAPI.Shutdown();
            e.Cancel = true;
            Environment.Exit(0);
        };

        _manager = new SteamLobbyManager(this, _logger, enableLogging: true);
        _manager.Initialize();
        _manager.RequestLobbyList();

        Console.WriteLine("Commands: create, join <index>, leave, update <key> <value>, tick, list, meta, simulate <change>, export <index>, exit");

        while (true)
        {
            Console.Write("> ");
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(input)) continue;

            var args = input.Split(' ', 3);
            var command = args[0].ToLower();

            switch (command)
            {
                case "create":
                    _manager.CreateLobby(8);
                    break;
                case "join":
                    if (args.Length < 2 || !int.TryParse(args[1], out var joinIndex) || joinIndex < 0 || joinIndex >= _manager.CachedLobbies.Count)
                    {
                        Console.WriteLine("Usage: join <index>");
                        break;
                    }
                    _manager.JoinLobby(_manager.CachedLobbies[joinIndex].Id);
                    break;
                case "leave":
                    _manager.LeaveLobby();
                    break;
                case "update":
                    if (args.Length < 3)
                    {
                        Console.WriteLine("Usage: update <key> <value>");
                        break;
                    }
                    _manager.UpdateCurrentLobbyMetadata(args[1], args[2]);
                    break;
                case "tick":
                    _manager.Tick();
                    break;
                case "list":
                    Console.WriteLine($"Cached lobbies ({_manager.LobbyCount}):");
                    for (int i = 0; i < _manager.CachedLobbies.Count; i++)
                    {
                        var lobby = _manager.CachedLobbies[i];
                        Console.WriteLine($"[{i}] {lobby.Id}: {lobby.Name} ({lobby.CurrentPlayers}/{lobby.MaxPlayers})");
                    }
                    break;
                case "meta":
                    _manager.PrintCurrentLobbyMetadata();
                    break;
                case "simulate":
                    if (args.Length < 2 || !Enum.TryParse<EChatMemberStateChange>(args[1], true, out var change))
                    {
                        Console.WriteLine("Usage: simulate <stateChange>");
                        break;
                    }
                    _manager.SimulateMemberChange(change);
                    break;
                case "export":
                    if (args.Length < 2 || !int.TryParse(args[1], out var exportIndex) || exportIndex < 0 || exportIndex >= _manager.CachedLobbies.Count)
                    {
                        Console.WriteLine("Usage: export <index>");
                        break;
                    }
                    var json = _manager.ExportLobbyDataJson(_manager.CachedLobbies[exportIndex].Id);
                    Console.WriteLine(json);
                    break;
                case "exit":
                    _manager.Shutdown();
                    SteamAPI.Shutdown();
                    return;
                default:
                    Console.WriteLine("Commands: create, join <index>, leave, update <key> <value>, tick, list, meta, simulate <change>, export <index>, exit");
                    break;
            }

            Thread.Sleep(50);
        }
    }
    public void OnLobbyListReceived(List<LobbyData> lobbies)
    {
        _logger.Log("Event", $"Lobby list received: {lobbies.Count} lobbies");
        for (var i = 0; i < lobbies.Count; i++)
        {
            var lobby = lobbies[i];
            _logger.Log("Event", $"  [{i}] {lobby.Id} — {lobby.Name} ({lobby.CurrentPlayers}/{lobby.MaxPlayers})");
        }
    }

    public void OnLobbyJoined(LobbyId lobbyId)
    {
        _logger.Log("Event", $"Joined lobby: {lobbyId}");
    }

    public void OnLobbyDataUpdated(LobbyId lobbyId)
    {
        _logger.Log("Event", $"Lobby data updated: {lobbyId}");
    }

    public void OnLobbyMemberChanged(LobbyId lobbyId, LobbyId memberId, EChatMemberStateChange change)
    {
        _logger.Log("Event", $"Member change in {lobbyId}: {memberId} → {change}");
    }
}