# Security Fixes and Documentation Updates

## Changes Made

### 🔒 Security Fixes (CodeQL Issues Resolved)

Fixed potential file operation exceptions that CodeQL may have flagged:

**TRTrackerPlugin.cs (lines 27-28):**
- Added error handling for `File.Delete()` operations
- Check file existence before deletion
- Wrapped file operations in try-catch blocks

**TRBarrelsPlugin.cs (lines 29-30):**
- Same security improvements as TRTrackerPlugin

These fixes prevent the mod from crashing if log files are locked or inaccessible.

### 📚 Documentation Improvements

**README.md - New Installation Section:**
- Comprehensive BepInEx 5.x (64-bit) installation guide
- Step-by-step instructions with folder structure examples
- Warning about using x64 version (not x86)
- Direct links to BepInEx 5.4.23.2 x64 download
- Complete hotkey reference:
  - F8: Toggle TRTracker window
  - F9: Pause/unpause game time
  - F6: Toggle TRBar window
  - F7 or Ctrl+B: Toggle TRBarrels window

### 🔨 Build Updates

All three DLLs rebuilt with security fixes:
- `build/TRTracker.dll` (updated)
- `build/TRBarrels.dll` (updated)
- `build/TRBar.dll` (rebuilt, no code changes)

## To Push These Changes

Run this command:
```bash
git push
```

You may need to authenticate. If prompted, use your Personal Access Token.

## CodeQL Status

After pushing, CodeQL will re-scan the code. The fixed issues were:
- **Unhandled File.Delete()** - Now wrapped in try-catch
- **Missing existence check** - Now checks `File.Exists()` first

These were low-severity issues but good practice for robust code.

## Next Steps

1. **Push the changes:**
   ```bash
   git push
   ```

2. **Verify CodeQL passes:**
   - Go to: https://github.com/lolaiur/TRTracker/actions
   - Wait for CodeQL workflow to complete
   - Check for green checkmark

3. **Create v1.1.2 release (optional):**
   If you want to release these security fixes:
   - Update version in all 3 .cs files from "1.1.1" to "1.1.2"
   - Rebuild DLLs
   - Commit and push
   - Run release workflow with version `1.1.2`

   OR keep as v1.1.1 since these are minor internal improvements.

## Files Modified

- `Plugins/TRTrackerPlugin/TRTrackerPlugin.cs` - Security fix
- `Plugins/TRBarrelsPlugin/TRBarrelsPlugin.cs` - Security fix
- `README.md` - Installation guide
- `build/*.dll` - Rebuilt with fixes
- Helper scripts: `MANUAL_STEPS.md`, `push_and_release.sh`, `push_and_release.ps1`
