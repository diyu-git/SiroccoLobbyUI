# Release Checklist - v1.0.0

## Pre-Release Verification

### Code Quality
- [x] All god classes documented in REFACTORING_PLAN.md
- [x] No critical TODOs or FIXMEs in production code
- [x] Version number updated in Plugin.cs (1.0.0)
- [x] Working tree is clean (no uncommitted changes)

### Documentation
- [x] README.md - Complete and up-to-date
- [x] INSTALLATION.md - Comprehensive installation guide
- [x] BUILDING.md - Build instructions verified
- [x] CHANGELOG.md - Created for v1.0.0
- [x] WORKING_MULTIPLAYER_FLOW.md - Flow documented
- [x] REFACTORING_PLAN.md - Technical debt documented

### Testing
- [x] Host can create lobby
- [x] Client can browse lobbies
- [x] Client can join lobby
- [x] Team/captain selection works
- [x] Ready state coordination works
- [x] Game start works (host → clients)
- [x] Captain mode works (snake draft)
- [x] Leave lobby works cleanly

### Build
- [ ] **TODO**: Clean build succeeds (`dotnet build -c Release`)
- [ ] **TODO**: Output DLLs verified:
  - [ ] SiroccoLobbyUI.dll
  - [ ] Steamworks.NET.dll
- [ ] **TODO**: No proprietary files in release (game DLLs, MelonLoader, etc.)
- [ ] **TODO**: Only mod code + Steamworks.NET (MIT licensed)

### Git
- [x] All changes committed
- [x] Branch: master
- [ ] **TODO**: Push to origin (`git push origin master`)
- [ ] **TODO**: Create annotated tag (`git tag -a v1.0.0 -m "Release v1.0.0"`)
- [ ] **TODO**: Push tag (`git push origin v1.0.0`)

## Release Steps

### 1. Build Release Binary
```powershell
# Clean previous builds
Remove-Item -Recurse -Force src\bin\Release -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force src\obj\Release -ErrorAction SilentlyContinue

# Build release
dotnet build src\SteamLobbyLib.csproj -c Release

# Verify output
Get-ChildItem src\bin\Release\net6.0\
```

**Expected Output**:
- `SiroccoLobbyUI.dll` (main mod)
- `Steamworks.NET.dll` (MIT-licensed wrapper)
- ⚠️ **DO NOT** include any other DLLs

### 2. Create Release Package
```powershell
# Create release directory
New-Item -ItemType Directory -Force release\v1.0.0

# Copy release files
Copy-Item src\bin\Release\net6.0\SiroccoLobbyUI.dll release\v1.0.0\
Copy-Item src\bin\Release\net6.0\Steamworks.NET.dll release\v1.0.0\

# Copy documentation
Copy-Item README.md release\v1.0.0\
Copy-Item LICENSE release\v1.0.0\
Copy-Item CHANGELOG.md release\v1.0.0\

# Create installation instructions
@"
Sirocco Lobby UI - v1.0.0

Installation:
1. Install MelonLoader in your Sirocco game directory
2. Copy both DLL files to: <Sirocco>\Mods\
   - SiroccoLobbyUI.dll
   - Steamworks.NET.dll
3. Launch Sirocco
4. Press F5 to open lobby browser

Full documentation: https://github.com/diyu-git/SiroccoLobbyUI

See CHANGELOG.md for release notes.
"@ | Out-File -FilePath release\v1.0.0\INSTALL.txt

# Create ZIP archive
Compress-Archive -Path release\v1.0.0\* -DestinationPath release\SiroccoLobbyUI-v1.0.0.zip -Force
```

### 3. Git Tag and Push
```powershell
# Commit CHANGELOG
git add CHANGELOG.md
git commit -m "Release v1.0.0 - Add CHANGELOG"

# Push commits
git push origin master

# Create annotated tag
git tag -a v1.0.0 -m "Release v1.0.0 - Initial stable release

Features:
- Steam lobby browser
- 5v5 multiplayer lobby room
- Captain mode with snake draft
- Full Steam P2P integration
- F5 toggle UI

See CHANGELOG.md for full release notes."

# Push tag
git push origin v1.0.0
```

### 4. Create GitHub Release
1. Go to: https://github.com/diyu-git/SiroccoLobbyUI/releases/new
2. Select tag: `v1.0.0`
3. Release title: **`v1.0.0 - Initial Release`**
4. Description: Copy from CHANGELOG.md (v1.0.0 section)
5. Attach file: `SiroccoLobbyUI-v1.0.0.zip`
6. Check: **"Set as the latest release"**
7. Click: **"Publish release"**

### 5. Update Documentation Links
Ensure these links work after release:
- [ ] README.md links to Releases page
- [ ] INSTALLATION.md links to latest release
- [ ] CHANGELOG.md links are functional

## Post-Release

### Announce
- [ ] Update project description on GitHub
- [ ] Post to relevant communities (if applicable)
- [ ] Update any external documentation

### Monitor
- [ ] Watch for installation issues
- [ ] Respond to GitHub issues
- [ ] Track bug reports

### Next Steps
- [ ] Create v2.0.0 milestone
- [ ] Start refactoring work (see REFACTORING_PLAN.md)
- [ ] Add issues for planned features

## Rollback Plan

If issues are found after release:

1. **Yank the release** (GitHub: Edit release → Check "Set as a pre-release")
2. **Fix the issue** on a hotfix branch
3. **Release v1.0.1** with fixes
4. **Update CHANGELOG.md** with hotfix notes

## Notes

- This is the first stable release after extensive testing
- Working multiplayer flow is fully documented
- Technical debt is documented for v2.0 refactoring
- All critical flows tested and working
- No known critical bugs

---

**Ready for Release**: ✅ YES (pending build and GitHub release creation)
