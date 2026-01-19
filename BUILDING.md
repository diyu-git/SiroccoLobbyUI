# Building from Source

This guide explains how to build **SiroccoLobbyUI** from source code.

## Prerequisites

Before building, ensure you have:

1. **.NET 6.0 SDK or later**
   - Download from: https://dotnet.microsoft.com/download
   - Verify installation: `dotnet --version`

2. **Sirocco** (the game) installed via Steam

3. **MelonLoader** installed in your Sirocco game directory
   - Download from: https://github.com/LavaGang/MelonLoader/releases
   - Follow the official installation guide
   - MelonLoader must be installed **before** building this mod

4. **Git** (to clone the repository)
   - Download from: https://git-scm.com/

## Step 1: Clone the Repository

```bash
git clone https://github.com/yourusername/SiroccoLobbyUI.git
cd SiroccoLobbyUI
```

## Step 2: Configure Build Paths

The build process needs to know where your game is installed. You have two options:

### Option A: Create a Local Configuration File (Recommended)

Create a file named `Directory.Build.props.user` in the repository root:

```xml
<Project>
    <PropertyGroup>
        <GAME_INSTALL_PATH>C:\Program Files (x86)\Steam\steamapps\common\Sirocco</GAME_INSTALL_PATH>
    </PropertyGroup>
</Project>
```

**Replace the path** with your actual Sirocco installation directory.

> **Note**: This file is gitignored and won't be committed, so your local paths stay private.

### Option B: Set Environment Variables

Alternatively, set the `GAME_INSTALL_PATH` environment variable:

**Windows (PowerShell):**
```powershell
$env:GAME_INSTALL_PATH = "C:\Program Files (x86)\Steam\steamapps\common\Sirocco"
```

**Windows (Command Prompt):**
```cmd
set GAME_INSTALL_PATH=C:\Program Files (x86)\Steam\steamapps\common\Sirocco
```

**Linux/Mac:**
```bash
export GAME_INSTALL_PATH="$HOME/.steam/steam/steamapps/common/Sirocco"
```

## Step 3: Build the Mod

### Debug Build (for development)

```bash
dotnet build SLL/SteamLobbyLib/SteamLobbyLib.csproj
```

### Release Build (for distribution)

```bash
dotnet build SLL/SteamLobbyLib/SteamLobbyLib.csproj -c Release
```

## Step 4: Locate the Output

After building, you'll find the compiled mod at:

- **Debug**: `SLL/SteamLobbyLib/bin/Debug/net6.0/SiroccoLobbyUI.dll`
- **Release**: `SLL/SteamLobbyLib/bin/Release/net6.0/SiroccoLobbyUI.dll`

You'll also find `Steamworks.NET.dll` in the same directory.

## Step 5: Install the Mod

Copy the compiled files to your game's Mods folder:

```bash
# Windows (PowerShell)
Copy-Item "SLL\SteamLobbyLib\bin\Release\net6.0\SiroccoLobbyUI.dll" "$env:GAME_INSTALL_PATH\Mods\"
Copy-Item "SLL\SteamLobbyLib\bin\Release\net6.0\Steamworks.NET.dll" "$env:GAME_INSTALL_PATH\Mods\"
```

```bash
# Linux/Mac
cp SLL/SteamLobbyLib/bin/Release/net6.0/SiroccoLobbyUI.dll "$GAME_INSTALL_PATH/Mods/"
cp SLL/SteamLobbyLib/bin/Release/net6.0/Steamworks.NET.dll "$GAME_INSTALL_PATH/Mods/"
```

> **Tip**: If you configured `GAME_INSTALL_PATH` correctly, the build will automatically copy the mod to your Mods folder.

## Troubleshooting

### "Could not find file 'MelonLoader.dll'"

**Cause**: MelonLoader is not installed, or the build can't find it.

**Solution**:
1. Verify MelonLoader is installed in `<GAME_INSTALL_PATH>\MelonLoader\`
2. Check that `GAME_INSTALL_PATH` is set correctly
3. If you have MelonLoader installed elsewhere, you can keep the reference DLLs in `SLL/SteamLobbyLib/lib/` for build-time reference (they won't be redistributed)

### "Could not find file 'UnityEngine.CoreModule.dll'"

**Cause**: The build can't find the game's Unity assemblies.

**Solution**:
1. Ensure the game is installed
2. Ensure MelonLoader has generated the `Managed` folder (run the game once with MelonLoader installed)
3. Check that `GAME_INSTALL_PATH` points to the correct directory

### Build succeeds but mod doesn't load in-game

**Checklist**:
- [ ] MelonLoader is installed correctly
- [ ] `SiroccoLobbyUI.dll` is in the `Mods` folder
- [ ] `Steamworks.NET.dll` is in the `Mods` folder
- [ ] Steam client is running
- [ ] Check MelonLoader logs in `<GAME_INSTALL_PATH>\MelonLoader\Latest.log`

## Creating a Release Package

To create a distributable ZIP for GitHub releases:

```bash
# Build in Release mode
dotnet build SLL/SteamLobbyLib/SteamLobbyLib.csproj -c Release

# Create release directory
mkdir release
cd release

# Copy only the necessary files
cp ../SLL/SteamLobbyLib/bin/Release/net6.0/SiroccoLobbyUI.dll .
cp ../SLL/SteamLobbyLib/bin/Release/net6.0/Steamworks.NET.dll .
cp ../README.md README.txt
cp ../LICENSE LICENSE.txt

# Create ZIP
# Windows: Use 7-Zip, WinRAR, or PowerShell:
Compress-Archive -Path * -DestinationPath SiroccoLobbyUI-v1.0.0.zip

# Linux/Mac:
zip -r SiroccoLobbyUI-v1.0.0.zip *
```

## What Gets Built vs. What Gets Redistributed

| File | Built? | Redistributed? | Notes |
|------|--------|----------------|-------|
| `SiroccoLobbyUI.dll` | ✅ Yes | ✅ Yes | Your mod |
| `Steamworks.NET.dll` | ✅ Copied | ✅ Yes | MIT licensed |
| `steam_api64.dll` | ❌ No | ❌ No | Loaded from game |
| `MelonLoader.dll` | ❌ No | ❌ No | Users install separately |
| `0Harmony.dll` | ❌ No | ❌ No | Provided by MelonLoader |
| `Il2CppInterop.*.dll` | ❌ No | ❌ No | Provided by MelonLoader |
| Game assemblies | ❌ No | ❌ No | Referenced at build time only |

The `.csproj` file is configured with `<Private>False</Private>` for all proprietary dependencies, ensuring they are **not** copied to the output directory.

## Next Steps

- Read [CONTRIBUTING.md](CONTRIBUTING.md) if you want to contribute
- Check the [GitHub Publication Guide](docs/github_publication_guide.md) for distribution best practices
