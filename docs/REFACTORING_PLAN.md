# Refactoring Plan: God Classes and Single Responsibility Violations

**Status**: 📋 **PLANNING** - System is working, we have a stable baseline on git

**Last Updated**: January 23, 2026

---

## Overview

The Sirocco Lobby UI mod is fully functional, but several classes have grown into "god classes" that violate the Single Responsibility Principle. This document outlines a systematic refactoring plan to improve maintainability without breaking existing functionality.

**Critical Context**: The refactoring plan must account for the **entire dependency chain**:
```
Plugin.cs (Composition Root)
    └─> Creates and wires all services
         ├─> LobbyController (main orchestrator)
         ├─> ProtoLobbyIntegration (facade)
         │    └─> NetworkIntegrationService (used internally)
         ├─> LobbyUIRoot (UI coordinator)
         │    ├─> LobbyBrowserView
         │    └─> LobbyRoomView
         └─> CaptainSelectionController (separate concern)
```

**Key Finding**: Refactoring cannot be done in isolation. Changes to `LobbyController` impact:
- `Plugin.cs` (initialization and wiring)
- `LobbyBrowserView` and `LobbyRoomView` (direct consumers)
- `ProtoLobbyIntegration` (owns `NetworkIntegrationService`)
- Event flow from `SteamLobbyManager` → `EventForwarder` → `LobbyController`

---

## Dependency Analysis

### Current Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                        Plugin.cs                            │
│                  (Composition Root)                         │
│  - Initializes all services                                 │
│  - Wires dependencies                                       │
│  - Handles F5 toggle, OnUpdate, OnGUI                       │
└────────┬────────────────────────────────────────────────────┘
         │
         ├──> SteamLobbyManager (SLL library)
         │    └──> EventForwarder → LobbyController (ILobbyEvents)
         │
         ├──> LobbyController (GOD CLASS - 966 lines)
         │    ├── Uses: ISteamLobbyService, ProtoLobbyIntegration
         │    ├── Used by: LobbyBrowserView, LobbyRoomView
         │    └── Responsibilities: Lifecycle, Data, Captain Mode, Ready, Events
         │
         ├──> ProtoLobbyIntegration (Facade - 124 lines)
         │    ├── Owns: NetworkIntegrationService (GOD CLASS - 776 lines)
         │    ├── Owns: GameReflectionBridge, LobbySelectionService, LobbyCompletionService
         │    └── Used by: LobbyController, LobbyRoomView, CaptainSelectionController
         │
         ├──> LobbyUIRoot (Coordinator)
         │    ├──> LobbyBrowserView
         │    │    └── Uses: LobbyController (Create/Join/Refresh)
         │    └──> LobbyRoomView (GOD VIEW - 615 lines)
         │         └── Uses: LobbyController (Ready/Start/Leave/Captain)
         │
         ├──> CaptainSelectionController (138 lines) ✅ Already focused
         │
         └──> LobbyState (Shared State - 104 lines) ✅ Already focused
