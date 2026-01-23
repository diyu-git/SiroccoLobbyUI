# Client Connection Flow

## Overview

This document explains how clients connect to a hosted game via Riptide P2P networking. This is critical for understanding the multiplayer architecture.

---

## Architecture

### Network Stack

```
┌─────────────────────────────────────┐
│  LobbyController (UI Layer)         │
│  - Captures host Steam ID           │
│  - Validates connection state       │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│  ProtoLobbyIntegration (Bridge)     │
│  - Reflection-based game interface  │
│  - Calls Mirror NetworkClient       │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│  Mirror.NetworkClient (Framework)   │
│  - Connect(string address)          │
│  - Ready(), AddPlayer()             │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│  Riptide SteamTransport             │
│  - SteamClient.Connect(string)      │
│  - Parses string → ulong → CSteamID │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│  Steam P2P Networking               │
│  - Establishes P2P connection       │
└─────────────────────────────────────┘
```

---

## Connection Flow

### 1. Lobby Browser → Join

**File**: `LobbyController.cs:JoinLobby()`

When a client clicks "Join" on a lobby in the browser:

```csharp
public void JoinLobby(object lobbyId)
{
    // CRITICAL: Capture host Steam ID BEFORE joining
    _state.HostSteamId = _steam.GetLobbyOwner(lobbyId)?.ToString() ?? "";
    
    _log.Msg($"[Client] Joining lobby hosted by SteamID64: {_state.HostSteamId}");
    
    _steam.JoinLobby(lobbyId);
}
```

**Why this matters**:
- `GetLobbyOwner()` returns the host's Steam ID as `object` (wrapping `ulong`)
- `.ToString()` converts to SteamID64 string format (e.g., `"76561198012345678"`)
- This is stored for later use in the connection phase

---

### 2. Lobby Entered → Connect to Game Server

**File**: `LobbyController.cs:OnLobbyEntered()`

After joining the Steam lobby, clients initiate the game connection:

```csharp
if (!_state.IsHost)
{
    // Validate we have the host address
    if (string.IsNullOrEmpty(_state.HostSteamId))
    {
        _log.Error("[Client] HostSteamId is null! Cannot connect.");
        return;
    }
    
    _log.Msg($"[Client] Initiating Riptide P2P connection to host {_state.HostSteamId}...");
    _protoLobby.ConnectToGameServer(_state.HostSteamId);
}
```

**What happens**:
1. `ConnectToGameServer(string)` is called with the host's SteamID64
2. This invokes `Mirror.NetworkClient.Connect(string address)` via reflection
3. Mirror passes the string to Riptide's SteamTransport
4. Riptide parses: `string` → `ulong.TryParse()` → `new CSteamID(ulong)` → `SteamConnection`

---

### 3. Ready Phase → Validate Connection

**File**: `LobbyController.cs:ToggleReady()`

Before allowing clients to ready up, we validate the connection:

```csharp
if (_state.IsLocalReady)
{
    // CRITICAL CHECK: Verify NetworkClient is connected
    if (!_protoLobby.IsConnected)
    {
        _log.Error("[Client] NetworkClient not connected! Cannot call Ready().");
        _state.IsLocalReady = false; // Revert
        _steam.SetLobbyMemberData(_state.CurrentLobby, "is_ready", "False");
        return;
    }
    
    // Connection verified, proceed with ready flow
    _protoLobby.CompleteProtoLobbyClient();
    _protoLobby.CallNetworkClientReady(_state.SelectedCaptainIndex, _state.SelectedTeam);
    _protoLobby.ValidatePlayersReadyForGameStart();
}
```

**Why this matters**:
- Prevents calling `NetworkClient.Ready()` before P2P connection is established
- Avoids silent failures that would block game start
- Provides clear error messages to the user

> Additional constraint (Mirror Auth): in some builds, Mirror will require the client to authenticate *before* it accepts `Ready()` / `AddPlayer()`.
> This mod guards that path; if you see log lines indicating ready was blocked due to authentication, wait for auth state to complete (or review the authenticator wiring described below).

