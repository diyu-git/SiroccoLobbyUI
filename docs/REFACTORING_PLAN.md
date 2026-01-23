# Refactoring Plan: God Classes and Single Responsibility Violations

**Status**: 📋 **PLANNING** - System is working, we have a stable baseline on git

**Last Updated**: January 23, 2026

---

## Overview

The Sirocco Lobby UI mod is fully functional, but several classes have grown into "god classes" that violate the Single Responsibility Principle. This document outlines a systematic refactoring plan to improve maintainability without breaking existing functionality.

---

## Identified God Classes

### 1. **LobbyController.cs** (966 lines, 35+ methods) 🔴 CRITICAL

**Current Responsibilities** (too many!):
- Lobby lifecycle (create, join, leave)
- Lobby data retrieval and caching
- Steam event handling (ILobbyEvents)
- Member list management
- Captain mode orchestration
- Ready state management  
- Team/captain selection
- Network connection coordination
- Steam ID utility operations
- Game start coordination

**Single Responsibility Violations**:
- ❌ Mixes business logic with data access
- ❌ Handles both UI coordination AND low-level Steam operations
- ❌ Contains utility methods that don't use controller state
- ❌ Combines multiple distinct feature areas (captain mode, ready state, etc.)

**Proposed Extraction**:
```
LobbyController (Orchestrator)
├── LobbyDataService          ← Data retrieval/caching
├── CaptainModeService        ← Captain draft logic
├── ReadyStateService         ← Ready validation/coordination
├── LobbyLifecycleService     ← Create/join/leave/cleanup
└── SteamIdHelpers (static)   ← Utility methods
```

---

### 2. **NetworkIntegrationService.cs** (776 lines) 🔴 CRITICAL

**Current Responsibilities**:
- P2P connection establishment
- Steam transport configuration
- Mirror authenticator handling
- Network state validation
- Reflection-based network manager access
- Connection retry logic

**Single Responsibility Violations**:
- ❌ Mixes low-level transport config with high-level connection logic
- ❌ Authentication logic intertwined with connection logic
- ❌ Large methods with multiple concerns

**Proposed Extraction**:
```
NetworkIntegrationService (Coordinator)
├── P2PConnectionService      ← Connection establishment
├── AuthenticationService     ← Authenticator handling
└── TransportConfigService    ← Transport setup/validation
```

---

### 3. **LobbyRoomView.cs** (615 lines) 🟡 HIGH PRIORITY

**Current Responsibilities**:
- Main lobby room UI rendering
- Host controls (start game button)
- Player list rendering
- Captain mode UI (draft interface)
- Ready button state
- Team/captain selection UI

**Single Responsibility Violations**:
- ❌ Monolithic UI rendering method
- ❌ Combines multiple distinct UI features in one class
- ❌ Difficult to test individual features

**Proposed Extraction**:
```
LobbyRoomView (Composition Root)
├── LobbyRoomMainView         ← Host controls, player list
├── CaptainModeView           ← Draft UI, captain assignment
└── ReadyStateView            ← Ready button, validation messages
```

---

## Specific Method Extractions

### LobbyController God Methods

#### **OnUpdate()** (~76 lines) - Multiple Concerns
```csharp
// Current: One method does everything
public void OnUpdate()
{
    // Auto-connection logic (20+ lines)
    if (!_state.IsHost && _state.CurrentLobby != null && _protoLobby != null...)
    
    // Native -> UI polling sync (30+ lines)  
    if (_protoLobby != null && _protoLobby.IsReady...)
    
    // View state handling
    if (_state.ViewState == LobbyUIState.Browser...)
}
```

**Proposed Extraction**:
```csharp
// New: Delegate to focused services
public void OnUpdate()
{
    _connectionAutoConnector.CheckAndAutoConnect();
    _nativeStateSynchronizer.SyncFromNativeState();
    // View-specific logic remains
}
```

#### **ToggleReady()** (~85 lines) - Complex Branching
- Split into: ValidateReadyState(), ApplyHostReady(), ApplyClientReady()
- Extract network validation to ReadyStateService

#### **OnLobbyEntered()** (~60 lines) - Initialization Overload
- Extract: InitializeLobbyState(), EstablishConnection(), SyncInitialData()

---

## Refactoring Strategy: Incremental Migration

### Phase 1: **Create Services (Non-Breaking)**
1. Create new service classes alongside existing code
2. Services are **additive** - old code still works
3. Add comprehensive tests for new services
4. No changes to callers yet

**Example**:
```csharp
// New service (created, not used yet)
public class LobbyDataService
{
    public void RefreshLobbyData(object lobbyId) { /* extracted logic */ }
    public void RebuildLobbyCache() { /* extracted logic */ }
}

// Old code still in LobbyController
public void RefreshLobbyData() { /* original logic remains */ }
```

### Phase 2: **Add Adapter Layer (Backwards Compatible)**
1. LobbyController creates new services internally
2. Delegate to services but keep same public API
3. External callers see no changes
4. Validate behavior is identical

**Example**:
```csharp
public class LobbyController
{
    private readonly LobbyDataService _lobbyData; // NEW
    
    public void RefreshLobbyData()
    {
        // Delegate to service, but keep same signature
        _lobbyData.RefreshLobbyData(_state.CurrentLobby);
    }
}
```