```

### Dependency Impact Matrix

| Change Target | Directly Impacts | Indirectly Impacts | Risk Level |
|--------------|------------------|-------------------|------------|
| **LobbyController** | Plugin, LobbyBrowserView, LobbyRoomView | ProtoLobbyIntegration (if signatures change) | 🔴 **HIGH** |
| **NetworkIntegrationService** | ProtoLobbyIntegration | LobbyController (via ProtoLobby facade) | 🟡 **MEDIUM** |
| **LobbyRoomView** | LobbyUIRoot | None (leaf component) | 🟢 **LOW** |
| **ProtoLobbyIntegration** | Plugin, LobbyController, LobbyRoomView | All dependent services | 🔴 **HIGH** |

### Critical Insight: ProtoLobbyIntegration is a Facade

`ProtoLobbyIntegration` is **already a well-designed facade** that:
- ✅ Owns and coordinates 4 focused services
- ✅ Exposes a clean, stable API
- ✅ Hides implementation details from consumers
- ✅ Delegates to specialized services internally

**This is GOOD architecture!** We should follow this pattern for `LobbyController`.

---

## Refactoring Constraints

### Files That MUST Be Updated

When refactoring `LobbyController`, these files **cannot be ignored**:

1. **Plugin.cs** (Composition Root)
   - Constructs `LobbyController` with dependencies
   - Wires `EventForwarder.Delegate = _controller`
   - Calls `_controller.OnUpdate()` and handles lifecycle
   - **Impact**: Must update constructor calls, potentially add new service registrations

2. **LobbyBrowserView.cs** (UI Consumer)
   - Holds `private readonly LobbyController _controller`
   - Calls: `CreateLobby()`, `JoinLobby()`, `RefreshLobbyList()`
   - **Impact**: May need to call new services directly or through controller facade

3. **LobbyRoomView.cs** (UI Consumer - God View)
   - Holds `private readonly LobbyController _controller`
   - Calls: `ToggleReady()`, `StartGame()`, `EndLobby()`, `SelectCaptainAndTeam()`, `ToggleCaptainMode()`, etc.
   - **Impact**: Many method calls, high coupling to controller

4. **ProtoLobbyIntegration.cs** (Facade)
   - Contains `NetworkIntegrationService`
   - Used by `LobbyController` for network operations
   - **Impact**: If we extract services from `LobbyController`, they may need `ProtoLobbyIntegration` too

### Files That May Need Updates

5. **CaptainSelectionController.cs**
   - Separate controller, but interacts with `LobbyController` indirectly
   - **Impact**: Minimal, unless we change event flow

6. **LobbyUIRoot.cs**
   - Just switches between views
   - **Impact**: None (delegates to views)

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

### Updated Strategy: Follow ProtoLobbyIntegration Pattern

**Key Insight**: We should refactor `LobbyController` to be a **facade like `ProtoLobbyIntegration`**, not split it entirely.

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
    private readonly ISteamLobbyService _steam;
    private readonly LobbyState _state;
    
    public LobbyDataService(ISteamLobbyService steam, LobbyState state) { ... }
    
    public void RefreshLobbyData(object lobbyId) { /* extracted logic */ }
    public void RebuildLobbyCache() { /* extracted logic */ }
}

// Old code still in LobbyController
public void RefreshLobbyData() { /* original logic remains */ }
```

### Phase 2: **Internal Delegation (Backwards Compatible)**
1. LobbyController creates new services internally (composition)
2. Existing public methods delegate to services
3. **Plugin.cs does NOT change** - same constructor signature
4. **Views do NOT change** - same API surface
5. Validate behavior is identical

**Example**:
```csharp
public class LobbyController : ILobbyEvents // Same interface
{
    // NEW: Internal services (composition)
    private readonly LobbyDataService _lobbyData;
    private readonly CaptainModeService _captainMode;
    private readonly ReadyStateService _readyState;
    
    // OLD: Same constructor signature (backwards compatible)
    public LobbyController(
        LobbyState state,
        ISteamLobbyService steam,
        ProtoLobbyIntegration protoLobby,
        MelonLogger.Instance log,
        SteamLobbyServiceWrapper? wrapper = null,
        CaptainSelectionController? captainController = null)
    {
        // Create services internally
        _lobbyData = new LobbyDataService(steam, state, log);
        _captainMode = new CaptainModeService(steam, state, log);
        _readyState = new ReadyStateService(steam, state, protoLobby, log);
        // ... keep existing initialization
    }
    
    // PUBLIC API UNCHANGED - Delegate to services
    public void RefreshLobbyData()
    {
        _lobbyData.RefreshLobbyData(_state.CurrentLobby);
    }
    
    public void ToggleCaptainMode(bool enabled)
    {
        _captainMode.Toggle(enabled);
    }
}
```

**Benefits**:
- ✅ **Plugin.cs unchanged** - No wiring changes
- ✅ **Views unchanged** - Same controller API
- ✅ **Easy rollback** - Just revert internal delegation
- ✅ **Test services in isolation** - Can test `LobbyDataService` directly

