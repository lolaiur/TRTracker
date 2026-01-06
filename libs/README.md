# Game Assembly References

This folder should contain DLLs from your Traveler's Rest game installation for local development.

## Required Files

Copy these from your game's `Traveler's Rest_Data/Managed/` folder:

```
libs/
  ├── Assembly-CSharp.dll
  ├── UnityEngine.dll
  ├── UnityEngine.CoreModule.dll
  ├── UnityEngine.UI.dll
  └── UnityEngine.IMGUIModule.dll
```

**Location examples:**
- **Steam (Windows)**: `C:\Program Files (x86)\Steam\steamapps\common\Travelers Rest\Traveler's Rest_Data\Managed\`
- **Steam (Linux)**: `~/.steam/steam/steamapps/common/Travelers Rest/Traveler's Rest_Data/Managed/`

## Note

These files are **NOT** committed to the repository (they're in .gitignore). Each developer must copy them locally to build the mod.

**CI builds will be skipped** until these assemblies are present, as they cannot be distributed publicly.