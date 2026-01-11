# TRTracker - Traveler's Rest Mod Collection

[![CodeQL](https://github.com/lolaiur/TRTracker/actions/workflows/codeql.yml/badge.svg)](https://github.com/lolaiur/TRTracker/actions/workflows/codeql.yml)
[![Validate Code](https://github.com/lolaiur/TRTracker/actions/workflows/validate.yml/badge.svg)](https://github.com/lolaiur/TRTracker/actions/workflows/validate.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A collection of BepInEx plugins that enhance gameplay in Traveler's Rest.
<img width="1418" height="643" alt="image" src="https://github.com/user-attachments/assets/10f860d0-ab79-4a52-a585-ec54647c22ab" />

The mod comes in the three DLLs listed below. Each can be installed individually or all together. 

## Mods Included

### 🎯 TRTracker (v1.1.1)
Gameplay tracking and statistics system for monitoring your tavern's performance.

### 🍺 TRBarrels (v1.1.1)
Enhanced barrel management and tracking features.

### 📊 TRBar (v1.1.1)
Improved bar UI with additional functionality.

## Installation

### Prerequisites

You need BepInEx 5.x (64-bit) installed first.

#### Installing BepInEx 5.x (64-bit)

1. Download **BepInEx 5.4.23.4 x64** from [BepInEx Releases](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.4)
   - Get the file: `BepInEx_win_x64_5.4.23.4.zip`
   - ⚠️ **Important:** Use the x64 (64-bit) version, not x86

2. Extract the ZIP file to your Traveler's Rest game folder:
   ```
   Steam/steamapps/common/Travellers Rest/
   ```
   The folder structure should look like:
   ```
   Travellers Rest/
   ├── BepInEx/
   │   ├── core/
   │   ├── plugins/  <- Mods go here
   │   └── config/
   ├── doorstop_config.ini
   ├── winhttp.dll
   └── TravellersRest.exe
   ```

3. Run the game once to initialize BepInEx (you'll see a console window)
4. Close the game - BepInEx is now installed!

#### Installing TRTracker Mods

1. Download the latest release from [Releases](https://github.com/lolaiur/TRTracker/releases)
2. Extract `TRTracker-vX.X.X.zip`
3. Copy all `.dll` files to:
   ```
   Travellers Rest/BepInEx/plugins/
   ```
4. Launch the game

## Requirements

- Traveler's Rest (Steam version)
- BepInEx 5.4.23.2 x64 or newer ([Download](https://github.com/BepInEx/BepInEx/releases))

## Source Code & Transparency

All source code is available in this repository for review:

- `Plugins/TRTrackerPlugin/TRTrackerPlugin.cs`
- `Plugins/TRBarPlugin/TRBarPlugin.cs`
- `Plugins/TRBarrelsPlugin/TRBarrelsPlugin.cs`

The pre-built DLLs in releases are compiled from this exact source code. Version numbers in the source files match the release versions for verification.

## Building Locally

These mods require references to game assemblies to compile. If you own the game and want to build from source:

### Prerequisites
- .NET Framework 4.x or Mono
- C# Compiler (Visual Studio, Rider, or `csc.exe`)
- Traveler's Rest installed
- BepInEx installed in your game directory

### Build Steps

1. Locate your game's managed assemblies:
   ```
   Travellers Rest/Travellers Rest_Data/Managed/
   ```

2. Compile each plugin (example for TRTracker):
   ```bash
   csc /t:library /out:TRTracker.dll \
       /r:"path/to/BepInEx/core/BepInEx.dll" \
       /r:"path/to/BepInEx/core/0Harmony.dll" \
       /r:"path/to/Managed/Assembly-CSharp.dll" \
       /r:"path/to/Managed/UnityEngine.dll" \
       /r:"path/to/Managed/UnityEngine.CoreModule.dll" \
       /r:"path/to/Managed/UnityEngine.UI.dll" \
       Plugins/TRTrackerPlugin/TRTrackerPlugin.cs
   ```

3. Or use the included PowerShell script (Windows):
   ```powershell
   # Edit paths in build_all_mods.ps1 first
   .\build_all_mods.ps1
   ```

## Development

### Workflow

This repository uses GitHub Actions for validation, security, and releases:

- **CodeQL Security Scan** - Runs on every push/PR and weekly to detect security vulnerabilities
- **Validate Code** - Runs on every push/PR to verify source files and DLLs exist
- **Create Release** - Manual workflow to create tagged releases

Security scanning results are visible in the [Security tab](https://github.com/lolaiur/TRTracker/security/code-scanning).

## License

MIT License - See LICENSE file for details

## Contributing

Contributions welcome! Please:

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Test thoroughly with the game
5. Submit a pull request

## Support

- Report issues: [GitHub Issues](https://github.com/lolaiur/TRTracker/issues)
- Game: [Traveler's Rest on Steam](https://store.steampowered.com/app/1139980/Travellers_Rest/)

## Credits

Created by lolaiur for the Traveler's Rest modding community.

Built with [BepInEx](https://github.com/BepInEx/BepInEx) and [HarmonyX](https://github.com/BepInEx/HarmonyX).