### Phase 3: **Optional - Expose Services (Breaking Change)**

Only if we want views to use services directly:

```csharp
// In LobbyController - expose services as properties
public ILobbyDataService LobbyData => _lobbyData;
public ICaptainModeService CaptainMode => _captainMode;

// In LobbyRoomView - call services directly
_controller.CaptainMode.Toggle(enabled);
```

**This phase is OPTIONAL**. We may keep the facade pattern indefinitely.

### Phase 4: **Deprecate Old Methods (Final Cleanup)**
1. Mark old methods `[Obsolete]` if we exposed services
2. Update callers one-by-one
3. Remove old methods once fully migrated
4. Commit refactored architecture

**Note**: This phase only applies if we chose to expose services in Phase 3.

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

## Implementation Order (Updated)

### Priority 0: Architecture Decision ⚠️
**DECIDE**: Facade pattern (like `ProtoLobbyIntegration`) vs. Service exposure?

**Recommendation**: **Facade Pattern**
- ✅ Minimal impact on Plugin.cs and views
- ✅ Backwards compatible
- ✅ Easy to test (test services directly, facade is thin)
- ✅ Can always expose later if needed

### Priority 1: High Value, Low Risk, No Breaking Changes
1. **SteamIdHelpers** - Extract to static utility class
   - No constructor changes
   - Views don't use these methods
   - Pure extraction

2. **LobbyDataService** - Extract inside LobbyController
   - Internal service, delegate from existing methods
   - Zero impact on Plugin.cs
   - Zero impact on Views

### Priority 2: Medium Complexity, Internal Changes Only  
3. **CaptainModeService** - Extract inside LobbyController
   - Internal service, delegate from existing methods
   - Views still call `controller.ToggleCaptainMode()`
   - No Plugin.cs changes

4. **ReadyStateService** - Extract inside LobbyController
   - Internal service, delegate from existing methods
   - Views still call `controller.ToggleReady()`
   - No Plugin.cs changes

### Priority 3: Complex, May Require Plugin.cs Updates
5. **LobbyLifecycleService** - Extract or keep in controller?
   - Touches many concerns (Steam, Network, State)
   - May be better to keep in facade
   - **Decision Point**: Is this worth extracting?

6. **NetworkIntegrationService split** - Already inside ProtoLobbyIntegration
   - Follow same facade pattern
   - Split internally without changing `ProtoLobbyIntegration` API
   - No impact on LobbyController

7. **LobbyRoomView split** - Composition pattern
   - Create sub-views (MainView, CaptainView, ReadyView)
   - `LobbyRoomView` becomes coordinator
   - No impact on `LobbyUIRoot` (still creates one `LobbyRoomView`)

---

## Updated Benefits

### Improved Maintainability
- ✅ Each class has one clear purpose
- ✅ Easier to locate bugs (know which service to check)
- ✅ Reduced cognitive load when reading code
- ✅ **Backwards compatible** - no migration pain

### Better Testability
- ✅ Services can be unit tested in isolation
- ✅ Mock dependencies easily
- ✅ Test complex scenarios without full integration setup
- ✅ **Test services directly** - don't need full Plugin setup

### Enhanced Extensibility  
- ✅ Add new features to specific services
- ✅ Swap implementations (e.g., different ready validation rules)
- ✅ Reuse services across different controllers
- ✅ **Internal services hidden** - can refactor without breaking external code

### Reduced Coupling
- ✅ Services depend on interfaces, not concrete implementations
- ✅ Can evolve services independently
- ✅ Clearer dependency graph
- ✅ **Plugin.cs and Views unchanged** - loose coupling maintained

---

## Risk Mitigation (Updated)

### Keep System Working
- ✅ Have stable baseline on git (commit: 90886c9)
- ✅ Refactor incrementally, never "big bang"
- ✅ **Facade pattern** - external API stable
- ✅ Run integration tests after each phase
- ✅ Can roll back to known-good state

### Validate Behavior
- ✅ Extensive logging already in place
- ✅ Compare logs before/after refactoring
- ✅ Manual testing of all flows
- ✅ Keep WORKING_MULTIPLAYER_FLOW.md as test script
- ✅ **Same call sequences** - behavior identical

