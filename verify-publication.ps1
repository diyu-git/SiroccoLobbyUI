# Pre-Publication Verification Script
# Run this script to verify your repository is ready for GitHub publication

Write-Host "`n=== SiroccoLobbyUI - GitHub Publication Verification ===" -ForegroundColor Cyan
Write-Host "This script checks if your repository is ready for safe publication`n" -ForegroundColor Gray

$repoRoot = "d:\source\SiroccoLobbySystem"
$errors = @()
$warnings = @()
$passed = @()

# Change to repo directory
Set-Location $repoRoot

Write-Host "[1/8] Checking .gitignore configuration..." -ForegroundColor Yellow

# Check .gitignore exists
if (Test-Path ".gitignore") {
    $gitignore = Get-Content ".gitignore" -Raw
    
    # Check critical exclusions
    if ($gitignore -match "dnspyAssembly") {
        $passed += "✅ dnspyAssembly is gitignored"
    } else {
        $errors += "❌ dnspyAssembly is NOT in .gitignore (CRITICAL)"
    }
    
    if ($gitignore -match "steam_api") {
        $passed += "✅ steam_api DLLs are gitignored"
    } else {
        $warnings += "⚠️ steam_api DLLs should be in .gitignore"
    }
    
    if ($gitignore -match "bin/" -and $gitignore -match "obj/") {
        $passed += "✅ Build artifacts (bin/obj) are gitignored"
    } else {
        $warnings += "⚠️ bin/ and obj/ should be gitignored"
    }
} else {
    $errors += "❌ .gitignore file not found"
}

Write-Host "[2/8] Checking for proprietary files..." -ForegroundColor Yellow

# Check if dnspyAssembly exists and is not gitignored
if (Test-Path "dnspyAssembly") {
    if (git check-ignore "dnspyAssembly" 2>$null) {
        $passed += "✅ dnspyAssembly exists but is properly gitignored"
    } else {
        $errors += "❌ dnspyAssembly exists and is NOT gitignored (CRITICAL - DMCA RISK)"
    }
}

# Check for steam_api64.dll in tracked files
if (Test-Path "SLL\steamworks\Windows-x64\steam_api64.dll") {
    if (git check-ignore "SLL\steamworks\Windows-x64\steam_api64.dll" 2>$null) {
        $passed += "✅ steam_api64.dll is properly gitignored"
    } else {
        $warnings += "⚠️ steam_api64.dll exists and may be tracked by git"
    }
}

Write-Host "[3/8] Checking documentation files..." -ForegroundColor Yellow

$requiredDocs = @("README.md", "LICENSE", "BUILDING.md", "THIRD_PARTY_LICENSES.md")
foreach ($doc in $requiredDocs) {
    if (Test-Path $doc) {
        $passed += "✅ $doc exists"
    } else {
        $warnings += "⚠️ $doc not found (recommended)"
    }
}

Write-Host "[4/8] Checking LICENSE file..." -ForegroundColor Yellow

if (Test-Path "LICENSE") {
    $license = Get-Content "LICENSE" -Raw
    if ($license -match "\[Your Name\]") {
        $warnings += "⚠️ LICENSE still contains placeholder '[Your Name]'"
    } else {
        $passed += "✅ LICENSE appears to be customized"
    }
}

Write-Host "[5/8] Checking build configuration..." -ForegroundColor Yellow

if (Test-Path "Directory.Build.props") {
    $passed += "✅ Directory.Build.props exists"
} else {
    $warnings += "⚠️ Directory.Build.props not found (recommended for build paths)"
}

if (Test-Path "SLL\SteamLobbyLib\SteamLobbyLib.csproj") {
    $csproj = Get-Content "SLL\SteamLobbyLib\SteamLobbyLib.csproj" -Raw
    if ($csproj -match "<Private>False</Private>") {
        $passed += "✅ .csproj uses <Private>False</Private> for dependencies"
    } else {
        $warnings += "⚠️ .csproj may not be configured to prevent DLL copying"
    }
}

Write-Host "[6/8] Testing clean build..." -ForegroundColor Yellow

# Clean build directories
if (Test-Path "SLL\SteamLobbyLib\bin") {
    Remove-Item "SLL\SteamLobbyLib\bin" -Recurse -Force -ErrorAction SilentlyContinue
}
if (Test-Path "SLL\SteamLobbyLib\obj") {
    Remove-Item "SLL\SteamLobbyLib\obj" -Recurse -Force -ErrorAction SilentlyContinue
}

# Attempt build
Write-Host "  Building project..." -ForegroundColor Gray
$buildOutput = dotnet build "SLL\SteamLobbyLib\SteamLobbyLib.csproj" -c Release 2>&1

