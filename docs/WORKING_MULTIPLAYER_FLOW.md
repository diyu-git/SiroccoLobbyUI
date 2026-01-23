# Working Multiplayer Connection Flow

**Status**: ✅ **FULLY FUNCTIONAL** - Clients successfully connect to host via Steam P2P

This document describes the complete, working multiplayer lobby and connection flow for the Sirocco Lobby UI mod.

> **See also**: [CLIENT_CONNECTION_FLOW.md](CLIENT_CONNECTION_FLOW.md) for detailed technical architecture of the Mirror/Steam P2P transport layer.

---

## Overview

The mod provides a Steam-integrated lobby system that allows players to:
1. Create and browse Steam lobbies
2. Join lobbies and select team/captain
3. Connect to the host's game server via Steam P2P transport
4. Ready up and start the game

The entire flow works seamlessly from lobby browsing through game start.

---

## Key Components

### 1. Steam Lobby Layer (SLL)
- **Purpose**: Low-level Steam API wrapper
- **Location**: `SLL/SteamLobbyLib/`
- **Key Fix**: `LobbyData.OwnerId` captures host Steam ID when lobby list is received

### 2. Lobby Controller
- **Purpose**: Business logic for lobby operations
- **Location**: `src/Mod/Controller/LobbyController.cs`
- **Responsibilities**: 
  - Lobby creation/joining
  - Team/captain selection
  - Ready state management
  - Game start coordination

### 3. Network Integration Service
- **Purpose**: Handles Steam P2P connection setup
- **Location**: `src/Mod/Services/NetworkIntegrationService.cs`
- **Key Fix**: Sets `NetworkAddress` **field** (not property) to host Steam ID before calling `StartClientOnly()`

---

## Complete Flow: From Lobby Browser to Game

### Phase 1: Host Creates Lobby

```
1. Host presses F5 → Opens lobby UI
2. Host clicks "Create Lobby"
   └─ StartSinglePlayer() → Starts Mirror server + Steam P2P transport
   └─ CreateLobby() → Creates Steam lobby for matchmaking
3. OnLobbyJoined callback fires
   └─ Sets lobby metadata: "name", "host_steam_id"
   └─ OnLobbyEntered() → Switches UI to Room view
4. Host is now in lobby room, waiting for clients
```

**Code Path**:
```
LobbyController.CreateLobby()
  ├─ ProtoLobby.TriggerSinglePlayer() [starts game server]
  └─ Steam.CreateLobby()
       └─ OnLobbyJoined()
            ├─ SetLobbyData("host_steam_id", localSteamId)
            └─ OnLobbyEntered()
```

---

### Phase 2: Client Browses Lobbies

```
1. Client presses F5 → Opens lobby UI (Browser view)
2. Client clicks "Refresh"
   └─ RequestLobbyList() → Asks Steam for available lobbies
3. OnLobbyListReceived callback fires
   └─ For each lobby: GetLobbyData(lobbyId)
       ├─ Retrieves: name, player count, max players
       └─ **CRITICAL**: Captures OwnerId (host Steam ID) via GetLobbyOwner()
   └─ RebuildLobbyCache() → Populates UI list
4. Client sees lobbies with host Steam IDs populated
```

**Code Path**:
```
LobbyController.RefreshLobbyList()
  └─ Steam.RequestLobbyList()
       └─ [Steam API callback: LobbyMatchList_t]
            └─ For each lobby:
                 GetLobbyByIndex(i) → Get lobby CSteamID
                 GetLobbyData(lobbyId):
                   ├─ GetLobbyData(steamId, "name")
                   ├─ GetLobbyMemberLimit(steamId)
                   ├─ GetNumLobbyMembers(steamId)
                   └─ GetLobbyOwner(steamId) → Returns host CSteamID ✅
            └─ OnLobbyListReceived(lobbies)
                 └─ RebuildLobbyCache()
                      └─ Creates LobbySummary with HostSteamId
```

**Key Fix**: `LobbyData.OwnerId` is now populated when the lobby list is retrieved, ensuring host Steam ID is available before joining.

---

### Phase 3: Client Joins Lobby

```
1. Client clicks "Join" on a lobby
   └─ JoinLobby(lobbyId, hostSteamId) [hostSteamId from LobbySummary]
   └─ Steam.JoinLobby(lobbyId)
2. OnLobbyJoined callback fires
   └─ OnLobbyEntered()
       ├─ RefreshLobbyData() → Gets authoritative host Steam ID
       │   ├─ GetLobbyData(lobbyId, "host_steam_id") [metadata]
       │   └─ OR GetLobbyOwner(lobbyId) [fallback]
       │   └─ Sets _state.HostSteamId ✅
       ├─ Switches UI to Room view
       └─ Sets initial team/captain/ready state
3. Client is now in lobby room, can see host and select team/captain
```

