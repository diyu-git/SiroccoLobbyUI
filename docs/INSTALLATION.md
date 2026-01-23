# Installation Guide

**Game**: Sirocco (5v5 Naval MOBA)  
**Mod**: SiroccoLobbyUI - Steam Lobby Browser  
**Platform**: Windows (Steam)

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Quick Install (Recommended)](#quick-install-recommended)
3. [Manual Installation](#manual-installation)
4. [Verification](#verification)
5. [Troubleshooting](#troubleshooting)
6. [Uninstallation](#uninstallation)
7. [Updating](#updating)

---

## Prerequisites

Before installing this mod, ensure you have:

### ✅ Required

1. **Sirocco** (Free-to-play)
   - Installed via Steam
   - Location: `C:\Program Files (x86)\Steam\steamapps\common\Sirocco\`
   - Must be able to launch and play vanilla game

2. **Steam Client**
   - Must be running when playing
   - Logged into your Steam account
   - Required for P2P networking

3. **MelonLoader 0.6.x** (Mod Framework)
   - Download: https://github.com/LavaGang/MelonLoader/releases/latest
   - **IMPORTANT**: Use the "Automated Installation" version
   - Version 0.6.x or higher recommended

### 📋 Recommended

- Windows 10/11 (64-bit)
- .NET Runtime 6.0 or higher (for development/building only)
- 500 MB free disk space

---

## Quick Install (Recommended)

### Step 1: Install MelonLoader

1. **Download MelonLoader Installer**
   - Go to: https://github.com/LavaGang/MelonLoader/releases/latest
   - Download: `MelonLoader.Installer.exe`

2. **Run Automated Installation**
   ```
   1. Run MelonLoader.Installer.exe
   2. Click "Select" and browse to Sirocco's game folder
      Example: C:\Program Files (x86)\Steam\steamapps\common\Sirocco\
   3. Select "Latest" version
   4. Click "Install"
   5. Wait for "Done!" message
   ```

3. **Verify MelonLoader Installation**
   - Launch Sirocco once
   - You should see a MelonLoader console window appear
   - The game should load normally
   - **First launch takes longer** (generating IL2CPP assemblies)

### Step 2: Download SiroccoLobbyUI

1. **Get Latest Release**
   - Go to: https://github.com/diyu-git/SiroccoLobbyUI/releases/latest
   - Download: `SiroccoLobbyUI-vX.X.X.zip`

2. **Extract Files**
   - Extract the ZIP contents
   - You should see:
     ```
     SiroccoLobbyUI-vX.X.X/
     ├── SiroccoLobbyUI.dll      ← Main mod DLL
     ├── Steamworks.NET.dll      ← Steam API wrapper
     └── README.txt              ← Installation notes
     ```

### Step 3: Install the Mod

1. **Locate Sirocco Mods Folder**
   ```
   C:\Program Files (x86)\Steam\steamapps\common\Sirocco\Mods\
   ```
   - If `Mods` folder doesn't exist, create it

2. **Copy Files**
   - Copy **both** DLL files to the `Mods` folder:
     ```
     SiroccoLobbyUI.dll      → Mods\SiroccoLobbyUI.dll
     Steamworks.NET.dll      → Mods\Steamworks.NET.dll
     ```

3. **Final Structure**
   ```
   Sirocco/
   ├── MelonLoader/           ← Created by MelonLoader installer
   ├── Mods/
   │   ├── SiroccoLobbyUI.dll    ← Mod files
   │   └── Steamworks.NET.dll    ← Steam wrapper
   ├── Sirocco.exe            ← Game executable
   └── UserData/              ← MelonLoader data
   ```

### Step 4: Launch and Test

1. **Start Sirocco via Steam**
   - MelonLoader console will appear
   - Look for: `[SiroccoLobby] Plugin initialized`

2. **In Main Menu**
   - Press **F5** to open lobby browser
   - You should see the Steam Lobby Browser UI

3. **Success!** 🎉
   - You can now create/join lobbies
   - See [Usage](#usage) section below

---

## Manual Installation

For advanced users or if automated install fails:

### 1. Manual MelonLoader Installation

```powershell
# Download MelonLoader manually
$url = "https://github.com/LavaGang/MelonLoader/releases/download/v0.6.x/MelonLoader.x64.zip"
$output = "$env:TEMP\MelonLoader.zip"
Invoke-WebRequest -Uri $url -OutFile $output

# Extract to game directory
$gameDir = "C:\Program Files (x86)\Steam\steamapps\common\Sirocco"
Expand-Archive -Path $output -DestinationPath $gameDir -Force

# Run version.dll to initialize
cd $gameDir
.\version.dll
```

### 2. Build from Source

See [BUILDING.md](BUILDING.md) for complete build instructions.

**Quick Reference**:
```bash
# Clone repository
git clone https://github.com/diyu-git/SiroccoLobbyUI.git
cd SiroccoLobbyUI

# Initialize submodules
git submodule update --init --recursive

# Configure paths (see BUILDING.md)
copy Directory.Build.props.user.example Directory.Build.props.user
# Edit Directory.Build.props.user with your paths

# Build
dotnet build src/SteamLobbyLib.csproj -c Release

# Output: src/bin/Release/net6.0/
```

---

## Verification

### Check Installation Success

1. **MelonLoader Console**
   - Should show: `[SiroccoLobby] Plugin initialized`
   - No red error messages about missing DLLs

2. **In-Game Test**
   - Press **F5** - Lobby browser should appear
   - Press **ESC** or **F5** again - Browser should close

3. **Log Files**
   - Location: `Sirocco\MelonLoader\Latest.log`
   - Search for: `"SiroccoLobby"`
   - Should see initialization messages

### Common Success Messages

```
[SiroccoLobby] Plugin initialized
[SiroccoLobby] Steam initialized successfully
[SiroccoLobby] Lobby controller ready
[SiroccoLobby] UI initialized
```

---

## Usage

### Keybinds

| Action | Key |
|--------|-----|
| **Toggle Lobby Browser** | `F5` |
| **Close UI** | `F5` or `ESC` |

### Creating a Lobby

1. Press **F5** in main menu
2. Click **"Create Lobby"**
3. Game starts as host
4. Steam lobby is created for others to join
5. Wait for players to join

### Joining a Lobby

1. Press **F5** in main menu
2. Click **"Refresh"** to see available lobbies
3. **Double-click** a lobby to join
4. Select your **team** and **captain**
5. Click **"Ready"** when ready to start

### Starting a Game (Host Only)

1. Wait for all players to click "Ready"
2. Click **"Start Game"**
3. Game begins for all connected players

### Leaving a Lobby

- Click **"Leave Lobby"** button
- Or press **ESC** to close UI and leave

---

## Troubleshooting

### Issue: Mod doesn't load

**Symptoms**: No MelonLoader console, or no `[SiroccoLobby]` messages

**Solutions**:
1. **Verify MelonLoader Installation**
   ```
   Check for: Sirocco\MelonLoader\
   Check for: Sirocco\version.dll or dobby.dll
   ```

2. **Reinstall MelonLoader**
   - Delete `MelonLoader\` folder
   - Delete `version.dll`, `dobby.dll`
   - Run MelonLoader installer again

3. **Check Game Integrity**
   - Steam → Right-click Sirocco → Properties → Installed Files → Verify integrity

### Issue: "Could not load file or assembly 'Steamworks.NET'"

**Cause**: Missing or wrong location for `Steamworks.NET.dll`

**Solution**:
```
Verify: Sirocco\Mods\Steamworks.NET.dll exists
If missing: Re-extract from release ZIP
```

### Issue: Lobby browser opens but no lobbies show

**Symptoms**: Empty list, "No lobbies found" message

**Solutions**:
1. **Check Steam Connection**
   - Steam client must be running
   - You must be logged in
   - Check Steam overlay works (Shift+Tab in-game)

2. **Refresh Lobby List**
   - Click "Refresh" button
   - Wait 2-3 seconds

3. **Create Your Own Lobby**
   - If no lobbies exist, create one
   - Others will then see it

### Issue: Can't join lobby / Connection failed

**Symptoms**: Join button does nothing, or timeout message

**Solutions**:
1. **Port Forwarding Not Required**
   - Steam P2P handles NAT traversal
   - But firewall must allow Steam

2. **Check Firewall**
   - Allow `Sirocco.exe` through Windows Firewall
   - Allow Steam through firewall

3. **Restart Steam**
   - Sometimes P2P connections get stuck
   - Restart Steam client and try again

### Issue: F5 doesn't work / UI doesn't appear

**Solutions**:
1. **Check Key Conflicts**
   - Try pressing F5 in main menu (not in-game)
   - Some keyboards require Fn+F5

2. **Check Logs**
   - Open: `Sirocco\MelonLoader\Latest.log`
   - Search for errors related to "UI" or "SiroccoLobby"

3. **Try Console Command** (if available)
   - Open console (varies by game)
   - Type: `/lobby` or similar mod command

### Issue: Mod works but crashes game

**Symptoms**: Game crashes when opening UI or joining lobby

**Solutions**:
1. **Update MelonLoader**
   - Use latest version (0.6.x+)

2. **Check Mod Compatibility**
   - Disable other mods temporarily
   - Test if SiroccoLobbyUI alone works

3. **Report Bug**
   - GitHub Issues: https://github.com/diyu-git/SiroccoLobbyUI/issues
   - Include: `Latest.log` file from MelonLoader folder

---

## Uninstallation

### Remove the Mod

1. **Delete Mod Files**
   ```
   Delete: Sirocco\Mods\SiroccoLobbyUI.dll
   Delete: Sirocco\Mods\Steamworks.NET.dll
   ```

2. **Keep MelonLoader** (Optional)
   - MelonLoader can stay for other mods
   - Or delete `MelonLoader\` folder to remove entirely

3. **Verify Removal**
   - Launch game
   - F5 should no longer open lobby browser

### Full Cleanup

To completely remove MelonLoader and all mods:

```
Delete: Sirocco\MelonLoader\
Delete: Sirocco\Mods\
Delete: Sirocco\UserData\
Delete: Sirocco\version.dll (or dobby.dll)
Delete: Sirocco\winmm.dll (if present)
```

Then verify game files via Steam to restore originals.

---

## Updating

### Update to Newer Version

1. **Download Latest Release**
   - https://github.com/diyu-git/SiroccoLobbyUI/releases/latest

2. **Replace Files**
   ```
   Overwrite: Sirocco\Mods\SiroccoLobbyUI.dll (new version)
   Keep:      Sirocco\Mods\Steamworks.NET.dll (usually unchanged)
   ```

3. **Check Changelog**
   - Read release notes for breaking changes
   - Some updates may require MelonLoader update

4. **Restart Game**
   - Close Sirocco completely
   - Launch via Steam
   - Verify new version in logs

### Update MelonLoader

If a mod update requires newer MelonLoader:

1. Run MelonLoader installer
2. Select game directory
3. Choose "Latest" version
4. Click "Install" (overwrites old version)

---

## FAQ

### Q: Does this work with the vanilla game?
**A**: Yes! The mod adds features, doesn't replace core gameplay.

### Q: Is this safe / Will I get banned?
**A**: This is a client-side mod for UI/lobby improvements. However, always check the game's EULA and mod policy.

### Q: Can I play with non-modded players?
**A**: Only if they also have the mod. The lobby system requires all players to use the mod.

### Q: Does this work on Linux / Steam Deck?
**A**: MelonLoader supports Linux via Wine/Proton, but testing is limited. YMMV.

### Q: Where are the log files?
**A**: `Sirocco\MelonLoader\Latest.log`

### Q: Can I use other mods with this?
**A**: Usually yes, but test compatibility. Some mods may conflict.

---

## Support

### Getting Help

1. **Read Documentation**
   - [README.md](../README.md)
   - [BUILDING.md](../BUILDING.md)
   - [WORKING_MULTIPLAYER_FLOW.md](WORKING_MULTIPLAYER_FLOW.md)

2. **Check GitHub Issues**
   - https://github.com/diyu-git/SiroccoLobbyUI/issues
   - Search existing issues first

3. **Report Bugs**
   - Create new issue with:
     - OS version
     - Game version
     - Mod version
     - MelonLoader version
     - `Latest.log` file
     - Steps to reproduce

### Community

- GitHub Discussions: https://github.com/diyu-git/SiroccoLobbyUI/discussions
- Discord: (if available)

---

## Credits

- **MelonLoader**: https://github.com/LavaGang/MelonLoader
- **Steamworks.NET**: https://github.com/rlabrecque/Steamworks.NET (MIT License)
- **Mirror Networking**: Used by the game for multiplayer

---

## License

This mod is released under the MIT License. See [LICENSE](../LICENSE) for details.

**Note**: This mod does not redistribute:
- Game files (copyrighted by game developer)
- Steam native DLLs (proprietary - Valve)
- MelonLoader (separate license)

Only the mod code and Steamworks.NET wrapper (MIT) are included.
