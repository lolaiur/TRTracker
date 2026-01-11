#!/bin/bash
set -e

echo "======================================"
echo "TRTracker GitHub Setup & Release"
echo "======================================"
echo ""

# Step 1: Push to GitHub
echo "Step 1: Pushing to GitHub..."
echo "You may be prompted for your GitHub username and Personal Access Token"
echo ""
git push -u origin main --force

if [ $? -ne 0 ]; then
    echo ""
    echo "❌ Push failed. Please check your credentials."
    echo ""
    echo "To create a Personal Access Token:"
    echo "1. Go to https://github.com/settings/tokens"
    echo "2. Click 'Generate new token (classic)'"
    echo "3. Select scopes: repo (all), workflow"
    echo "4. Copy the token and use it as your password"
    exit 1
fi

echo ""
echo "✅ Successfully pushed to GitHub!"
echo ""

# Step 2: Wait for workflows
echo "Step 2: Waiting for initial workflows to start..."
sleep 5

echo ""
echo "You can check the workflow status at:"
echo "https://github.com/lolaiur/TRTracker/actions"
echo ""
read -p "Press Enter once you've verified the workflows are running (or skip)..."

# Step 3: Create release
echo ""
echo "Step 3: Creating v1.1.1 release..."
echo ""

# Check if gh CLI is available
if command -v gh &> /dev/null; then
    echo "Using GitHub CLI to create release..."

    # Create the tag first
    git tag -a v1.1.1 -m "Release v1.1.1 - Initial public release"
    git push origin v1.1.1

    # Create release
    cd build
    zip -r ../TRTracker-v1.1.1.zip *.dll
    cd ..

    gh release create v1.1.1 \
        TRTracker-v1.1.1.zip \
        --title "TRTracker v1.1.1" \
        --notes "## Traveler's Rest Tracker Mods v1.1.1

This release contains three BepInEx plugins for Traveler's Rest:

- **TRTracker** (v1.1.1) - Gameplay tracking and statistics
- **TRBarrels** (v1.1.1) - Barrel management enhancements
- **TRBar** (v1.1.1) - Bar UI improvements

### Installation

1. Download \`TRTracker-v1.1.1.zip\`
2. Extract all DLL files
3. Copy to \`Travellers Rest/BepInEx/plugins/\`
4. Launch the game

---

**Note:** These DLLs are compiled from the source code visible in this repository."

    echo ""
    echo "✅ Release created via GitHub CLI!"
    echo ""
else
    echo "GitHub CLI (gh) not found."
    echo ""
    echo "Please create the release manually:"
    echo "1. Go to https://github.com/lolaiur/TRTracker/actions/workflows/release.yml"
    echo "2. Click 'Run workflow'"
    echo "3. Enter version: 1.1.1"
    echo "4. Click 'Run workflow' button"
    echo ""
    read -p "Press Enter once you've triggered the release workflow..."
fi

echo ""
echo "======================================"
echo "✅ Setup Complete!"
echo "======================================"
echo ""
echo "Next steps:"
echo "1. Visit: https://github.com/lolaiur/TRTracker"
echo "2. Check that the release appears: https://github.com/lolaiur/TRTracker/releases"
echo "3. Verify the badges show up on the README"
echo ""
echo "⚠️  Repository is still PRIVATE"
echo ""
echo "When you're ready to make it public:"
echo "  Settings → Danger Zone → Change visibility → Make public"
echo ""