**Code Path**:
```
UI: Join button clicked
  └─ LobbyController.JoinLobby(lobbyId, lobby.HostSteamId)
       └─ Steam.JoinLobby(lobbyId)
            └─ [Steam API callback: LobbyEnter_t]
                 └─ OnLobbyEntered(lobbyId)
                      └─ RefreshLobbyData()
                           └─ _state.HostSteamId = GetOwnerFromLobby() ✅
```

**Key Point**: After joining, `RefreshLobbyData()` gets the authoritative host Steam ID that will be used for P2P connection.

---

### Phase 4: Client Connects to Game Server (AUTO)

```
1. OnUpdate() runs every frame, checking conditions:
   ├─ Is client (not host)? ✅
   ├─ In a lobby? ✅
   ├─ ProtoLobby ready (F5 pressed)? ✅
   ├─ Host Steam ID valid? ✅
   ├─ Not connected yet? ✅
   └─ Not attempted yet? ✅
2. All conditions met → ConnectToGameServer(_state.HostSteamId)
   └─ TryIntegrateWithProtoLobby(hostSteamId)
       ├─ EnableSteamP2P() [if tester available]
       ├─ **CRITICAL**: Set NetworkAddress FIELD to hostSteamId
       │   networkManagerType.GetField("NetworkAddress").SetValue(host)
       ├─ StartClientOnly() → Initiates Mirror client connection
       │   └─ Mirror reads NetworkAddress field
       │   └─ Passes to SteamP2PTransport.ClientConnect(hostSteamId)
       │   └─ Steam P2P connection established ✅
       └─ SetGameAuthority to ClientOnly mode
3. Connection established, client can now ready up
```

**Code Path**:
```
LobbyController.OnUpdate()
  └─ IF conditions met:
       └─ ProtoLobby.ConnectToGameServer(hostSteamId)
            └─ NetworkIntegrationService.ConnectToGameServer(hostSteamId)
                 └─ TryIntegrateWithProtoLobby(hostSteamId)
                      ├─ EnableSteamP2P()
                      ├─ networkManagerInstance.GetField("NetworkAddress")
                      │   .SetValue(hostSteamId) ✅
                      ├─ StartClientOnly()
                      │   └─ NetworkClient.Connect(networkAddress)
                      │        └─ SteamP2PTransport.ClientConnect(hostSteamId) ✅
                      └─ GameAuthority.SetClientOnlyMode()
```

**Key Fix**: We now set the `NetworkAddress` **field** directly (not property), matching exactly how the game's `ConnectToSteamID` method works. This ensures Mirror passes the correct Steam ID to the transport layer.

---

### Phase 5: Ready Up and Game Start

```
HOST:
1. Host selects team/captain
2. Host clicks "Ready" (waits for clients to ready)
3. All clients ready → Host clicks "Start Game"
   └─ CompleteProtoLobbyServer()
       ├─ Sends RPC to all connected clients
       └─ Transitions host to gameplay
   └─ Closes lobby UI (_state.GameHasStarted = true)

CLIENT:
1. Client selects team/captain
2. Client clicks "Ready"
   └─ Validation: Is NetworkClient connected? ✅
   └─ CallNetworkClientReady() → Sends ready state to host
3. Host starts game → Client receives RPC
   └─ CompleteProtoLobbyClient() called
       └─ OnClientGameStarted event fires
            ├─ Closes lobby UI
            ├─ Exits Steam lobby
            └─ Sets GameHasStarted = true (prevents F5 reopening)
4. Client transitions to gameplay
```

**Code Path**:
```
HOST:
  StartGame()
    ├─ CompleteProtoLobbyServer() [sends RPC via Mirror/Steam P2P]
    ├─ ExitSteamLobby()
    ├─ _state.ShowDebugUI = false
    └─ _state.GameHasStarted = true

CLIENT:
  [Receives RPC from host]
    └─ CompleteProtoLobbyClient()
         └─ OnClientGameStarted event
              └─ LobbyController.OnClientGameStarted()
                   ├─ _state.ShowDebugUI = false
                   ├─ _state.GameHasStarted = true
                   ├─ ExitSteamLobby()
                   └─ ClearLobbyState()
```

