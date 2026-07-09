# Changelog

## 2026-07-09

### TRAutoloader 1.2.0

- Fixed food not loading and a recurring lag spike. A stale compatibility cache made the loader keep trying to add a food that no longer fit the bar menu (it spammed "cannot fit Pescado Asado" every tick and blocked all food). The cache is removed so fit is checked fresh each time.
- Raised the per-tick caps (drink targets, drink units, food moves) so more dispensers and kegs fill per tick instead of one at a time. Unity requires all game-object access on the main thread, so true background threading is not safe; raising the caps is how the loader attends multiple kegs in the same tick.
- Verbose debug logging is off by default (still available via Debug / VerboseDrinkLog) and the log clears on each launch. Only event-level lines (loads, assignments, errors) remain.
- Added a rolling "recent actions" feed to the F5 panel.

### TRStats 1.3.1

- Added diagnostics for the infinite-water cheat (logs the patch target counts and when the bucket patches fire) so we can confirm whether they attach and fire after the game update.

## 2026-07-08 (4)

### TRAutoloader 1.1.4

- Fixed wine kegs still not filling. They sit in unzoned bar space (zone=none), so even the relaxed "any tavern zone" check skipped them. The zone requirement is dropped entirely: any active drink dispenser is now eligible, and the existing "only top off dispensers that already hold a drink" rule still prevents unwanted fills.

## 2026-07-08 (3)

### TRAutoloader 1.1.3

- Fixed wine kegs (and other drink dispensers) not filling when placed outside the dining room zone. The loader only filled dispensers in the dining room, but kegs are often placed in the bar area, which is zoned separately. It now fills any dispenser that is inside a tavern zone.
- Enriched the verbose skip log so a skipped dispenser reports its type (tap or keg), its zone, and the drink it holds.

## 2026-07-08 (2)

### TRAutoloader 1.1.2

- The debug log now appends across game restarts instead of wiping on each launch, so test data is not lost between sessions while drink loading is being diagnosed. No loading behavior change.

## 2026-07-08

### TRAutoloader 1.1.1

- Fixed drinks not transferring into dispensers. The loader was using the game's AddItemInstance path for beer taps, but the dispenser's item filters reject cloned items there (it returns null even when the slot has room), so nothing moved. All dispensers (taps, kegs, and bar barrels) now use direct slot transfer, topping off by incrementing the existing drink's stack, which is how the game itself fills dispensers.

## 2026-07-07 (6)

### TRAutoloader 1.1.0

- Fixed the drink cap. The loader was capping some dispensers at the per-slot maxStack (for example cider at 10) instead of the dispenser's own maxStack (30, what every bar dispenser actually holds and what AddItemInstance accepts). Partially filled dispensers now top off to the full 30.
- Added a verbose entry log for each drink target so silent skips (for example a dispenser with a null slot) are visible in the trace.

## 2026-07-07 (5)

### TRAutoloader 1.0.9

- Fixed partially-filled dispensers never topping off. The autoloader kept a "learned capacity" per dispenser that locked in after a single failed add (for example, cider stuck at 10), then treated the dispenser as permanently full. That learned cap is no longer used; the loader now relies on the slot and item max values, and the compatibility cooldown already prevents re-trying genuinely full dispensers.
- Verbose log now prints the current count and max when a dispenser is reported full, to confirm the cap.

## 2026-07-07 (4)

### TRAutoloader 1.0.8

- Fixed drinks not transferring even when the loader had them. The autoloader was filling empty dispensers with whatever drink was already active elsewhere (for example, beer into an empty wine tap), so it would never put the intended drink back. It now only tops off dispensers that already hold a drink: leave a drink in a dispenser and the autoloader keeps it stocked from the loader; empty dispensers are left alone.
- Verbose drink logging is forced on for this build so the per-target trace is captured for confirmation.

## 2026-07-07 (3)

### TRAutoloader 1.0.7

- The F5 panel now lists the distinct drinks held by each assigned loader (food and drink), so you can see exactly which drinks the loader you assigned actually contains. This makes it obvious when a dispenser is not filling because the drink it holds is not in the assigned loader (for example, a cider dispenser when the loader holds only wine and beer).

## 2026-07-07 (2)

### Window title renames

- TRTracker window: "TAVERN TRACKER" -> "TR TAVERN TRACKER"
- TRBarrels window: "AGING STATS" -> "TR AGING TRACKER"
- TRBar window: "TR BAR" -> "TR BAR TRACKER"
- TRStats window: "TR STATS" -> "TR CHEATS"
- TRAutoloader window title unchanged.

