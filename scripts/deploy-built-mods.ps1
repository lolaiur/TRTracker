$ErrorActionPreference = "Stop"

$Repo = Split-Path -Parent $PSScriptRoot
$GamePath = "C:\Games\Steam\steamapps\common\Travellers Rest"
$DllNames = @("TRTracker.dll", "TRBarrels.dll", "TRBar.dll", "TRStats.dll", "TRAutoloader.dll")

$Assembly = Get-ChildItem -Path $GamePath -Filter "Assembly-CSharp.dll" -Recurse -ErrorAction Stop | Select-Object -First 1
if (-not $Assembly) {
    throw "Assembly-CSharp.dll not found under $GamePath"
}

$ManagedPath = $Assembly.DirectoryName
$BepInExPath = [System.IO.Path]::GetFullPath((Join-Path $ManagedPath "..\..\BepInEx"))
if (-not (Test-Path $BepInExPath)) {
    $BepInExPath = Join-Path $GamePath "BepInEx"
}
if (-not (Test-Path $BepInExPath)) {
    $BepInExPath = Join-Path $GamePath "Windows\BepInEx"
}
if (-not (Test-Path $BepInExPath)) {
    throw "BepInEx not found under $GamePath"
}

$PluginsPath = Join-Path $BepInExPath "plugins"
if (-not (Test-Path $PluginsPath)) {
    New-Item -ItemType Directory -Force -Path $PluginsPath | Out-Null
}

$Stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$BackupPath = Join-Path (Join-Path $BepInExPath "TRModBackups") "pre-game-update-$Stamp"
New-Item -ItemType Directory -Force -Path $BackupPath | Out-Null

foreach ($DllName in $DllNames) {
    $InstalledDll = Join-Path $PluginsPath $DllName
    if (Test-Path $InstalledDll) {
        Copy-Item -LiteralPath $InstalledDll -Destination (Join-Path $BackupPath $DllName) -Force
    }
}

$RollbackLines = @(
    '$ErrorActionPreference = "Stop"',
    '$BackupDir = Split-Path -Parent $MyInvocation.MyCommand.Path',
    ('$PluginsDir = "' + $PluginsPath + '"'),
    '$DllNames = @("TRTracker.dll", "TRBarrels.dll", "TRBar.dll", "TRStats.dll", "TRAutoloader.dll")',
    'foreach ($DllName in $DllNames) {',
    '    $SourceDll = Join-Path $BackupDir $DllName',
    '    if (Test-Path $SourceDll) {',
    '        Copy-Item -LiteralPath $SourceDll -Destination (Join-Path $PluginsDir $DllName) -Force',
    '        Write-Host "Restored $DllName"',
    '    } else {',
    '        Write-Warning "Backup missing: $DllName"',
    '    }',
    '}',
    'Write-Host "Rollback complete."'
)
Set-Content -LiteralPath (Join-Path $BackupPath "rollback.ps1") -Value $RollbackLines -Encoding UTF8

foreach ($DllName in $DllNames) {
    $BuiltDll = Join-Path (Join-Path $Repo "dll") $DllName
    if (-not (Test-Path $BuiltDll)) {
        throw "Built DLL missing: $BuiltDll"
    }

    Copy-Item -LiteralPath $BuiltDll -Destination (Join-Path $PluginsPath $DllName) -Force
}

Write-Host "Deployed DLLs:"
Get-ChildItem -LiteralPath $PluginsPath -Filter "TR*.dll" | Select-Object Name, Length, LastWriteTime
Write-Host "Backup: $BackupPath"
Write-Host "Rollback: $(Join-Path $BackupPath 'rollback.ps1')"
