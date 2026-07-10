# TRTracker - Traveler's Rest Mod Collection

[![CodeQL](https://github.com/lolaiur/TRTracker/actions/workflows/codeql.yml/badge.svg)](https://github.com/lolaiur/TRTracker/actions/workflows/codeql.yml)
[![Validate Code](https://github.com/lolaiur/TRTracker/actions/workflows/validate.yml/badge.svg)](https://github.com/lolaiur/TRTracker/actions/workflows/validate.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A set of BepInEx plugins for Traveler's Rest: live tavern stats, barrel and bar panels, gameplay cheats, and automated food and drink loading.

## Supported game version: 0.7.5.3


<img width="2557" height="972" alt="image" src="https://github.com/user-attachments/assets/3ab57655-5fba-4b39-a70c-d407a53b087f" />


Five DLLs are included. Each one works on its own, or you can install all of them. See [CHANGELOG.md](CHANGELOG.md) for per-mod version history.


## Mods Included

Each panel opens and closes with the key shown next to it.

### 🎯 TRTracker (F1)
Live tavern stats: money earned per minute and per session, profit, time open, heat, dirt, comfort, and reputation progress.

### 🍺 TRBarrels (F2)
Tracks aging barrels and shows aging progress and ready drinks across all of them.

### 📊 TRBar (F3)
Extra bar panel showing the drinks on tap, current stock, and flow rates.

### 🍔 TRStats (F4)
Cheats and gameplay tweaks: player speed, customer capacity, price modifiers, employee work avoidance, infinite coal and water, crop watering and instant-grow tools, and one-click animal water filling.

### 🔄 TRAutoloader (F5)
Keeps the bar menu stocked and tops off beer taps, kegs, and bar barrels from a loader container you assign. Each dispenser keeps the drink it already holds (it never mixes drinks, so put the drink you want in a dispenser once and it stays topped off). It has an on/off toggle, a configurable refill interval, and a rolling action feed in the panel.

## Installation

### Prerequisites

Install BepInEx 5.x (64-bit) first.

#### Installing BepInEx 5.x (64-bit)

1. Download **BepInEx 5.4.23.4 x64** from [BepInEx Releases](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.4). Get `BepInEx_win_x64_5.4.23.4.zip`. Use the x64 (64-bit) build, not x86.
2. Extract the ZIP into your Traveler's Rest folder. On Steam the game files live under a `Windows` subfolder, so the layout looks like:
   ```
   Travellers Rest/
   └── Windows/
       ├── BepInEx/
       │   ├── core/
       │   ├── plugins/   <- Mods go here
       │   └── config/
       ├── doorstop_config.ini
       ├── winhttp.dll
       └── TravellersRest.exe
   ```
3. Run the game once to initialize BepInEx (a console window will appear), then close it.

#### Installing the mods

1. Download the latest release from [Releases](https://github.com/lolaiur/TRTracker/releases).
2. Extract `TRTracker-vX.X.X.zip`.
3. Copy the `.dll` files into `Travellers Rest/Windows/BepInEx/plugins/`.
4. Launch the game. Open the panels with F1 through F5.

## Requirements

- Traveler's Rest (Steam)
- BepInEx 5.4.23.4 x64 or newer ([Download](https://github.com/BepInEx/BepInEx/releases))

## Source Code

All source is in this repository:

- `Plugins/TRTrackerPlugin/TRTrackerPlugin.cs`
- `Plugins/TRBarPlugin/TRBarPlugin.cs`
- `Plugins/TRBarrelsPlugin/TRBarrelsPlugin.cs`
- `Plugins/TRStatsPlugin/TRStatsPlugin.cs` and `Patches.cs`
- `Plugins/TRAutoloaderPlugin/TRAutoloaderPlugin.cs`

The release DLLs are built from this source. The version string in each plugin matches its release version.

## Building Locally

These mods reference game assemblies, so you need the game installed to compile them.

### Prerequisites
- .NET Framework 4.x or Mono
- A C# compiler (Visual Studio, Rider, or `csc.exe`)
- Traveler's Rest installed with BepInEx

### Build Steps

1. Find the managed assemblies at `Travellers Rest/Windows/TravellersRest_Data/Managed/`.
2. Compile each plugin. Example for TRTracker:
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
3. Or run the included PowerShell script on Windows. It locates the game install, compiles every plugin, and copies the DLLs into `BepInEx/plugins/`:
   ```powershell
   .\build_all_mods.ps1
   ```

`scripts/audit-obfuscation.ps1` checks that every game member the plugins use still exists in your installed `Assembly-CSharp.dll`. Run it after a game update to catch obfuscation breaks.

## Development

GitHub Actions handle validation, security scanning, and releases:

- **CodeQL Security Scan** runs on every push/PR and weekly.
- **Validate Code** runs on every push/PR to confirm source files and DLLs are present and versions match.
- **Create Release** is a manual workflow for tagged releases.

Security scan results are in the [Security tab](https://github.com/lolaiur/TRTracker/security/code-scanning).

## Contributing

Pull requests are welcome. Fork the repo, work on a feature branch, and test your changes in-game before opening a PR.

## License

MIT. See the [LICENSE](LICENSE) file.

## Support

- Report issues: [GitHub Issues](https://github.com/lolaiur/TRTracker/issues)
- Game: [Traveler's Rest on Steam](https://store.steampowered.com/app/1139980/Travellers_Rest/)

## Credits

Created by lolaiur for the Traveler's Rest modding community. Built with [BepInEx](https://github.com/BepInEx/BepInEx) and [HarmonyX](https://github.com/BepInEx/HarmonyX).