### Maintain Backwards Compatibility
- ✅ **No `[Obsolete]` needed** - keep all public methods
- ✅ **No Plugin.cs changes** (or minimal) - same wiring
- ✅ **No View changes** - same controller API
- ✅ Internal refactoring only

---

## Folder Structure Analysis

### Current Structure

```
src/Mod/
├── Plugin.cs                      # Composition root
├── Controller/                    # 2 files
│   ├── LobbyController.cs        # GOD CLASS (966 lines)
│   └── CaptainSelectionController.cs  # ✅ Focused (138 lines)
├── Model/                        # 5 files - Domain models
│   ├── LobbyState.cs             # ✅ Focused (104 lines)
│   ├── LobbySummary.cs
│   ├── LobbyMember.cs
│   ├── LobbyUIState.cs
│   └── CaptainModePhase.cs
├── Services/                     # 9 files (root level)
│   ├── ProtoLobbyIntegration.cs  # Facade (124 lines)
│   ├── NetworkIntegrationService.cs  # GOD CLASS (776 lines)
│   ├── LobbySelectionService.cs
│   ├── LobbyCompletionService.cs
│   ├── NativeLobbyWatcher.cs
│   ├── HarmonyPatches.cs
│   ├── SteamLobbyServiceWrapper.cs
│   ├── SteamReflectionBridge.cs
│   ├── ISteamLobbyService.cs
│   ├── Core/                     # 1 file - Core abstractions
│   │   └── GameReflectionBridge.cs  # (381 lines)
│   └── Helpers/                  # 6 files - Utilities
│       ├── AuthDebugTracker.cs
│       ├── ClientAuthenticatorHelper.cs
│       ├── NetworkManagerResolver.cs
│       ├── ObjectDumper.cs
│       ├── StringToUintFNV1a.cs
│       └── (future: SteamIdHelpers.cs)
├── UI/                           # 5 files
│   ├── LobbyUIRoot.cs            # ✅ Coordinator (49 lines)
│   ├── LobbyBrowserView.cs       # ✅ Focused (125 lines)
│   ├── LobbyRoomView.cs          # GOD VIEW (615 lines)
│   ├── LobbyStyles.cs
│   └── SharedUIComponents.cs
└── Helpers/                      # 1 file (root level)
    └── NativeLobbyHelpers.cs
```

### Issues with Current Structure

1. **Services/ is cluttered** (9 files at root level)
   - Mix of facades, implementations, wrappers, and interfaces
   - Hard to distinguish between public contracts and internal services
   - No clear organization by domain

2. **Helpers/ exists in two places**
   - `src/Mod/Helpers/` (1 file)
   - `src/Mod/Services/Helpers/` (6 files)
   - Inconsistent - should utilities be in Services or separate?

3. **No Interfaces/ folder**
   - `ISteamLobbyService.cs` is buried in Services/
   - After refactoring, we'll have more interfaces
   - Should be clearly separated from implementations

4. **Services/Core/ has only 1 file**
   - `GameReflectionBridge` - is this really "Core"?
   - Or is it just a large service that needs a subfolder?

5. **Controller/ will grow significantly**
   - If we extract services to separate files, where do they go?
   - Option A: Services/Lobby/ (domain-based)
   - Option B: Keep in Controller/ (co-location)

---

## Proposed Folder Structure (Option 1: Domain-Based)

This option organizes by business domain rather than technical layer:

