# Test: Does Steamworks.NET Find Game's steam_api64.dll?

## Test Procedure

1. **Remove bundled steam_api64.dll from Mods folder**
   ```powershell
   Remove-Item "D:\Spell\Installed\Steam\steamapps\common\Sirocco\Mods\steam_api64.dll"
   ```

2. **Verify game's DLL is still present**
   ```powershell
   Test-Path "D:\Spell\Installed\Steam\steamapps\common\Sirocco\Sirocco_Data\Plugins\x86_64\steam_api64.dll"
   # Should return: True
   ```

3. **Launch Sirocco and test your mod**
   - Press F5 to open lobby browser
   - Try to create a lobby
   - Check MelonLoader logs for errors

## Expected Results

### ✅ If Test PASSES (mod works):
- Lobby browser opens successfully
- Can create/join lobbies
- No "Failed to load steam_api64.dll" errors in logs
- **Conclusion**: Steamworks.NET finds the game's DLL automatically
- **Action**: Remove the copy step from `.csproj` (lines 65-67)

### ❌ If Test FAILS (mod crashes):
- Mod fails to load
- Errors in MelonLoader logs: "Failed to load steam_api64.dll" or "SteamAPI_Init() failed"
- **Conclusion**: Steamworks.NET needs the DLL in the Mods folder
- **Action**: Keep bundling steam_api64.dll (but document this in README)

## How Steamworks.NET Loads the Native DLL

Steamworks.NET uses P/Invoke to load `steam_api64.dll`. The DLL search order on Windows is:

1. **Application directory** (where the .exe is) - `Sirocco.exe` directory
2. **System directories** (System32, etc.)
3. **Current working directory**
4. **Directories in PATH**

For Unity games, the native plugins are typically in:
- `<Game>_Data\Plugins\x86_64\` (64-bit Windows)
- `<Game>_Data\Plugins\x86\` (32-bit Windows)

Unity automatically adds these directories to the DLL search path, so Steamworks.NET should find it.

## Current Situation

**Game has steam_api64.dll at:**
```
D:\Spell\Installed\Steam\steamapps\common\Sirocco\Sirocco_Data\Plugins\x86_64\steam_api64.dll
```

**Your .csproj currently copies it to:**
```
SLL\SteamLobbyLib\bin\Release\net6.0\steam_api64.dll
```

**And then to:**
```
D:\Spell\Installed\Steam\steamapps\common\Sirocco\Mods\steam_api64.dll
```

## Recommendation

**Test first**, then decide:

1. Run the test above
2. If it works without the bundled DLL:
   - Remove lines 65-67 from `.csproj`
   - Do NOT include `steam_api64.dll` in releases
   - Update README to note it's loaded from game installation
3. If it fails:
   - Keep bundling it for now
   - Add disclaimer in README
   - Consider this a "convenience" inclusion

## Next Steps After Test

Run this test now, then report results back.
