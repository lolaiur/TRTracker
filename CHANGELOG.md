# Changelog

## 2026-04-10

### TRTracker 1.3.0

- Rebuilt against the current Traveler's Rest assemblies after the latest obfuscation changes.
- Replaced fragile singleton lookups with more resilient runtime reflection where needed.
- Moved the tavern-open hook to the live `get_open` accessor.
- Reduced tracker UI refresh frequency to cut hot-path reflection and UI churn.
- Improved UI interaction by bringing the window to the front on click or drag.
- Updated the tracker window title and log version strings.

### TRBar 1.3.0

- Rebuilt against the current game assemblies and updated tavern-open access to the current runtime shape.
- Collapsed the bar UI into a single managed refresh coroutine to avoid duplicate loops.
- Reduced per-update work by caching the coroutine and only refreshing on the configured interval.
- Normalized the canvas to screen-space overlay for more predictable layering.
- Added bring-to-front behavior so the bar window can be focused without moving other windows first.
- Spread the default window position to reduce overlap with other mod panels.
- Updated the bar window title and log version strings.

### TRBarrels 1.3.0

- Rebuilt against the current game assemblies and replaced the old static world-time property lookup with type-based runtime lookup.
- Replaced the expensive broad `FindObjectsOfType<MonoBehaviour>()` sweep with targeted aging-barrel discovery.
- Split barrel scanning from list refresh timing so discovery runs less often than UI updates.
- Normalized the canvas to screen-space overlay for more predictable layering.
- Added bring-to-front behavior so the barrels window can be focused directly.
- Spread the default window position to reduce overlap with other mod panels.
- Updated the barrels window title and version strings.

### TRStats 1.1.0

- Rebuilt against the current game assemblies and updated crop, fuel, and common-reference access for the latest obfuscation pass.
- Replaced brittle private method-name patches with dynamic target resolution for bucket/well behavior.
- Collapsed the stats UI into a single managed refresh coroutine to avoid duplicate loops.
- Throttled info text updates instead of rebuilding them every frame.
- Added bring-to-front behavior so the stats window no longer permanently blocks lower windows.
- Updated the stats window title, plugin version constant, and log version strings.

### Tooling and Workflows

- Replaced the old dump-only obfuscation audit with a live assembly audit script.
- Updated validation checks for the current plugin versions and added TRStats source validation.
- Updated release workflow packaging and release notes metadata to include TRStats and the tracked `build` output directory.
