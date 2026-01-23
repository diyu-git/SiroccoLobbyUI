# SiroccoLobbyUI

A custom Steam lobby browser and multiplayer room UI for **Sirocco** (5v5 naval MOBA), inspired by Battle.net and Warcraft 3 lobbies.

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![.NET](https://img.shields.io/badge/.NET-6.0-purple.svg)
![MelonLoader](https://img.shields.io/badge/MelonLoader-required-orange.svg)

## Features

- **Lobby Browser**: Browse and filter available Steam lobbies with real-time updates
- **5v5 Lobby Room**: 10 player slots with team assignments and ready status
- **Event Log**: Real-time system messages for player joins, leaves, and state changes
- **Naval Theme**: Ocean-inspired UI with teals, blues, and aqua accents
- **Steam Integration**: Seamless integration with Steam's P2P networking
- **Captain Selection**: In-lobby captain selection with game-specific integration

## Requirements

Before installing this mod, you **must** have:

1. **Sirocco** (free-to-play, installed via Steam)
2. **Steam client** running
3. **MelonLoader** installed in your Sirocco game directory
   - Download from: https://github.com/LavaGang/MelonLoader/releases
   - Follow the official installation guide

> **Important**: This mod does **not** include MelonLoader or any game files. You must install these separately.

📖 **[See Complete Installation Guide →](docs/INSTALLATION.md)**

## Quick Install

1. **Install MelonLoader** (one-time setup)
   - Download and run the automated installer
   - Point it to your Sirocco game folder

2. **Download Latest Release**
   - Get the mod from [Releases page](https://github.com/diyu-git/SiroccoLobbyUI/releases/latest)

3. **Copy Files to Game**
   ```
   Extract to: <Steam>\steamapps\common\Sirocco\Mods\
   
   Files (all 3 required):
   - SiroccoLobbyUI.dll
   - SteamLobbyLib.dll
   - Steamworks.NET.dll
   ```

4. **Launch & Play**
   - Start Sirocco via Steam
   - Press **F5** to open lobby browser

📖 **Detailed instructions, troubleshooting, and FAQ: [INSTALLATION.md](docs/INSTALLATION.md)**

## Dependency Management

This project uses **Git Submodules** to manage the `SLL` (SteamLobbyLib) dependency. This allows the library to be maintained independently while being integrated here.

- **Source of Truth**: [https://github.com/diyu-git/SteamLobbyLib](https://github.com/diyu-git/SteamLobbyLib)
- **Local Enhancements**: Any changes made to `SLL` within this project should be committed inside the `SLL` folder and pushed back to the main library repository to keep both projects in sync.

## Building from Source

See [BUILDING.md](BUILDING.md) for detailed build instructions.

## Usage

| Action | Keybind/Method |
|--------|----------------|
| **Toggle lobby browser** | Press **F5** |
| **Close lobby UI** | Press **F5** or ESC |
| **Create lobby** | Click "Create Lobby" in browser |
| **Join lobby** | Double-click a lobby in the browser |
| **Ready up** | Click "Ready" in the lobby room |
| **Start game** | Host clicks "Start Game" when all players are ready |
| **Leave lobby** | Click "Leave Lobby" |

## Architecture

```
Steam API → SteamLobbyManager → ISteamLobbyService → LobbyController → UI Views
                                                    ↓
                                            ProtoLobbyIntegration → Game Systems
```

### Key Components

- **`SteamLobbyManager`**: Core Steam lobby lifecycle management
- **`ISteamLobbyService`**: Domain interface for lobby operations
- **`LobbyController`**: Coordinates lobby state and UI updates
- **`ProtoLobbyIntegration`**: Bridges custom lobby system with game's native networking
- **`NetworkIntegrationService`**: Mirror connect/auth/ready/add-player orchestration (reflection-based)
- **`LobbyBrowserView`**: IMGUI-based lobby browser
- **`LobbyRoomView`**: IMGUI-based 5v5 lobby room with player slots

## Debugging & Diagnostics

### Proto-lobby graph dump (runtime introspection)

Because the game is **Unity IL2CPP**, decompiled C# wrappers often route through `il2cpp_runtime_invoke` and don’t expose the actual method bodies.
Instead, this project uses *runtime introspection* to dump live object graphs at key points.

- The dumper is implemented in `src/Mod/Services/Helpers/ObjectDumper.cs`.
- The current dump entry point is in `src/Mod/Services/NetworkIntegrationService.cs` and is logged with the prefix `"[ProtoDump]"`.

What you’ll see in logs:

- `"[ProtoDump] === <label> ==="` header lines
- filtered field/property dumps that include names like `lobby`, `proto`, `ready`, `steam`, `network`, `connection`, `player`

Notes:

- Dump output is intentionally bounded (depth + enumerable sampling) to avoid huge logs.
- Cycles are detected and printed as `<visited ...>` to keep dumping safe.

### About `SteamP2PNetworkTester`

Some proto-lobby related types in the game are located under `Il2CppWartide.Testing.*` (e.g., `SteamP2PNetworkTester`).
In the normal production lobby flow, this object may **not** exist at runtime. The mod treats it as optional/debug-only.
If it’s missing, hosting/joining can still work.

## Project Structure

```
SiroccoLobbyUI/
├── src/                            # Main mod project
│   ├── Mod/
│   │   ├── Controller/             # Lobby and captain selection controllers
│   │   ├── Model/                  # Lobby state and member models
│   │   ├── Services/               # Steam integration and game bridges
│   │   ├── UI/                     # IMGUI views and styles
│   │   └── Plugin.cs               # MelonLoader entry point
│   ├── Interfaces.cs               # Core domain interfaces
│   ├── LobbyData.cs                # Lobby data structures
│   ├── SteamLobbyManager.cs        # Steam API wrapper
│   └── SteamLobbyLib.csproj        # Project file
├── SLL/                            # SteamLobbyLib submodule (legacy)
│   └── steamworks/
│       └── Steamworks.NET.dll      # MIT-licensed Steam wrapper
├── docs/                           # Documentation
│   └── CLIENT_CONNECTION_FLOW.md   # Client connection architecture
├── BUILDING.md                     # Build instructions
├── THIRD_PARTY_LICENSES.md         # Third-party attributions
└── README.md                       # This file
```

## Documentation

- **[Client Connection Flow](docs/CLIENT_CONNECTION_FLOW.md)**: Detailed explanation of how clients connect to hosted games via Riptide P2P networking
- **[Building Guide](BUILDING.md)**: Build instructions and troubleshooting
- **[Third-Party Licenses](THIRD_PARTY_LICENSES.md)**: Attribution for dependencies


## Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This mod is licensed under the **MIT License**. See [LICENSE](LICENSE) for details.

### Third-Party Licenses

This project uses **Steamworks.NET** (MIT License). See [THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md) for full attribution.

## Legal Disclaimer

**Important**: This mod includes minimal dependencies:

- **Steamworks.NET.dll** (MIT licensed wrapper) is included
- **MelonLoader** must be installed separately
- **Unity runtime files** are not redistributed

This is an **independent mod** and is **not affiliated with** the developers or publishers of Sirocco. All game-related trademarks and copyrights belong to their respective owners.

## Troubleshooting

### Mod doesn't load

1. Verify MelonLoader is installed correctly
2. Check that `SiroccoLobbyUI.dll` and `Steamworks.NET.dll` are in the `Mods` folder
3. Ensure Steam client is running
4. Check MelonLoader logs: `<GAME_PATH>\MelonLoader\Latest.log`

### "Steam API initialization failed"

1. Ensure Steam client is running
2. Verify you own Sirocco on Steam
3. Check that the game's `steam_api64.dll` is present in the game directory

### Lobby browser is empty

1. Ensure you're connected to Steam
2. Try clicking "Refresh Lobbies"
3. Check your Steam network connectivity
4. Verify other players have created lobbies

### Build errors

See [BUILDING.md](BUILDING.md) for detailed troubleshooting.

## Links

- **Report Issues**: [GitHub Issues](https://github.com/diyu-git/SiroccoLobbyUI/issues)
- **MelonLoader**: https://github.com/LavaGang/MelonLoader
- **Steamworks.NET**: https://github.com/rlabrecque/Steamworks.NET

## Acknowledgments

- **Riley Labrecque** for Steamworks.NET
- **LavaGang** for MelonLoader
