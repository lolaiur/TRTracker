# TRTracker GitHub Setup & Release Script (PowerShell)

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "TRTracker GitHub Setup & Release" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Push to GitHub
Write-Host "Step 1: Pushing to GitHub..." -ForegroundColor Yellow
Write-Host "You may be prompted for your GitHub username and Personal Access Token" -ForegroundColor Gray
Write-Host ""

try {
    git push -u origin main --force

    if ($LASTEXITCODE -ne 0) {
        throw "Git push failed"
    }

    Write-Host ""
    Write-Host "✅ Successfully pushed to GitHub!" -ForegroundColor Green
    Write-Host ""
}
catch {
    Write-Host ""
    Write-Host "❌ Push failed. Please check your credentials." -ForegroundColor Red
    Write-Host ""
    Write-Host "To create a Personal Access Token:" -ForegroundColor Yellow
    Write-Host "1. Go to https://github.com/settings/tokens"
    Write-Host "2. Click 'Generate new token (classic)'"
    Write-Host "3. Select scopes: repo (all), workflow"
    Write-Host "4. Copy the token and use it as your password"
    exit 1
}

# Step 2: Wait for workflows
Write-Host "Step 2: Waiting for initial workflows to start..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

Write-Host ""
Write-Host "You can check the workflow status at:" -ForegroundColor Cyan
Write-Host "https://github.com/lolaiur/TRTracker/actions" -ForegroundColor White
Write-Host ""
Read-Host "Press Enter once you've verified the workflows are running (or skip)"

# Step 3: Create release
Write-Host ""
Write-Host "Step 3: Creating v1.1.1 release..." -ForegroundColor Yellow
Write-Host ""

# Check if gh CLI is available
$ghExists = Get-Command gh -ErrorAction SilentlyContinue

if ($ghExists) {
    Write-Host "Using GitHub CLI to create release..." -ForegroundColor Cyan

    # Create the tag first
    git tag -a v1.1.1 -m "Release v1.1.1 - Initial public release"
    git push origin v1.1.1

    # Create release package
    Set-Location build
    Compress-Archive -Path *.dll -DestinationPath ../TRTracker-v1.1.1.zip -Force
    Set-Location ..

    $releaseNotes = @"
## Traveler's Rest Tracker Mods v1.1.1

This release contains three BepInEx plugins for Traveler's Rest:

- **TRTracker** (v1.1.1) - Gameplay tracking and statistics
- **TRBarrels** (v1.1.1) - Barrel management enhancements
- **TRBar** (v1.1.1) - Bar UI improvements

### Installation

1. Download ``TRTracker-v1.1.1.zip``
2. Extract all DLL files
3. Copy to ``Travellers Rest/BepInEx/plugins/``
4. Launch the game

---

**Note:** These DLLs are compiled from the source code visible in this repository.
"@

    gh release create v1.1.1 `
        TRTracker-v1.1.1.zip `
        --title "TRTracker v1.1.1" `
        --notes $releaseNotes

    Write-Host ""
    Write-Host "✅ Release created via GitHub CLI!" -ForegroundColor Green
    Write-Host ""
}
else {
    Write-Host "GitHub CLI (gh) not found." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Please create the release manually:" -ForegroundColor Cyan
    Write-Host "1. Go to https://github.com/lolaiur/TRTracker/actions/workflows/release.yml"
    Write-Host "2. Click 'Run workflow'"
    Write-Host "3. Enter version: 1.1.1"
    Write-Host "4. Click 'Run workflow' button"
    Write-Host ""
    Read-Host "Press Enter once you've triggered the release workflow"
}

Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "✅ Setup Complete!" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Visit: https://github.com/lolaiur/TRTracker"
Write-Host "2. Check that the release appears: https://github.com/lolaiur/TRTracker/releases"
Write-Host "3. Verify the badges show up on the README"
Write-Host ""
Write-Host "⚠️  Repository is still PRIVATE" -ForegroundColor Magenta
Write-Host ""
Write-Host "When you're ready to make it public:" -ForegroundColor Yellow
Write-Host "  Settings → Danger Zone → Change visibility → Make public"
Write-Host ""