---

## Mirror Auth (Authenticator) Flow

Mirror supports an optional *Authenticator* component attached to the NetworkManager. In this project we **force-trigger** the client auth hook via reflection because we are interfacing with the game's internal Mirror setup rather than owning the full transport/auth pipeline.

### Where it happens

**File**: `src/Mod/Services/NetworkIntegrationService.cs`

After a successful connect attempt (both local and remote), `NetworkIntegrationService` checks for a cached authenticator and calls its auth entry point:

- `GameReflectionBridge` caches:
  - `AuthenticatorInstance` (from the NetworkManager `authenticator` field)
  - `OnClientAuthenticateMethod` (method named `OnClientAuthenticate`)

- `NetworkIntegrationService` triggers auth:
  - `ConnectToLocalServer()` → `NetworkClient.ConnectLocalServer()` → **then** `authenticator.OnClientAuthenticate()`
  - `ConnectToRemoteServer(address)` → `NetworkClient.Connect(address)` → **then** `authenticator.OnClientAuthenticate()`

### Why we do this

In a typical Mirror project, the authenticator is invoked as part of the normal connect pipeline. Here we’re *hooking into* the game's existing Mirror objects via reflection, so we explicitly call `OnClientAuthenticate` when the authenticator is present.

### Ordering relative to Ready/AddPlayer

The intended order is:

1. Client joins Steam lobby and captures the host SteamID64 string.
2. Client calls `ConnectToGameServer(hostSteamId)`.
3. Network connect begins (Mirror → Transport → Steam P2P).
4. If an authenticator exists, we call `OnClientAuthenticate()`.
5. Only after the client is actually connected (`IsConnected == true`) should UI allow:
   - `NetworkClient.Ready()`
   - `NetworkClient.AddPlayer(...)`
   - `CompleteProtoLobbyClient()` (ProtoLobby flow completion)

In addition, if Mirror auth is enforced by the server, the client must be authenticated before `Ready()`/`AddPlayer()` are sent.
The mod logs and blocks those calls when `NetworkClient.isAuthenticated` is still false.

---

## Diagnostics: runtime object dumps (IL2CPP)

Many interesting game-side objects are IL2CPP and don’t have readable managed method bodies in decompiled output.
To understand *what the game is actually doing*, the mod includes a small reflection-based object graph dumper:

- Implementation: `src/Mod/Services/Helpers/ObjectDumper.cs`
- Used by services/patches to dump a bounded set of fields/properties at runtime
- Proto-lobby related dumps use the log prefix `"[ProtoDump]"`

If a dump is skipped due to an object instance not being found, that usually means the target type is not instantiated in the current game flow (common for `Il2CppWartide.Testing.*` classes).

### Failure modes / troubleshooting

- If auth is required and `OnClientAuthenticate` is never invoked, you’ll often see:
  - client appears connected but cannot ready/add player, or
  - host never sees the client as valid/ready.

- If reflection caching fails (authenticator field/method missing), connection may still succeed but auth-specific behavior won’t run.

> Note: the authenticator behavior is game-defined; this doc only describes how our mod triggers the entry point.

---

## Type Chain

### Why String?

The entire network stack uses `string` for addresses:

1. **Mirror.NetworkClient.Connect(string address)**
   - Public API expects string
   - Documented: https://mirror-networking.gitbook.io/docs/

2. **Riptide.SteamClient.Connect(string hostAddress)**
   - Public API expects string
   - Source: `Assets/RiptideSteamTransport/Transport/SteamClient.cs:91`
   - Internally parses to `ulong` then `CSteamID`

3. **SteamConnection(CSteamID steamId)**
   - Internal constructor, not called directly
   - Only used after Riptide has parsed the string

