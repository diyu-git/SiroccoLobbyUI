# SiroccoLobbyUI

A custom Steam lobby browser and multiplayer room UI for **Sirocco** (5v5 naval MOBA), inspired by Battle.net and Warcraft 3 lobbies.

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![.NET](https://img.shields.io/badge/.NET-6.0-purple.svg)
![MelonLoader](https://img.shields.io/badge/MelonLoader-required-orange.svg)

## ✨ Features

- **🔍 Lobby Browser**: Browse and filter available Steam lobbies with real-time updates
- **⚓ 5v5 Lobby Room**: 10 player slots with team assignments and ready status
- **📜 Event Log**: Real-time system messages for player joins, leaves, and state changes
- **🌊 Naval Theme**: Ocean-inspired UI with teals, blues, and aqua accents
- **🎮 Steam Integration**: Seamless integration with Steam's P2P networking
- **🎯 Captain Selection**: In-lobby captain selection with game-specific integration

## ⚠️ Requirements

Before installing this mod, you **must** have:

1. **Sirocco** (free-to-play, installed via Steam)
2. **Steam client** running
3. **MelonLoader** installed in your Sirocco game directory
   - Download from: https://github.com/LavaGang/MelonLoader/releases
   - Follow the official installation guide

> **⚠️ Important**: This mod does **not** include MelonLoader or any game files. You must install these separately.

## 📦 Installation

### Option 1: Download Precompiled Release (Recommended)

1. Download the latest release ZIP from the [Releases page](https://github.com/diyu-git/SiroccoLobbyUI/releases)
2. Extract the ZIP contents to your Sirocco `Mods` folder:
   ```
   <Steam>\steamapps\common\Sirocco\Mods\
   ```
3. Launch Sirocco via Steam
4. Press **F5** to toggle the lobby browser

### Option 2: Build from Source

See [BUILDING.md](BUILDING.md) for detailed build instructions.

**Quick start:**

```bash
# Clone the repository
git clone https://github.com/diyu-git/SiroccoLobbyUI.git
cd SiroccoLobbyUI

# Configure your game path (create Directory.Build.props.user)
# See BUILDING.md for details

# Build
dotnet build SLL/SteamLobbyLib/SteamLobbyLib.csproj -c Release

# Copy to game
cp SLL/SteamLobbyLib/bin/Release/net6.0/SiroccoLobbyUI.dll "<Steam>/steamapps/common/Sirocco/Mods/"
cp SLL/SteamLobbyLib/bin/Release/net6.0/Steamworks.NET.dll "<Steam>/steamapps/common/Sirocco/Mods/"
```

## 🎮 Usage

| Action | Keybind/Method |
|--------|----------------|
| **Toggle lobby browser** | Press **F5** |
| **Close lobby UI** | Press **F5** or ESC |
| **Create lobby** | Click "Create Lobby" in browser |
| **Join lobby** | Double-click a lobby in the browser |
| **Ready up** | Click "Ready" in the lobby room |
| **Start game** | Host clicks "Start Game" when all players are ready |
| **Leave lobby** | Click "Leave Lobby" |

## 🏗️ Architecture

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
- **`LobbyBrowserView`**: IMGUI-based lobby browser
- **`LobbyRoomView`**: IMGUI-based 5v5 lobby room with player slots

## 📁 Project Structure

```
SiroccoLobbySystem/
├── SLL/
│   ├── SteamLobbyLib/              # Main mod project
│   │   ├── Mod/
│   │   │   ├── Controller/         # Lobby and captain selection controllers
│   │   │   ├── Model/              # Lobby state and member models
│   │   │   ├── Services/           # Steam integration and game bridges
│   │   │   ├── UI/                 # IMGUI views and styles
│   │   │   └── Plugin.cs           # MelonLoader entry point
│   │   ├── Interfaces.cs           # Core domain interfaces
│   │   ├── LobbyData.cs            # Lobby data structures
│   │   └── SteamLobbyManager.cs    # Steam API wrapper
│   └── steamworks/
│       └── Steamworks.NET.dll      # MIT-licensed Steam wrapper
├── BUILDING.md                     # Build instructions
├── THIRD_PARTY_LICENSES.md         # Third-party attributions
└── README.md                       # This file
```

## 🤝 Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This mod is licensed under the **MIT License**. See [LICENSE](LICENSE) for details.

### Third-Party Licenses

This project uses **Steamworks.NET** (MIT License). See [THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md) for full attribution.

## ⚖️ Legal Disclaimer

**Important**: This mod includes minimal dependencies:

- ✅ **Steamworks.NET.dll** (MIT licensed wrapper) is included
- ⚠️ **steam_api64.dll** (Steamworks SDK) is included for convenience, but you can also use the one from your game installation
- ❌ **Game assemblies** are not included
- ❌ **MelonLoader** must be installed separately
- ❌ **Unity runtime files** are not redistributed

This is an **independent mod** and is **not affiliated with or endorsed by** the developers or publishers of Sirocco. All game-related trademarks and copyrights belong to their respective owners.

## 🐛 Troubleshooting

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

## 🔗 Links

- **Report Issues**: [GitHub Issues](https://github.com/diyu-git/SiroccoLobbyUI/issues)
- **MelonLoader**: https://github.com/LavaGang/MelonLoader
- **Steamworks.NET**: https://github.com/rlabrecque/Steamworks.NET

## 🙏 Acknowledgments

- **Riley Labrecque** for Steamworks.NET
- **LavaGang** for MelonLoader
- **Blizzard Entertainment** for Battle.net UI inspiration
- The Sirocco modding community

---

**Made with ⚓ for the Sirocco community**
