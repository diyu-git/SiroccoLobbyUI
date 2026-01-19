using System;
using System.Collections.Generic;
using Steamworks;
// using System.Text.Json;
namespace SteamLobbyLib;

public partial class SteamLobbyManager
{
    private readonly ILobbyEvents _events;
    private readonly bool _enableLogging;

    private LobbyId? _currentLobbyId;
    private readonly List<LobbyData> _cachedLobbies = [];

    public LobbyId? CurrentLobby => _currentLobbyId;
    public IReadOnlyList<LobbyData> CachedLobbies => _cachedLobbies;
    public int LobbyCount => _cachedLobbies.Count;

    private readonly ITraceLogger _logger;

    public SteamLobbyManager(ILobbyEvents events, ITraceLogger logger, bool enableLogging = true)
    {
        _events = events;
        _enableLogging = enableLogging;
        _logger = logger;
        SteamCallbackBinder.Bind(events, this);
        Log("Manager", "SteamCallbackBinder bound.");
    }

    public void Initialize()
    {
        Log("Lifecycle", "Attempting SteamAPI initialization...");

        if (!SteamAPI.Init())
        {
            var hints = new List<string>();

            if (!SteamAPI.IsSteamRunning())
                hints.Add("Steam client is not running.");

            if (!Environment.Is64BitProcess)
                hints.Add("Process is not 64-bit. Steamworks requires x64 architecture.");

            if (!SteamAPI.RestartAppIfNecessary((AppId_t)480))
                hints.Add("App restart check failed. AppId may be incorrect or not set up properly.");

            var message = "SteamAPI failed to initialize.";
            if (hints.Count > 0)
                message += " Possible causes:\n - " + string.Join("\n - ", hints);

            throw new InvalidOperationException(message);
        }

        Log("Lifecycle", $"Steam initialized successfully. Steam running: {SteamAPI.IsSteamRunning()}");

        try 
        {
             var steamId = SteamUser.GetSteamID();
             var name = SteamFriends.GetFriendPersonaName(steamId);
             var appId = SteamUtils.GetAppID();
             Log("Lifecycle", $"Logged in as: {name} (SteamID: {steamId})");
             Log("Lifecycle", $"AppID: {appId}");
        } 
        catch (Exception e) 
        {
             Log("Lifecycle", $"Failed to get user details: {e.Message}");
        }
    }

    public void Shutdown()
    {
        SteamAPI.Shutdown();
        Log("Lifecycle", "Steam shutdown complete.");
    }

    public void Tick()
    {
        SteamAPI.RunCallbacks();
        //Log("Lifecycle", "SteamAPI callbacks ticked."); 
    }

    public void CreateLobby(int maxPlayers)
    {
        Log("Lobby", $"Creating lobby with maxPlayers={maxPlayers}");
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, maxPlayers);
    }

    public void JoinLobby(LobbyId lobbyId)
    {
        Log("Lobby", $"Joining lobby {lobbyId}");
        SteamMatchmaking.JoinLobby(lobbyId.ToSteamId());
    }

    public void LeaveLobby()
    {
        if (_currentLobbyId == null)
        {
            Log("Lobby", "No active lobby to leave.");
            return;
        }

        Log("Lobby", $"Leaving lobby {_currentLobbyId}");
        SteamMatchmaking.LeaveLobby(_currentLobbyId.Value.ToSteamId());
        _currentLobbyId = null;
    }

    public void UpdateCurrentLobbyMetadata(string key, string value)
    {
        if (_currentLobbyId == null)
        {
            Log("Lobby", "No active lobby to update.");
            return;
        }

        SetLobbyData(_currentLobbyId.Value, key, value);
    }

    private void SetLobbyData(LobbyId lobbyId, string key, string value)
    {
        Log("Lobby", $"Setting lobby data: {key} = {value} for {lobbyId}");
        SteamMatchmaking.SetLobbyData(lobbyId.ToSteamId(), key, value);
    }

    public void RequestLobbyList(int count = 10)
    {
        Log("Lobby", $"Requesting lobby list (max {count})");
        SteamMatchmaking.AddRequestLobbyListResultCountFilter(count);
        SteamMatchmaking.RequestLobbyList();
    }

    public void RequestLobbyListWithFilter(string key, string value)
    {
        Log("Lobby", $"Requesting lobby list with filter: {key} = {value}");
        SteamMatchmaking.AddRequestLobbyListStringFilter(key, value, ELobbyComparison.k_ELobbyComparisonEqual);
        RequestLobbyList();
    }

    public LobbyData GetLobbyData(LobbyId lobbyId)
    {
        var steamId = lobbyId.ToSteamId();
        var name = SteamMatchmaking.GetLobbyData(steamId, "name");
        var maxPlayers = SteamMatchmaking.GetLobbyMemberLimit(steamId);
        var currentPlayers = SteamMatchmaking.GetNumLobbyMembers(steamId);

        Log("Lobby", $"Hydrating LobbyData for {lobbyId}: name='{name}', players={currentPlayers}/{maxPlayers}");

        return new LobbyData
        {
            Id = lobbyId,
            Name = name,
            MaxPlayers = maxPlayers,
            CurrentPlayers = currentPlayers
        };
    }

    public void PrintCurrentLobbyMetadata()
    {
        if (_currentLobbyId == null)
        {
            Log("Lobby", "No active lobby to inspect.");
            return;
        }

        var steamId = _currentLobbyId.Value.ToSteamId();
        var keys = new[] {"name", "map", "mode", "version"};

        foreach (var key in keys)
        {
            var value = SteamMatchmaking.GetLobbyData(steamId, key);
            Log("Metadata", $"{key} = {value}");
        }
    }

    public void SimulateMemberChange(EChatMemberStateChange change)
    {
        if (_currentLobbyId == null)
        {
            Log("Lobby", "No active lobby to simulate member change.");
            return;
        }

        var fakeMember = LobbyId.FromSteamId(new CSteamID(123456789));
        Log("Simulate", $"Member {fakeMember} → {change} in {_currentLobbyId}");
        _events.OnLobbyMemberChanged(_currentLobbyId.Value, fakeMember, change);
    }

    // Method removed to avoid System.Text.Json dependency
    // public string ExportLobbyDataJson(LobbyId lobbyId) ...

    internal void SetCurrentLobby(LobbyId lobbyId)
    {
        _currentLobbyId = lobbyId;
        Log("Lifecycle", $"Current lobby set to {lobbyId}");
    }

    internal void SetCachedLobbies(List<LobbyData> lobbies)
    {
        _cachedLobbies.Clear();
        _cachedLobbies.AddRange(lobbies);
        Log("Lifecycle", $"Cached {lobbies.Count} lobbies.");
    }

    private void Log(string category, string message)
    {
        if (_enableLogging)
            _logger.Log(category, message);
    }
}