if ($LASTEXITCODE -eq 0) {
    $passed += "✅ Project builds successfully"
    
    # Check output directory
    $outputDir = "SLL\SteamLobbyLib\bin\Release\net6.0"
    if (Test-Path $outputDir) {
        $dlls = Get-ChildItem $outputDir -Filter "*.dll" | Select-Object -ExpandProperty Name
        
        Write-Host "  Output DLLs: $($dlls -join ', ')" -ForegroundColor Gray
        
        # Check for expected DLLs
        if ($dlls -contains "SiroccoLobbyUI.dll") {
            $passed += "✅ SiroccoLobbyUI.dll built successfully"
        } else {
            $errors += "❌ SiroccoLobbyUI.dll not found in output"
        }
        
        if ($dlls -contains "Steamworks.NET.dll") {
            $passed += "✅ Steamworks.NET.dll in output (MIT licensed, OK to redistribute)"
        }
        
        # Check for unwanted DLLs
        if ($dlls -contains "steam_api64.dll") {
            $warnings += "⚠️ steam_api64.dll in output (should NOT be in release ZIP)"
        }
        
        if ($dlls -contains "MelonLoader.dll") {
            $errors += "❌ MelonLoader.dll in output (should NOT be redistributed)"
        }
        
        if ($dlls -contains "Il2Cppmscorlib.dll") {
            $errors += "❌ Il2Cppmscorlib.dll in output (game assembly, should NOT be redistributed)"
        }
    }
} else {
    $errors += "❌ Build failed - check build output above"
}

Write-Host "[7/8] Checking git repository status..." -ForegroundColor Yellow

# Check if git repo exists
if (Test-Path ".git") {
    $passed += "✅ Git repository initialized"
    
    # Check for tracked DLLs
    $trackedDlls = git ls-files | Select-String -Pattern '\.(dll|exe)$'
    if ($trackedDlls) {
        Write-Host "  Tracked DLLs/EXEs:" -ForegroundColor Gray
        foreach ($dll in $trackedDlls) {
            Write-Host "    $dll" -ForegroundColor Gray
            if ($dll -notmatch "Steamworks\.NET\.dll") {
                $warnings += "⚠️ Tracked DLL: $dll (verify this should be in repository)"
            }
        }
    } else {
        $passed += "✅ No DLLs/EXEs tracked by git (or only Steamworks.NET.dll)"
    }
} else {
    $warnings += "⚠️ Git repository not initialized (run 'git init')"
}

Write-Host "[8/8] Checking README accuracy..." -ForegroundColor Yellow

if (Test-Path "README.md") {
    $readme = Get-Content "README.md" -Raw
    
    if ($readme -match "diyu-git") {
        $passed += "✅ README uses correct GitHub username (diyu-git)"
    } else {
        $warnings += "⚠️ README may have incorrect GitHub username"
    }
    
    if ($readme -match "F5") {
        $passed += "✅ README mentions F5 keybind"
    } else {
        $warnings += "⚠️ README may have incorrect keybind"
    }
    
    if ($readme -match "free-to-play") {
        $passed += "✅ README correctly states Sirocco is free-to-play"
    } else {
        $warnings += "⚠️ README may incorrectly state Sirocco requires purchase"
    }
}

# Print results
Write-Host "`n=== VERIFICATION RESULTS ===" -ForegroundColor Cyan

if ($passed.Count -gt 0) {
    Write-Host "`n✅ PASSED ($($passed.Count)):" -ForegroundColor Green
    foreach ($item in $passed) {
        Write-Host "  $item" -ForegroundColor Green
    }
}

if ($warnings.Count -gt 0) {
    Write-Host "`n⚠️ WARNINGS ($($warnings.Count)):" -ForegroundColor Yellow
    foreach ($item in $warnings) {
        Write-Host "  $item" -ForegroundColor Yellow
    }
}

if ($errors.Count -gt 0) {
    Write-Host "`n❌ ERRORS ($($errors.Count)):" -ForegroundColor Red
    foreach ($item in $errors) {
        Write-Host "  $item" -ForegroundColor Red
    }
    Write-Host "`n⚠️ FIX ERRORS BEFORE PUBLISHING TO GITHUB" -ForegroundColor Red
} else {
    Write-Host "`n🎉 NO CRITICAL ERRORS - Repository appears ready for publication!" -ForegroundColor Green
    
    if ($warnings.Count -eq 0) {
        Write-Host "✨ PERFECT - No warnings either!" -ForegroundColor Green
    } else {
        Write-Host "Review warnings above and fix if necessary." -ForegroundColor Yellow
    }
}

Write-Host "`n=== NEXT STEPS ===" -ForegroundColor Cyan
Write-Host "1. Fix any errors or warnings above" -ForegroundColor White
Write-Host "2. Review: publication_summary.md" -ForegroundColor White
Write-Host "3. Create release ZIP (see publication_summary.md)" -ForegroundColor White
Write-Host "4. Push to GitHub: git remote add origin https://github.com/diyu-git/SiroccoLobbyUI.git" -ForegroundColor White
Write-Host "5. Create GitHub release and attach ZIP" -ForegroundColor White
Write-Host ""