**Correct usage**:
```csharp
// Correct
string hostSteamId = GetLobbyOwner(lobbyId)?.ToString();
NetworkClient.Connect(hostSteamId);

// Wrong - would require manual CSteamID creation
CSteamID hostId = new CSteamID(GetLobbyOwner(lobbyId));
// No public API accepts CSteamID directly
```

---

## Common Issues

### Issue: Client tries to connect locally instead of P2P

**Symptom**: Logs show `"Server is NOT active"` or connection fails immediately

**Cause**: `ConnectToGameServer()` called without host address parameter

**Fix**: Ensure `JoinLobby()` captures and stores `HostSteamId` before joining

---

### Issue: Ready() fails silently

**Symptom**: Client clicks Ready but host never sees them as ready

**Cause**: `NetworkClient.Ready()` called before P2P connection established

**Fix**: Check `IsConnected` before calling Ready() (implemented in `ToggleReady()`)

---

### Issue: Invalid Steam ID format

**Symptom**: Connection fails with "Invalid host address" error

**Cause**: Steam ID not properly converted to string

**Fix**: Use `.ToString()` on the Steam ID object, not custom formatting

---

## Testing Checklist

### Connection Phase
- [ ] Client sees lobbies in browser
- [ ] Clicking "Join" logs: `"Joining lobby hosted by SteamID64: [number]"`
- [ ] Client enters lobby room view
- [ ] Connection attempt logs: `"Initiating Riptide P2P connection to host [number]"`
- [ ] No error: `"Server is NOT active"` (indicates local connection attempt)
- [ ] Riptide connection succeeds (check for connection success in logs)

### Ready Phase
- [ ] Client can select captain and team
- [ ] Ready button is clickable
- [ ] If clicked before connection: Error shown and ready state reverted
- [ ] Once connected: Ready succeeds
- [ ] Host sees client as ready in lobby

### Game Start Phase
- [ ] Host can start when all players ready
- [ ] Client receives game start signal
- [ ] Client loads into game with correct captain/team

---

## Code References

### Key Files

- **LobbyController.cs**: Client connection orchestration
  - `JoinLobby()`: Lines 59-70
  - `OnLobbyEntered()`: Lines 103-124
  - `ToggleReady()`: Lines 282-314

- **ProtoLobbyIntegration.cs**: Game integration via reflection
  - `ConnectToGameServer()`: Lines 452-507
  - `CallNetworkClientReady()`: Lines 958-1032
  - `IsConnected` property: Lines 24-38

- **LobbyState.cs**: State management
  - `HostSteamId`: Line 26

### External Dependencies

- **Mirror Networking**: https://github.com/vis2k/Mirror
  - NetworkClient.Connect() documentation
  
- **Riptide SteamTransport**: https://github.com/RiptideNetworking/SteamTransport
  - SteamClient.Connect() source code
  - Address parsing logic

---

## Future Improvements

### Connection Timeout
Currently, if P2P connection hangs, client waits indefinitely.

**Recommendation**: Add timeout in `OnUpdate()`:
```csharp
private float _connectionStartTime;
private const float CONNECTION_TIMEOUT = 10f;

// In OnLobbyEntered (client path)
_connectionStartTime = Time.time;

// In OnUpdate
if (_state.IsConnecting && Time.time - _connectionStartTime > CONNECTION_TIMEOUT)
{
    _log.Error("[Client] Connection timeout! Retrying...");
    _protoLobby.ConnectToGameServer(_state.HostSteamId);
    _connectionStartTime = Time.time;
}
```

### UI Feedback
Add connection status badges to `LobbyRoomView`:
- "Connecting..." (yellow)
- "Connected" (green)
- "Connection Failed - Retry" (red)

### Lobby Cleanup on Game Start
Client should disconnect from Steam lobby when game starts:
```csharp
// In client's game start flow
LeaveLobby(shutdownNetwork: false); // Keep Riptide connection, drop Steam lobby
```

---

## Revision History

- **2026-01-22**: Initial documentation
  - Documented client connection flow
  - Verified string type usage across entire stack
  - Added testing checklist and troubleshooting guide
