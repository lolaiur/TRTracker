# Obfuscation Audit - 2026-04-10

Checked against the installed game assembly:

- `C:\Games\Steam\steamapps\common\Travellers Rest\Windows\TravellersRest_Data\Managed\Assembly-CSharp.dll`

Reference dumps consulted during the investigation:

- `C:\j0sh\projects\tr-dis`
- `C:\j0sh\projects\tr-dis-2`

## Outcome

- The decompile dump at `tr-dis-2` does not fully match the currently installed game binaries.
- A full rebuild on April 10, 2026 initially failed on renamed members in `TRTracker`, `TRBar`, and `TRStats`.
- After updating the affected references, all four plugins compiled successfully against the installed game assembly:
  - `TRTracker.dll`
  - `TRBarrels.dll`
  - `TRBar.dll`
  - `TRStats.dll`

## Confirmed Current Renames

- `TavernManager.OFDGCPAEGOM` -> `TavernManager.GOKBJFAMHMJ`
- `TavernReputation.OFDGCPAEGOM` -> `TavernReputation.GOKBJFAMHMJ`
- `TavernServiceManager.OFDGCPAEGOM` -> `TavernServiceManager.GOKBJFAMHMJ`
- `TavernZonesManager.OFDGCPAEGOM` -> `TavernZonesManager.GOKBJFAMHMJ`
- `CommonReferences.OFDGCPAEGOM` -> `CommonReferences.GOKBJFAMHMJ`
- `CropSetter.DOAIGHJOJNA` -> `CropSetter.PILMFLODILL`
- `Crafter.PCHEDHIGFCB` -> `Crafter.AHCDANNFGPG`

## Source Adjustments Made

- `TRTracker` now resolves singleton-style managers through reflection instead of hard-coding the singleton property name.
- `TRBar` uses `TavernManager.GOKBJFAMHMJ` and reads the private `_open` field via reflection for tavern-open state.
- `TRStats` uses `CropSetter.PILMFLODILL`, `Crafter.AHCDANNFGPG`, and `CommonReferences.GOKBJFAMHMJ`.
- `TRStats` no longer depends on the removed `Item.JEEJEGMDMKG(...)` helper and compares `Item.nameId` instead for the bucket swap logic.

## Current Risk Areas

- `TRTracker` still depends on private and obfuscated members for reputation, heat, dirt, and time state.
- `TRBar` still depends on the private `_open` field because the public tavern-open accessor changed again.
- `TRStats` still patches obfuscated method names on `Well` and `Crafter`:
  - `Well.FAGAHNPPIPA(...)`
  - `Crafter.AHGOCDAALHF(...)`

## Recommended Validation Flow For Future Updates

1. Audit the live `Assembly-CSharp.dll`, not just a decompile dump.
2. Rebuild the plugins against the installed game assemblies.
3. Re-check Harmony patch targets whose names are still obfuscated.
4. Smoke test the plugins in-game after copying the rebuilt DLLs into `BepInEx\plugins`.
