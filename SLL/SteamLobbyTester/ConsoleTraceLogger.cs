using SteamLobbyLib;

namespace SteamLobbyTester;

internal class ConsoleTraceLogger(string prefix = "Trace") : ITraceLogger
{
    public void Log(string category, string message)
    {
        var stamp = DateTime.Now.ToString("HH:mm:ss.fff");
        Console.WriteLine($"[{prefix} {category} @ {stamp}] {message}");
    }
}