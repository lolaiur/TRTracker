# TRTracker - Traveler's Rest Mod Collection

A collection of BepInEx plugins that enhance gameplay in Traveler's Rest.

## Mods Included

### 🎯 TRTracker (v1.1.1)
Gameplay tracking and statistics system for monitoring your tavern's performance.

### 🍺 TRBarrels (v1.1.1)
Enhanced barrel management and tracking features.

### 📊 TRBar (v1.1.1)
Improved bar UI with additional functionality.

## Installation

1. Download the latest release from [Releases](https://github.com/lolaiur/TRTracker/releases)
2. Extract `TRTracker-vX.X.X.zip`
3. Copy all `.dll` files to:
   ```
   Travellers Rest/BepInEx/plugins/
   ```
4. Launch the game

## Requirements

- Traveler's Rest (Steam version)
- [BepInEx 5.x](https://github.com/BepInEx/BepInEx/releases) installed

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

This repository uses GitHub Actions for validation and releases:

- **Validate Code** - Runs on every push/PR to verify source files and DLLs exist
- **Create Release** - Manual workflow to create tagged releases

### Creating a Release

1. Ensure all source files are updated with the new version number
2. Compile locally and update DLLs in `build/`
3. Commit and push changes
4. Go to Actions → Create Release → Run workflow
5. Enter the version number (e.g., `1.2.0`)

### Version Numbering

Each plugin maintains its own version in the `BepInPlugin` attribute:
```csharp
[BepInPlugin("com.lolaiur.trtracker", "Tavern Tracker", "1.1.1")]
```

The release version (e.g., v1.1.1) matches the plugin versions when they're aligned.

## Why Pre-built DLLs?

Game mods require references to proprietary game assemblies that can't be included in public repositories. The source code is provided for transparency and verification, while pre-built DLLs enable easy installation.

Users can:
- ✅ Review all source code before using
- ✅ Verify version numbers match between source and DLLs
- ✅ Build from source if they own the game
- ✅ See exactly what the code does

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