### TRAutoloader 1.0.6

- Added an always-on (throttled) diagnostic for drink loading. When a target finds no source, the log now names the drink it wanted and lists every loader slot with its item type, so it is clear whether the wanted drink is present and whether it is being excluded (for example wine held as kegs rather than loose bottles). Runs without verbose mode.

## 2026-07-07

### TRAutoloader 1.0.5

- Verbose drink logging is now a config toggle (Debug / VerboseDrinkLog, default off) instead of a hardcoded flag, so the trace can be turned back on for diagnosis without a rebuild. Default off removes the logging lag.
- No behavior change to loading. Diagnosis showed the loader is working correctly: dispensers only fill when the loader holds the same drink they already contain, so they never mix drinks. A tap holding a drink that is not in the loader is skipped on purpose.

## 2026-07-06 (5)

### TRAutoloader 1.0.4

- Restored drink loading. The 1.0.3 change to skip the item clone broke it, because the game rejects a still-slotted item in its fit check. The clone is back.
- Added a food compatibility cache so food fit checks only clone once per item and target, then reuse the result. Lowered the per-tick drink cap (12 to 8) to spread moves across ticks.
- Verbose drink logging is on for this build so the per-target trace lands in the log. Expect a little extra lag from the logging.

### TRTracker 1.3.4

- Debug build for the still-missing tracker window. The plugin now also creates the manager on scene loads (not only in Awake), and logs the manager lifecycle, the plugin Awake completion, the scene hook, and the first Update. This should show exactly when and whether the manager gets created.

## 2026-07-06 (4)

### TRTracker 1.3.3

- Fixed the tracker window never appearing. Diagnostic logging showed the manager GameObject was destroyed during the bootstrap scene change before it ever started (OnDestroy fired, Start did not). The plugin now recreates the manager from its own Update loop once a real scene is active, which reliably survives.

### TRAutoloader 1.0.3

- Reduced the per-tick lag. Container.CanFitItems is a read-only check, so the loader no longer clones items just to test whether they fit. That clone was the main cost during source selection on every tick.
- Halved the per-tick drink cap (24 to 12) so item moves spread out over more ticks instead of spiking one frame.

## 2026-07-06 (3)

### TRAutoloader 1.0.2

- Fixed drinks not loading: kegs/service barrels (non-beer-tap dispensers) keep their drink in `slots[1]`, but the loader was filling `slots[0]` for every dispenser. It now targets `slots[1]` for kegs and `slots[0]` for beer taps (matching how the game itself fills them), and kegs are back on the direct-slot transfer path.

### TRTracker 1.3.2

- Diagnostic build to locate the missing tracker window: logs the manager Start/CreateUI result, OnDestroy, and first Update so we can see whether the panel is created and whether something destroys it. Also bumped the canvas sort order (100 -> 104) so it can't sit behind the other mod windows.

## 2026-07-06 (2)

### TRAutoloader 1.0.1

- Fixed the loader panel overlapping its own title bar, which made it look broken and blocked dragging. Content is now anchored below the header so the header is always grabbable.
- Added an "Auto-Loading Enabled" master toggle (top of the panel, also in config as `Autoloaders/Enabled`) so loading can be paused without clearing loader assignments; state is shown in the status line.
- Fixed kegs (non-beer-tap drink dispensers) not loading. They now use the same add-path as beer taps instead of the failing direct-slot path.
- Relaxed the food loader so it stocks the bar menu from any room (it was previously rejected unless placed in the dining room).

## 2026-07-06

### TRAutoloader 1.0.0

- Split the auto food/drink loader out of TRStats into its own standalone plugin (`com.lolaiur.trautoloader`, toggle **F5**).
- Optimized the loader tick to remove the per-tick reflection storms that dragged game performance:
  - Cached the `TavernZonesManager` singleton instead of re-resolving it via reflection on every dispenser and barrel zone check (previously the dominant per-tick cost).
  - Wired up the compatibility cache so drink-source compatibility probes skip the reflection item clone on the hot path.
  - Cached the obfuscated price field per item type instead of walking the type hierarchy for every candidate each tick.
- Added a compact loader panel (assign/clear food and drink loaders, interval, live status).

### TRStats 1.3.0

- Removed the autoloader (now shipped as TRAutoloader). The autoloader config keys (`FoodLoaderGuid`, `DrinkLoaderGuid`, `IntervalSeconds`) moved from `com.trstats.mod.cfg` to `com.lolaiur.trautoloader.cfg`. Reassign your loaders once after updating.

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