```
src/Mod/
├── Plugin.cs
├── Controller/
│   ├── LobbyController.cs        # Facade (delegates to services)
│   └── CaptainSelectionController.cs
├── Model/                        # Domain models (unchanged)
│   ├── LobbyState.cs
│   ├── LobbySummary.cs
│   ├── LobbyMember.cs
│   ├── LobbyUIState.cs
│   └── CaptainModePhase.cs
├── Services/
│   ├── Interfaces/               # 🆕 Public contracts
│   │   ├── ILobbyDataService.cs
│   │   ├── ICaptainModeService.cs
│   │   ├── IReadyStateService.cs
│   │   ├── ILobbyLifecycleService.cs
│   │   └── ISteamLobbyService.cs  # ← moved from root
│   ├── Lobby/                    # 🆕 Lobby domain services
│   │   ├── LobbyDataService.cs
│   │   ├── CaptainModeService.cs
│   │   ├── ReadyStateService.cs
│   │   └── LobbyLifecycleService.cs
│   ├── Network/                  # 🆕 Network domain services
│   │   ├── NetworkIntegrationService.cs  # ← split this further
│   │   ├── P2PConnectionService.cs
│   │   ├── AuthenticationService.cs
│   │   └── TransportConfigService.cs
│   ├── Game/                     # 🆕 Game integration services
│   │   ├── ProtoLobbyIntegration.cs  # ← moved from root
│   │   ├── GameReflectionBridge.cs   # ← moved from Core/
│   │   ├── LobbySelectionService.cs
│   │   ├── LobbyCompletionService.cs
│   │   └── NativeLobbyWatcher.cs
│   ├── Steam/                    # 🆕 Steam integration services
│   │   ├── SteamLobbyServiceWrapper.cs  # ← moved from root
│   │   └── SteamReflectionBridge.cs     # ← moved from root
│   └── Utilities/                # 🆕 Renamed from Helpers
│       ├── HarmonyPatches.cs     # ← moved from root
│       ├── SteamIdHelpers.cs     # 🆕 Extracted from LobbyController
│       ├── AuthDebugTracker.cs
│       ├── ClientAuthenticatorHelper.cs
│       ├── NetworkManagerResolver.cs
│       ├── ObjectDumper.cs
│       └── StringToUintFNV1a.cs
├── UI/
│   ├── LobbyUIRoot.cs
│   ├── LobbyBrowserView.cs
│   ├── LobbyRoomView.cs          # Eventually: Coordinator only
│   ├── LobbyStyles.cs
│   ├── SharedUIComponents.cs
│   └── Views/                    # 🆕 Sub-views after splitting LobbyRoomView
│       ├── LobbyRoomMainView.cs
│       ├── CaptainModeView.cs
│       └── ReadyStateView.cs
└── (Remove Helpers/ - consolidated into Services/Utilities/)
```

**Benefits**:
- ✅ Clear domain separation (Lobby, Network, Game, Steam)
- ✅ Interfaces separated from implementations
- ✅ Services organized by concern
- ✅ Easy to find related functionality
- ✅ Scales well as services grow

**Drawbacks**:
- ❌ Large migration - many file moves
- ❌ Git history disrupted (file moves)
- ❌ Need to update all `using` statements

---

## Proposed Folder Structure (Option 2: Minimal Change)

This option minimizes disruption by keeping most files in place:

```
src/Mod/
├── Plugin.cs
├── Controller/
│   ├── LobbyController.cs        # Facade
│   └── CaptainSelectionController.cs
├── Model/                        # (unchanged)
├── Services/
│   ├── ProtoLobbyIntegration.cs  # (unchanged location)
│   ├── NetworkIntegrationService.cs  # (unchanged location)
│   ├── LobbySelectionService.cs
│   ├── LobbyCompletionService.cs
│   ├── NativeLobbyWatcher.cs
│   ├── HarmonyPatches.cs
│   ├── SteamLobbyServiceWrapper.cs
│   ├── SteamReflectionBridge.cs
│   ├── Interfaces/               # 🆕 Only new folder
│   │   ├── ISteamLobbyService.cs  # ← moved from root
│   │   ├── ILobbyDataService.cs   # 🆕
│   │   ├── ICaptainModeService.cs # 🆕
│   │   ├── IReadyStateService.cs  # 🆕
│   │   └── ILobbyLifecycleService.cs  # 🆕
│   ├── Lobby/                    # 🆕 New extracted services
│   │   ├── LobbyDataService.cs
│   │   ├── CaptainModeService.cs
│   │   ├── ReadyStateService.cs
│   │   └── LobbyLifecycleService.cs
│   ├── Core/
│   │   └── GameReflectionBridge.cs  # (unchanged)
│   └── Helpers/
│       ├── SteamIdHelpers.cs     # 🆕 Extracted utility
│       ├── AuthDebugTracker.cs
│       ├── ClientAuthenticatorHelper.cs
│       ├── NetworkManagerResolver.cs
│       ├── ObjectDumper.cs
│       └── StringToUintFNV1a.cs
└── UI/                           # (unchanged for now)
```