---

## Critical Fixes That Made It Work

### 1. **Host Steam ID in Lobby List** (SLL Layer)
**Problem**: `GetLobbyOwner()` was returning `0` when called from lobby browser  
**Root Cause**: Owner information wasn't being captured when lobby list was retrieved  
**Fix**: Added `OwnerId` to `LobbyData` class, populated via `SteamMatchmaking.GetLobbyOwner()` in `GetLobbyData()`

**Files Changed**:
- `SLL/SteamLobbyLib/LobbyData.cs` - Added `OwnerId` property
- `SLL/SteamLobbyLib/SteamLobbyManager.cs` - Populate `OwnerId` in `GetLobbyData()`

### 2. **NetworkAddress Field Access** (Connection Layer)
**Problem**: Mirror was using "localhost" instead of host Steam ID  
**Root Cause**: We were trying to set `NetworkAddress` property, but IL2CPP requires setting the field directly  
**Fix**: Changed to use `GetField("NetworkAddress")` matching the game's exact implementation

**Files Changed**:
- `src/Mod/Services/NetworkIntegrationService.cs` - Changed from property to field access

### 3. **Wait for Valid Host Steam ID** (Connection Logic)
**Problem**: Connection attempted before host Steam ID was available  
**Root Cause**: Timing issue between lobby join and data refresh  
**Fix**: Added validation in `OnUpdate()` to wait for valid host Steam ID before connecting

**Files Changed**:
- `src/Mod/Controller/LobbyController.cs` - Added host Steam ID validation in connection logic

### 4. **UI Cleanup on Game Start** (Polish)
**Problem**: Lobby UI stayed open after game started  
**Fix**: Added `OnClientGameStarted` event to close UI and prevent F5 reopening

**Files Changed**:
- `src/Mod/Services/ProtoLobbyIntegration.cs` - Added `OnClientGameStarted` event
- `src/Mod/Controller/LobbyController.cs` - Subscribe to event and handle cleanup
- `src/Mod/Model/LobbyState.cs` - Added `GameHasStarted` flag
- `src/Mod/Plugin.cs` - Check flag in F5 handler

---

## Testing Checklist

### ✅ Lobby Browser
- [x] Host creates lobby → appears in Steam lobby list
- [x] Client refreshes → sees host's lobby
- [x] Lobby shows correct player count
- [x] Lobby shows host name
- [x] Host Steam ID is populated (not 0)

### ✅ Lobby Join
- [x] Client clicks Join → enters lobby room
- [x] Client sees host in member list
- [x] Client can select team/captain
- [x] Steam lobby metadata syncs correctly

### ✅ Network Connection
- [x] Client auto-connects when ProtoLobby ready (F5 pressed)
- [x] Connection logs show correct host Steam ID (not "localhost")
- [x] Steam P2P transport receives valid Steam ID
- [x] Mirror connection establishes successfully
- [x] No "Invalid Steam ID format: localhost" error

### ✅ Ready and Game Start
- [x] Client can ready up after connection
- [x] Host sees client ready state
- [x] Host can start game when all ready
- [x] Client receives game start RPC
- [x] Both host and client UIs close
- [x] Game transitions to gameplay

### ✅ UI Behavior
- [x] F5 opens/closes lobby UI in lobby
- [x] F5 disabled after game starts
- [x] UI shows correct connection status
- [x] No errors in logs during full flow

---

## Network Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                         STEAM LAYER                          │
│  - Steam Matchmaking (lobby list, join, metadata)           │
│  - Steam P2P Transport (game networking)                    │
└──────────────────────────┬──────────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────────┐
│                     SLL (Steam Lobby Lib)                    │
│  - SteamLobbyManager: Core Steam API operations             │
│  - LobbyData: Captures lobby info + owner Steam ID          │
│  - SteamCallbackBinder: Routes Steam callbacks              │
└──────────────────────────┬──────────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────────┐
│                      MOD CONTROLLER LAYER                     │
│  - LobbyController: Business logic, lobby operations         │
│  - NetworkIntegrationService: Sets up Mirror/Steam P2P       │
│  - LobbyState: Shared state (host ID, ready status, etc.)   │
└──────────────────────────┬──────────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────────┐
│                    GAME INTEGRATION LAYER                     │
│  - ProtoLobbyIntegration: Facade for game reflection        │
│  - GameReflectionBridge: Finds game types/methods           │
│  - NetworkManager: Game's Mirror networking (IL2CPP)         │
│  - SteamP2PTransport: Mirror transport using Steam P2P       │
└───────────────────────────────────────────────────────────────┘
```

---

## Data Flow: Host Steam ID

```
1. LOBBY LIST RETRIEVAL (Steam API)
   Steam sends lobby list → GetLobbyByIndex() → GetLobbyOwner()
                                                      ↓
   SteamLobbyManager.GetLobbyData() captures OwnerId
                                                      ↓
                            LobbyData { OwnerId: 76561198023662509 }
                                                      ↓
