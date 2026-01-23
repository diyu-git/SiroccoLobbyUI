# Changelog

All notable changes to the Sirocco Lobby UI project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-01-23

### 🎉 Initial Release

First stable release of Sirocco Lobby UI - A custom Steam lobby browser and multiplayer room UI for Sirocco (5v5 naval MOBA).

### ✨ Features

#### Core Lobby System
- **Steam Lobby Browser** - Browse and filter available Steam lobbies with real-time updates
- **5v5 Lobby Room** - 10 player slots with team assignments and ready status
- **Host/Join Flow** - Create lobbies as host or join existing lobbies as client
- **Auto-Refresh** - Automatic lobby list refresh every 5 seconds (toggleable)
- **F5 Toggle** - Quick access to lobby browser from main menu

#### Multiplayer Integration
- **Steam P2P Networking** - Seamless integration with Steam's P2P transport
- **Mirror Network Support** - Full integration with game's Mirror networking stack
- **Auto-Connection** - Automatic client connection to host's game server
- **Ready State Management** - Host/client ready coordination with validation
- **Game Start Coordination** - Host can start game when all players ready

#### Team Management
- **Team Selection** - Players choose Team A or Team B
- **Captain Selection** - Choose from available captains with game integration
- **Captain Mode** (Advanced) - Snake draft system for competitive team building
  - Host assigns two captains
  - Snake draft pattern (A, B, B, A, A, B...)
  - Real-time draft feed

#### User Interface
- **Naval Theme** - Ocean-inspired UI with teals, blues, and aqua accents
- **Event Log** - Real-time system messages for player joins, leaves, and state changes
- **Responsive Design** - Adaptive window sizing (65% of screen)
- **Player List** - Shows all lobby members with Steam names, teams, captains, and ready status
- **Host Controls** - Start game, kick players (future), lobby settings

### 🛠️ Technical

#### Architecture
- **Facade Pattern** - Clean separation between UI, Controller, and Services
- **Event-Driven** - Steam callbacks properly handled and forwarded
- **Reflection-Based** - Integrates with game's native systems via IL2CPP reflection
- **Service Layer** - Organized services for Steam, Network, Game integration
- **State Management** - Centralized lobby state with reactive updates

#### Integration Points
- **SteamLobbyLib (SLL)** - Custom Steam lobby wrapper library (submodule)
- **MelonLoader** - Mod framework for Unity IL2CPP games
- **Steamworks.NET** - MIT-licensed Steam API wrapper
- **Mirror Networking** - Game's networking framework
- **Harmony** - Optional IL2CPP method tracing for debugging

#### Code Quality
- **966 lines** - Main controller (identified for refactoring in v2.0)
- **Extensive Logging** - Debug traces for connection flow, lobby events, game integration
- **Error Handling** - Graceful fallbacks and user-friendly error messages
- **Object Dumping** - Runtime introspection for IL2CPP debugging

### 📚 Documentation

- **README.md** - Project overview, quick start, architecture
- **INSTALLATION.md** - Comprehensive installation guide with troubleshooting
- **BUILDING.md** - Build instructions for developers
- **WORKING_MULTIPLAYER_FLOW.md** - Complete multiplayer connection flow documentation
- **CLIENT_CONNECTION_FLOW.md** - Legacy architecture documentation (deprecated)
- **REFACTORING_PLAN.md** - Technical debt analysis and refactoring roadmap
- **THIRD_PARTY_LICENSES.md** - License compliance documentation

### 🔧 Build System
- **.NET 6.0** - Target framework
- **Directory.Build.props** - Centralized build configuration
- **Submodules** - Git submodules for SteamLobbyLib dependency
- **Release Build** - Optimized release configuration

### ✅ Tested Flows

All critical user flows have been manually tested and verified:

1. ✅ **Host Creates Lobby** - Start game server, create Steam lobby, wait for players
2. ✅ **Client Browses Lobbies** - Refresh lobby list, see available lobbies with host info
3. ✅ **Client Joins Lobby** - Join Steam lobby, auto-connect to host's Mirror/Steam P2P
4. ✅ **Team/Captain Selection** - Select team and captain, sync to Steam and game
5. ✅ **Ready Coordination** - Client ready → Host ready (when all ready) → Start game
6. ✅ **Game Start** - Host starts, RPC sent via Mirror, clients receive and transition
7. ✅ **Captain Mode** (Advanced) - Snake draft system with captain assignment and picks
8. ✅ **Leave Lobby** - Clean exit from Steam lobby and network shutdown

### 🐛 Known Issues

None critical. See [GitHub Issues](https://github.com/diyu-git/SiroccoLobbyUI/issues) for minor enhancements and feature requests.

### 📋 Requirements

- **Sirocco** (Free-to-play on Steam)
- **Steam Client** (Must be running)
- **MelonLoader 0.6.x** (Mod framework)
- **Windows 10/11** (64-bit)

### 🔗 Links

- **Repository**: https://github.com/diyu-git/SiroccoLobbyUI
- **SteamLobbyLib**: https://github.com/diyu-git/SteamLobbyLib
- **License**: MIT

---

## [Unreleased]

### Planned for v2.0.0

- Refactor god classes (LobbyController, NetworkIntegrationService, LobbyRoomView)
- Extract domain services (LobbyDataService, CaptainModeService, ReadyStateService)
- Improve folder structure (domain-based organization)
- Add unit tests for core services
- Enhanced error handling and recovery
- Settings persistence
- Player kick/ban functionality

---

## Release Notes Template (For Future Releases)

```markdown
## [X.Y.Z] - YYYY-MM-DD

### Added
- New features

### Changed
- Changes in existing functionality

### Deprecated
- Features that will be removed

### Removed
- Removed features

### Fixed
- Bug fixes

### Security
- Security fixes
```

---

[Unreleased]: https://github.com/diyu-git/SiroccoLobbyUI/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/diyu-git/SiroccoLobbyUI/releases/tag/v1.0.0
