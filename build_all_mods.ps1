
# Power-Install Script for ALL TR Mods
# Compiles TRTracker, TRBarrels, and TRBar and installs to BepInEx/plugins

# Setup Logging
$LogDir = Join-Path $PSScriptRoot "logs"
if (-Not (Test-Path $LogDir)) { New-Item -ItemType Directory -Force -Path $LogDir | Out-Null }
$LogFile = Join-Path $LogDir "build_all_v2.log"
Start-Transcript -Path $LogFile -Force

# 1. Path Setup
$GenericGamePath = "C:\Games\Steam\steamapps\common\Travellers Rest"
Write-Host "Searching for Managed folder in: $GenericGamePath" -ForegroundColor Cyan

# Find Managed Folder
$Assembly = Get-ChildItem -Path $GenericGamePath -Filter "Assembly-CSharp.dll" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $Assembly) {
    Write-Error "Could not find Assembly-CSharp.dll in $GenericGamePath"
    exit
}
$ManagedPath = $Assembly.DirectoryName
Write-Host "Found Managed Path: $ManagedPath" -ForegroundColor Green

$BepInExPath = Join-Path $ManagedPath "..\..\BepInEx" 
# Normalize
$BepInExPath = [System.IO.Path]::GetFullPath($BepInExPath)
# Check if BepInEx is there, common install is in GameRoot, not GameRoot/Windows/BepInEx
# If not found, try GameRoot/BepInEx
if (-not (Test-Path $BepInExPath)) {
    $BepInExPath = Join-Path $GenericGamePath "BepInEx"
}
if (-not (Test-Path $BepInExPath)) {
    # Check Windows/BepInEx
    $BepInExPath = Join-Path $GenericGamePath "Windows\BepInEx"
}

$PluginsPath = Join-Path $BepInExPath "plugins"

Write-Host "BepInEx Path: $BepInExPath" -ForegroundColor Cyan
Write-Host "Plugins Path: $PluginsPath" -ForegroundColor Cyan

if (-Not (Test-Path $BepInExPath)) {
    Write-Error "BepInEx not found! Please check path."
    exit
}

if (-Not (Test-Path $PluginsPath)) {
    New-Item -ItemType Directory -Force -Path $PluginsPath | Out-Null
}

# Find CSC
$CSC = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-Not (Test-Path $CSC)) {
    Write-Error "C# Compiler (csc.exe) not found."
    exit
}

# References
$RefList = @(
    "Assembly-CSharp.dll", "UnityEngine.dll", "UnityEngine.CoreModule.dll",
    "netstandard.dll", "Unity.TextMeshPro.dll", "UnityEngine.UI.dll",
    "UnityEngine.IMGUIModule.dll", "UnityEngine.TextRenderingModule.dll",
    "UnityEngine.InputLegacyModule.dll", "UnityEngine.UIModule.dll",
    "UnityEngine.TilemapModule.dll", "UnityEngine.GridModule.dll",
    "UnityEngine.Physics2DModule.dll", "Sirenix.OdinInspector.Attributes.dll",
    "Sirenix.Serialization.dll", "Sirenix.Utilities.dll", "Sirenix.Serialization.Config.dll",
    "PhotonUnityNetworking.dll", "PhotonRealtime.dll",
    "UnityEngine.TextRenderingModule.dll"
)

# BepInEx Refs
$BepRef = Join-Path $BepInExPath "core\BepInEx.dll"
$HarRef = Join-Path $BepInExPath "core\0Harmony.dll"

$RefString = "/r:`"$BepRef`" /r:`"$HarRef`""
foreach ($r in $RefList) {
    $p = Join-Path $ManagedPath $r
    if (Test-Path $p) {
        $RefString += " /r:`"$p`""
    }
    else {
        Write-Host "Missing Ref: $r (at $p)" -ForegroundColor Magenta
    }
}

# Output Dir
$LocalDllDir = Join-Path $PSScriptRoot "dll"
if (-Not (Test-Path $LocalDllDir)) { New-Item -ItemType Directory -Force -Path $LocalDllDir | Out-Null }

function Compile-Mod($SourceFile, $DllName) {
    Write-Host "Compiling $DllName..." -ForegroundColor Yellow
    $SourcePath = Join-Path $PSScriptRoot $SourceFile
    $OutDLL = Join-Path $LocalDllDir $DllName

    if (Test-Path $OutDLL) { Remove-Item $OutDLL }

    $Cmd = "& `"$CSC`" /t:library /out:`"$OutDLL`" $RefString `"$SourcePath`""
    # Write-Host "CMD: $Cmd"
    
    # Execute and capture output mixed
    try {
        $p = Start-Process -FilePath $CSC -ArgumentList "/t:library /out:`"$OutDLL`" $RefString `"$SourcePath`"" -NoNewWindow -Wait -PassThru
        if ($p.ExitCode -ne 0) {
            Write-Error "Compiler exited with code $($p.ExitCode)"
        }
    }
    catch {
        Write-Error "Exec failed: $_"
    }

    if (Test-Path $OutDLL) {
        Copy-Item $OutDLL $PluginsPath -Force
        Write-Host "SUCCESS: $DllName deployed." -ForegroundColor Green
    }
    else {
        Write-Host "FAILED: $DllName did not compile." -ForegroundColor Red
    }
}

# Execute
Compile-Mod "TRTrackerPlugin.cs" "TRTracker.dll"
Compile-Mod "TRBarrelsPlugin.cs" "TRBarrels.dll"
Compile-Mod "TRBarPlugin.cs" "TRBar.dll"

Stop-Transcript
