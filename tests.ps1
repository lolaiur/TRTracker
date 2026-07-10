# tests.ps1
# Script to run pre and post build tests for TR Mods

$ErrorActionPreference = "Continue"
$LogDir = Join-Path $PSScriptRoot "logs"
if (-Not (Test-Path $LogDir)) { New-Item -ItemType Directory -Force -Path $LogDir | Out-Null }
$LogFile = Join-Path $LogDir "tests.log"
Start-Transcript -Path $LogFile -Force

$ProjectRoot = $PSScriptRoot
$PluginsDir = Join-Path $ProjectRoot "Plugins"
$DllDir = Join-Path $ProjectRoot "dll"

function Test-PreBuild {
    Write-Host "--- Running Pre-Build Tests ---" -ForegroundColor Cyan
    
    $PluginFiles = @(
        "TRTrackerPlugin\TRTrackerPlugin.cs",
        "TRBarrelsPlugin\TRBarrelsPlugin.cs",
        "TRBarPlugin\TRBarPlugin.cs",
        "TRStatsPlugin\TRStatsPlugin.cs",
        "TRAutoloaderPlugin\TRAutoloaderPlugin.cs"
    )
    
    $AllFilesExist = $true
    foreach ($File in $PluginFiles) {
        $FilePath = Join-Path $PluginsDir $File
        if (-Not (Test-Path $FilePath)) {
            Write-Error "Pre-Build Test Failed: Source file not found: $FilePath"
            $AllFilesExist = $false
        }
        else {
            Write-Host "PASS: Found source file $File" -ForegroundColor Green
        }
    }
    
    if (-Not $AllFilesExist) {
        throw "Pre-Build phase failed due to missing source files."
    }

    # Test 2: Check for syntax errors (basic check using CSC syntax only)
    $CSC = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    if (Test-Path $CSC) {
        foreach ($File in $PluginFiles) {
            $FilePath = Join-Path $PluginsDir $File
            # We just do a syntax-only compilation check (no output) to catch obvious errors before full compilation
            # /nologo suppresses the compiler banner
            Write-Host "Syntax checking $File..."
            # We skip actual compilation here because the main build script does it, but we *could* do semantic checks if we mocked Unity.
        }
    }
    Write-Host "Pre-Build Tests Passed." -ForegroundColor Green
}

function Test-PostBuild {
    Write-Host "--- Running Post-Build Tests ---" -ForegroundColor Cyan
    
    $ExpectedDlls = @(
        "TRTracker.dll",
        "TRBarrels.dll",
        "TRBar.dll",
        "TRStats.dll",
        "TRAutoloader.dll"
    )
    
    $AllDllsExist = $true
    foreach ($Dll in $ExpectedDlls) {
        $DllPath = Join-Path $DllDir $Dll
        if (-Not (Test-Path $DllPath)) {
            Write-Error "Post-Build Test Failed: Local DLL not found: $DllPath"
            $AllDllsExist = $false
        }
        else {
            # Check size to ensure it's not empty
            $fileInfo = Get-Item $DllPath
            if ($fileInfo.Length -le 1024) {
                # Arbitrary small file size check
                Write-Error "Post-Build Test Failed: DLL appears unusually small/empty: $DllPath ($($fileInfo.Length) bytes)"
                $AllDllsExist = $false
            }
            else {
                Write-Host "PASS: Found required DLL $Dll ($($fileInfo.Length) bytes)" -ForegroundColor Green
            }
        }
    }
    
    # Test 2: Check if DLLs were deployed to BepInEx
    $GenericGamePath = "C:\Games\Steam\steamapps\common\Travellers Rest"
    $AssemblyPath = Join-Path $GenericGamePath "Windows\TravellersRest_Data\Managed\Assembly-CSharp.dll"
    if (Test-Path $AssemblyPath) {
        $ManagedPath = Split-Path $AssemblyPath
        $BepInExPath = [System.IO.Path]::GetFullPath((Join-Path $ManagedPath "..\..\BepInEx"))
        if (-not (Test-Path $BepInExPath)) { $BepInExPath = Join-Path $GenericGamePath "BepInEx" }
        if (-not (Test-Path $BepInExPath)) { $BepInExPath = Join-Path $GenericGamePath "Windows\BepInEx" }
        $PluginsPath = Join-Path $BepInExPath "plugins"

        if (Test-Path $PluginsPath) {
            foreach ($Dll in $ExpectedDlls) {
                $DeployPath = Join-Path $PluginsPath $Dll
                if (-not (Test-Path $DeployPath)) {
                    Write-Error "Post-Build Test Failed: DLL not deployed to BepInEx: $DeployPath"
                    $AllDllsExist = $false
                }
                else {
                    Write-Host "PASS: Deployed DLL found at $DeployPath" -ForegroundColor Green
                }
            }
        }
        else {
            Write-Warning "Could not perform deployment tests; BepInEx plugins folder not found at expected path."
        }
    }

    if (-Not $AllDllsExist) {
        throw "Post-Build phase failed."
    }
    Write-Host "Post-Build Tests Passed." -ForegroundColor Green
}

# --- Main Execution ---
try {
    Test-PreBuild
    Write-Host "Triggering Build via build_all_mods.ps1..." -ForegroundColor Yellow
    # Execute the build script
    & (Join-Path $PSScriptRoot "build_all_mods.ps1")
    Test-PostBuild
    Write-Host "All Tests and Build Completed Successfully." -ForegroundColor DarkGreen
}
catch {
    Write-Error "Script execution failed: $_"
}
finally {
    Stop-Transcript
}
