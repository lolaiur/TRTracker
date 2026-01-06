# Traveler's Rest Tracker Mod

A gameplay tracking mod for Traveler's Rest.

## Building

```bash
dotnet restore
dotnet build --configuration Release
```

## Installation

1. Download the latest release from the [Releases](https://github.com/lolaiur/TRTracker/releases) page
2. Extract the ZIP file
3. Copy `TRTracker.dll` to your Traveler's Rest mods folder

## Releasing

To create a new release:

```bash
git tag v1.0.0
git push origin v1.0.0
```

The GitHub Actions workflow will automatically build and create a release with the compiled mod.

## Versioning

This project uses [Semantic Versioning](https://semver.org/):
- **v1.0.0** - Major.Minor.Patch
- Major: Breaking changes
- Minor: New features (backwards compatible)
- Patch: Bug fixes