**Benefits**:
- ✅ Minimal disruption to existing structure
- ✅ Git history preserved for most files
- ✅ Only new folders: Interfaces/, Lobby/
- ✅ Easier migration path
- ✅ Less `using` statement updates

**Drawbacks**:
- ❌ Services/ root still cluttered (9 → 10+ files)
- ❌ No clear domain separation
- ❌ Harder to navigate as services grow

---

## Proposed Folder Structure (Option 3: Co-Location)

Keep extracted services close to the controller that uses them:

```
src/Mod/
├── Plugin.cs
├── Controller/
│   ├── LobbyController.cs        # Facade
│   ├── CaptainSelectionController.cs
│   └── Services/                 # 🆕 Controller-specific services
│       ├── LobbyDataService.cs
│       ├── CaptainModeService.cs
│       ├── ReadyStateService.cs
│       └── LobbyLifecycleService.cs
├── Model/                        # (unchanged)
├── Services/                     # Global/shared services only
│   ├── Interfaces/               # 🆕
│   │   ├── ISteamLobbyService.cs
│   │   ├── ILobbyDataService.cs
│   │   └── ...
│   ├── ProtoLobbyIntegration.cs
│   ├── NetworkIntegrationService.cs
│   └── ...
└── UI/
```

**Benefits**:
- ✅ Related code lives together
- ✅ Clear that these services are used by LobbyController
- ✅ Easy to find controller's dependencies

**Drawbacks**:
- ❌ Services aren't truly "global" if they're under Controller/
- ❌ Unconventional - usually Services/ is at same level as Controllers/
- ❌ Harder to reuse services across controllers

---

## Recommendation: Hybrid Approach (Pragmatic)

**Phase 1 (Now)**: Minimal change - Follow Option 2
- Create `Services/Interfaces/` for new interfaces
- Create `Services/Lobby/` for extracted lobby services
- Keep existing files where they are
- Add `Services/Helpers/SteamIdHelpers.cs`

**Phase 2 (Later)**: Domain organization - Move toward Option 1
- Once refactoring is stable and tested
- Group related services into domain folders
- Update namespaces and using statements
- Commit separately from logic changes

**Rationale**:
- ✅ Don't mix structural changes with logic changes
- ✅ Git history shows logic refactoring separately from file moves
- ✅ Can validate behavior before structure reorganization
- ✅ Easier rollback if needed

---

## Namespace Conventions

After refactoring, namespaces should reflect folder structure:

### Current Namespaces
```csharp
SiroccoLobby.Controller
SiroccoLobby.Model
SiroccoLobby.Services
SiroccoLobby.Services.Core
SiroccoLobby.Services.Helpers
SiroccoLobby.UI
SiroccoLobby.Helpers
```

### Proposed Namespaces (Option 1 - Domain-Based)
```csharp
SiroccoLobby.Controller
SiroccoLobby.Model
SiroccoLobby.Services.Interfaces      // 🆕
SiroccoLobby.Services.Lobby           // 🆕
SiroccoLobby.Services.Network         // 🆕
SiroccoLobby.Services.Game            // 🆕
SiroccoLobby.Services.Steam           // 🆕
SiroccoLobby.Services.Utilities       // Renamed from Helpers
SiroccoLobby.UI
SiroccoLobby.UI.Views                 // 🆕
```