### Phase 3: **Migrate Callers (Incremental)**
1. Update callers one-by-one to use services directly
2. Test each migration
3. Keep old methods marked `[Obsolete]` temporarily
4. Monitor for breakage

### Phase 4: **Remove Old Code (Final Cleanup)**
1. Once all callers migrated, remove old methods
2. Update documentation
3. Commit refactored architecture

---

## Service Boundaries and Responsibilities

### Proposed Services

#### **ILobbyDataService**
```csharp
public interface ILobbyDataService
{
    void RefreshLobbyData(object lobbyId);
    void RebuildLobbyCache();
    void UpdateLobbySummary(object lobbyId);
    void AddPendingUpdate(ulong lobbyId);
    void ProcessPendingUpdates();
}
```
**Responsibility**: Steam lobby data retrieval, caching, and summary management

#### **ICaptainModeService**
```csharp
public interface ICaptainModeService
{
    void ToggleCaptainMode(bool enabled);
    void AssignCaptain(int team, string steamId);
    void PickPlayer(string steamId);
    bool IsComplete { get; }
    CaptainModePhase CurrentPhase { get; }
}
```
**Responsibility**: Captain draft orchestration and state management

#### **IReadyStateService**
```csharp
public interface IReadyStateService
{
    void ToggleReady();
    bool CanReady();
    string? GetReadyBlockReason();
    void ApplyReadyState(bool isReady);
}
```
**Responsibility**: Ready state validation and network ready coordination

#### **ILobbyLifecycleService**
```csharp
public interface ILobbyLifecycleService
{
    void CreateLobby();
    void JoinLobby(object lobbyId, string hostSteamId);
    void LeaveLobby(LobbyEndMode mode);
    void CleanupOnGameStart();
}
```
**Responsibility**: Lobby creation, joining, leaving, and cleanup orchestration

#### **Static: SteamIdHelpers**
```csharp
public static class SteamIdHelpers
{
    public static string GetSteamIdString(ISteamLobbyService steam);
    public static ulong GetSteamIdULong(ISteamLobbyService steam);
    public static bool IsLocalSteamId(ISteamLobbyService steam, object? steamId);
}
```
**Responsibility**: Steam ID conversion and comparison utilities

---

## Benefits of Refactoring

### Improved Maintainability
- ✅ Each class has one clear purpose
- ✅ Easier to locate bugs (know which service to check)
- ✅ Reduced cognitive load when reading code

### Better Testability
- ✅ Services can be unit tested in isolation
- ✅ Mock dependencies easily
- ✅ Test complex scenarios without full integration setup

### Enhanced Extensibility  
- ✅ Add new features to specific services
- ✅ Swap implementations (e.g., different ready validation rules)
- ✅ Reuse services across different controllers

### Reduced Coupling
- ✅ Services depend on interfaces, not concrete implementations
- ✅ Can evolve services independently
- ✅ Clearer dependency graph

---

## Risk Mitigation

### Keep System Working
- ✅ Have stable baseline on git (commit: 90886c9)
- ✅ Refactor incrementally, never "big bang"
- ✅ Run integration tests after each phase
- ✅ Can roll back to known-good state

### Validate Behavior
- ✅ Extensive logging already in place
- ✅ Compare logs before/after refactoring
- ✅ Manual testing of all flows
- ✅ Keep WORKING_MULTIPLAYER_FLOW.md as test script

### Maintain Backwards Compatibility
- ✅ Use `[Obsolete]` attributes during migration
- ✅ Keep old APIs until fully migrated
- ✅ Document breaking changes clearly

---

## Implementation Order (Suggested)

### Priority 1: High Value, Low Risk
1. **SteamIdHelpers** (simple utility extraction)
2. **LobbyDataService** (clear boundary, well-defined)

### Priority 2: Medium Complexity  
3. **CaptainModeService** (isolated feature)
4. **ReadyStateService** (complex but testable)

### Priority 3: Complex, High Impact
5. **LobbyLifecycleService** (touches many concerns)
6. **NetworkIntegrationService split** (requires careful analysis)
7. **LobbyRoomView split** (UI refactoring)

---

## Next Steps

1. ✅ Document god classes and violations (this document)
2. ⏭️ Review and approve refactoring approach with team
3. ⏭️ Create feature branch for refactoring work
4. ⏭️ Start with Priority 1: Extract SteamIdHelpers
5. ⏭️ Create tests for extracted services
6. ⏭️ Migrate incrementally, validate at each step

---

## References

- **Working Baseline**: Commit `90886c9` (Add logs directory documentation)
- **Current Flow Documentation**: [WORKING_MULTIPLAYER_FLOW.md](WORKING_MULTIPLAYER_FLOW.md)
- **Architecture**: [CLIENT_CONNECTION_FLOW.md](CLIENT_CONNECTION_FLOW.md) (deprecated but has architecture diagrams)

---

## Notes

- This is a **living document** - update as refactoring progresses
- Keep old documentation until refactoring complete
- All service interfaces should be in `src/Mod/Services/Interfaces/`
- Follow existing namespace conventions: `SiroccoLobby.Services.*`
