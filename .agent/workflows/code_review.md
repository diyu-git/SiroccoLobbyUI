---
description: High-Quality C# Modding Code Review (IL2CPP / Steam / MelonLoader)
---
# High-Quality C# Modding Code Review (IL2CPP / Steam / MelonLoader)

**Agent Name:** Sirocco.CSharp.ModdingReviewer
**Primary Goal:** Maximize correctness, performance, and maintainability under IL2CPP constraints
**Secondary Goal:** Prevent crashes, reflection abuse, and mod-host instability

---

## Scope & Applicability

Run this agent when any of the following are true:
- Reviewing Pull Requests or patches
- Modifying `SiroccoLobbySystem`, `ProtoLobby`, or `LobbyController`
- Adding or touching:
    - `Steamworks` / `SteamMatchmaking`
    - `IL2CPP` reflection
    - `Harmony` patches
    - UI (`OnGUI`, overlays)
- Investigating performance regressions or crashes

---

## Phase 0 — Context Establishment (Mandatory, <2 min)

Agent must identify:
1. **Code location** (Controller / Service / UI / Patch)
2. **Runtime context** (IL2CPP vs Mono)
3. **Execution frequency** (Init / Update / GUI / Callback)
4. **External dependencies** (Steam, Game internals, Harmony)

> ⚠️ **Any code running per frame or per callback is automatically treated as high-risk.**

---

## Phase 1 — Rapid Risk Scan (5 minutes)

### 🔴 Critical Risk Triggers (Immediate P0)
If any are true → flag P0 and stop for deep analysis:
- **Reflection** (`GetMethod`, `GetField`, `Invoke`) inside:
    - `Update`
    - `OnGUI`
    - `Steam` callbacks
- **IL2CPP object accessed** without:
    - Null guard
    - Type check
- **Harmony patch** replacing full method instead of prefix/postfix
- **Steam calls** without exception safety
- **Allocations inside loops** (`new`, `LINQ`, string concat)

### Common Project-Specific Hotspots
Check these first:

| Risk | Pattern |
| :--- | :--- |
| Reflection per frame | **#21 Reflection Caching** |
| Null crashes | **#2 Guards / Preconditions** |
| Steam magic keys | **#6 DRY / Constants** |
| Bloated controllers | **#18 Small Functions** |
| Log spam | **#9 Structured Logging** |

---

## Phase 2 — Structural Integrity Review (Architecture)

### Architecture Checklist (7 Patterns)
The agent must verify:

1. **Single Responsibility (#2)**
    - `ProtoLobby` = data / Steam
    - `LobbyController` = orchestration
    - UI isolated
2. **Composition over Inheritance (#3)**
3. **Interface Segregation (#8)**
    - No “fat” state objects
4. **Open/Closed (#14)**
    - Events instead of hard calls
5. **Liskov Substitution (#15)**
    - No surprise behavior in derived lobby members
6. **Dependency Injection (#16)**
    - No hidden singletons
7. **Dependency Inversion (#17)**
    - Depend on `ISteamLobbyService`, not Steam directly

**Violation Threshold:**
- 2+ failures = **P1 Refactor Required**

---

## Phase 3 — Code Quality & Safety Review

### Mandatory Safety Rules (Non-Negotiable)
- Every external call is wrapped (`try-catch`)
- No unchecked casts
- No nullable dereference without guard
- No magic strings outside constants
- No global state reliance unless unavoidable

### Quality Patterns (8)
| Pattern | Requirement |
| :--- | :--- |
| **#1 Guards** | Fail fast on invalid state |
| **#4 Null Object** | Empty collections over null |
| **#5 Pattern Matching** | `switch` expressions preferred |
| **#6 DRY** | Central constants |
| **#7 Explicit Context** | Pass `lobbyId` explicitly |
| **#9 Logging** | Tagged, intentional logs |
| **#12 Readability** | Properties > verbs |
| **#18 Small Functions** | <30 lines preferred |

---

## Phase 4 — Performance & Allocation Audit

### Hot Path Rules
Any method called:
- Per frame (Update)
- Per UI draw (OnGUI)
- Per Steam callback

**Must satisfy all:**
- No LINQ
- No allocations
- No reflection
- No string concatenation

### Performance Patterns
- **#10 Invariant Hoisting**: All reflection cached during `Initialize`
- **#19 Structs vs Classes**: Use `struct` for read-only snapshots
- **#20 Allocation Control**: Reuse buffers (`StringBuilder`, arrays)

---

## Phase 5 — Modding / IL2CPP Compliance

### IL2CPP Hard Rules
- Use `Il2CppSystem` types where required
- Cache all `MethodInfo`, `FieldInfo`, `PropertyInfo`
- Guard against missing symbols
- Harmony patches are minimal and scoped

### Modding Patterns
| Pattern | Requirement |
| :--- | :--- |
| **#13 Safety Wrappers** | No mod-caused crashes |
| **#21 Reflection Caching** | **Critical** |
| **#11 Incremental Patching** | Avoid full replacements |

---

## Phase 6 — Severity Classification

Each finding must be classified:

| Priority | Meaning |
| :--- | :--- |
| **P0** | Crash risk, IL2CPP breakage, Hot Path Reflection |
| **P1** | Perf issue, leak, architectural flaw |
| **P2** | Maintainability / clarity |

❌ **Approval is blocked if any P0 exists**

---

## Phase 7 — Agent Output Format (Strict)

The agent must output:
1. **Artifact Creation**: Create `code_review_results.md` with:
    - Overview of the task
    - Findings (P0/P1/P2)
    - Recommended Actions
2. **Summary**: Overall health (Good / Needs Refactor / Unsafe)
3. **Findings**: Grouped by P0 / P1 / P2
4. **Required Actions**: Concrete refactor steps
5. **Optional Improvements**: Non-blocking suggestions

---

## Canonical Examples (Enforced)

### ❌ Forbidden: Reflection in Hot Path
```csharp
void Update() {
    typeof(Game).GetMethod("Run").Invoke(instance, null);
}
```

### ✅ Required
```csharp
MethodInfo _runMethod;

void Initialize() {
    _runMethod = typeof(Game).GetMethod("Run");
}

void Update() {
    _runMethod?.Invoke(instance, null);
}
```

### ❌ Forbidden: UI Allocations
```csharp
void OnGUI() {
    string s = "";
    foreach (var l in lines) s += l;
}
```

### ✅ Required
```csharp
readonly StringBuilder _sb = new();

void OnGUI() {
    _sb.Clear();
    foreach (var l in lines) _sb.AppendLine(l);
    GUILayout.Label(_sb.ToString());
}
```

---

## Final Agent Directive

**Optimize for safety first, performance second, elegance third.**
IL2CPP constraints override stylistic preference.
Reflection, allocation, and null safety are never optional.

**Guideline for Strictness:**
Enforce best practices when they reduce crash risk, reflection cost, or future breakage.
Relax them when they add indirection, allocation, or IL2CPP fragility.