2. LOBBY CACHE POPULATION (Mod Layer)
   LobbyController.RebuildLobbyCache() reads LobbyData.OwnerId
                                                      ↓
                    LobbySummary { HostSteamId: "76561198023662509" }
                                                      ↓
3. UI DISPLAY
   LobbyBrowserView shows lobby with host Steam ID
   Client clicks Join → Passes HostSteamId to JoinLobby()
                                                      ↓
4. POST-JOIN CONFIRMATION
   OnLobbyEntered() → RefreshLobbyData() → _state.HostSteamId
   (Gets authoritative value from joined lobby)
                                                      ↓
5. CONNECTION
   OnUpdate() checks _state.HostSteamId → ConnectToGameServer(hostSteamId)
                                                      ↓
   NetworkManager.NetworkAddress FIELD = "76561198023662509"
                                                      ↓
   StartClientOnly() → NetworkClient.Connect(NetworkAddress)
                                                      ↓
   SteamP2PTransport.ClientConnect("76561198023662509") ✅
```

---

## Logs to Watch For Success

### Host Side
```
[Host] Game server started (Server mode via StartSinglePlayer)
[Events] Joined lobby: 109775241607912814
[Host] Set lobby name: Civi's Lobby
[Host] Set lobby data: host_steam_id = 76561198023662509
```

### Client Side (Lobby Join)
```
[Client] Joining lobby hosted by SteamID64: 76561198023662509
[Events] Joined lobby: 109775241607912814
[RefreshLobbyData] Host Steam ID: '76561198023662509' (IsHost: False)
[Client] Will auto-connect to Mirror/Steam P2P once ProtoLobby is ready (host: 76561198023662509)
```

### Client Side (Connection)
```
[Client] ProtoLobby ready - connecting to Mirror/Steam P2P (host: 76561198023662509)...
[NetworkIntegrationService] NetworkAddress field type: String
[NetworkIntegrationService] Current NetworkAddress value: 'localhost'
[NetworkIntegrationService] Setting to value: '76561198023662509'
[NetworkIntegrationService] ✓ Set NetworkAddress field to: '76561198023662509'
[NetworkIntegrationService] Calling StartClientOnly...
[NetworkIntegrationService] ✓ GameAuthority set to ClientOnly mode
[NetworkIntegrationService] ✓ Steam P2P connection sequence completed!
[NetworkIntegrationService] Connected via IntegrateWithProtoLobby to: 76561198023662509
```

### Game Start
```
HOST:
[Host] Starting Game...
[Host] Completing ProtoLobby - sending game start RPC to clients via Mirror/Steam P2P...

CLIENT:
[ProtoLobbyIntegration] Lobby client completed - game starting.
[Client] Game starting - closing lobby UI and cleaning up...
```

---

## Known Limitations

1. **Captain Mode**: Draft system works but needs testing with 4+ players
2. **Reconnection**: No support for reconnecting to ongoing games
3. **Error Recovery**: Limited handling if host disconnects during lobby
4. **UI Reopening**: F5 is permanently disabled after game start (by design, prevents issues)

---

## Future Enhancements

- [ ] Add reconnection support for dropped clients
- [ ] Better error messages for connection failures
- [ ] Host migration if host leaves lobby
- [ ] Save/load team presets
- [ ] Lobby chat integration
- [ ] Spectator mode support

---

## Conclusion

The multiplayer connection flow is **fully functional** from lobby browsing through game start. The key breakthrough was capturing the host Steam ID at the right moment (during lobby list retrieval) and setting it correctly (as a field, not property) before initiating the Mirror client connection.

All major components work together:
- ✅ Steam lobby browsing and joining
- ✅ Host Steam ID propagation through all layers
- ✅ Steam P2P connection establishment
- ✅ Mirror networking integration
- ✅ Ready state synchronization
- ✅ Game start coordination
- ✅ UI lifecycle management

**Status**: Ready for production use! 🎉