### Proposed Namespaces (Option 2 - Minimal)
```csharp
SiroccoLobby.Controller
SiroccoLobby.Model
SiroccoLobby.Services
SiroccoLobby.Services.Interfaces      // 🆕
SiroccoLobby.Services.Lobby           // 🆕
SiroccoLobby.Services.Core
SiroccoLobby.Services.Helpers
SiroccoLobby.UI
```

**Decision**: Use Option 2 namespaces initially, migrate to Option 1 later if desired.

---

## Migration Strategy: Folder Structure

### Step 1: Create New Folders (No file moves yet)
```powershell
# Create new directories
New-Item -ItemType Directory "src/Mod/Services/Interfaces"
New-Item -ItemType Directory "src/Mod/Services/Lobby"
```

### Step 2: Add New Files in Correct Locations
```csharp
// New files go directly into target folders
src/Mod/Services/Interfaces/ILobbyDataService.cs
src/Mod/Services/Lobby/LobbyDataService.cs
```

### Step 3: Move Files (Phase 2, separate commit)
```powershell
# Move existing files (after refactoring is stable)
git mv src/Mod/Services/ISteamLobbyService.cs src/Mod/Services/Interfaces/
```

### Step 4: Update Namespaces and Using Statements
```csharp
// Old
using SiroccoLobby.Services;

// New
using SiroccoLobby.Services.Interfaces;
using SiroccoLobby.Services.Lobby;
```

---

## Critical Files to Monitor During Refactoring

### Must Review for Every Change
1. **Plugin.cs** - Ensure constructor signatures stable
2. **LobbyBrowserView.cs** - Test lobby creation/joining
3. **LobbyRoomView.cs** - Test ready flow, captain mode, game start
4. **ProtoLobbyIntegration.cs** - Ensure facade API stable

### Test After Each Service Extraction
- ✅ F5 opens lobby browser
- ✅ Create lobby (host)
- ✅ Join lobby (client)
- ✅ Select team/captain
- ✅ Toggle ready
- ✅ Start game (host)
- ✅ Captain mode (if enabled)
- ✅ Leave lobby

---

## Next Steps (Revised)

1. ✅ Document god classes and violations (this document)
2. ✅ Identify all dependent files (Plugin, Views, ProtoLobby)
3. ⏭️ **Decision**: Confirm facade pattern approach
4. ⏭️ Create feature branch for refactoring work
5. ⏭️ Start with Priority 1: Extract SteamIdHelpers (zero impact)
6. ⏭️ Extract LobbyDataService (internal, delegate)
7. ⏭️ Create tests for extracted services
8. ⏭️ Validate at each step, commit incrementally

---

## Conclusion

**Key Realization**: The refactoring plan **must account for the entire dependency graph**:
- Plugin.cs creates and wires everything
- Views consume LobbyController directly
- ProtoLobbyIntegration is already a good facade pattern example
- NetworkIntegrationService lives inside ProtoLobbyIntegration

**Folder Structure Impact**:
- ✅ Current structure is decent but will become cluttered as services grow
- ✅ Create `Services/Interfaces/` and `Services/Lobby/` for new code
- ✅ Don't mix structural changes with logic changes
- ✅ **Phase 1**: Add new files in correct locations
- ✅ **Phase 2**: Reorganize existing files once refactoring is stable

**Revised Strategy**: 
- ✅ Follow the **facade pattern** (like ProtoLobbyIntegration)
- ✅ Extract services **internally** within LobbyController
- ✅ Keep **public API stable** - no changes to Plugin.cs or Views
- ✅ Delegate from existing methods to new services
- ✅ **Zero breaking changes** until we explicitly decide to expose services
- ✅ **Minimal folder changes** - only add new folders, don't move existing files yet
- ✅ **Namespaces reflect folders** - maintain consistency

**Implementation Order**:
1. Create `Services/Interfaces/` and `Services/Lobby/` folders
2. Extract services into new folders (new files only)
3. Keep LobbyController in current location (delegates to new services)
4. Validate behavior, commit incrementally
5. **Later**: Reorganize existing files by domain (separate commit)

This approach is **lower risk**, **backwards compatible**, **easier to roll back**, and **doesn't mix structural with logical changes**.

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
