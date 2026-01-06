# Traveler's Rest Tracker Mod

A gameplay tracking mod for Traveler's Rest.

## Workflows & CI/CD

This repository uses automated workflows for building, testing, and releasing:

### 🔨 Build Workflows
- **Build** (main branch): Runs on every push to main
- **Build PR**: Validates and builds PRs from `feat/*` or `fix/*` branches only
- **CodeQL**: Security scanning for C# code (weekly + on PRs)

### 🚀 Smart Release Workflow
The release workflow automatically determines version bumps based on commit message tags:

| Commit Tag | Version Bump | Example |
|------------|-------------|---------||
| `#major` | Major (breaking changes) | v1.0.0 → v2.0.0 |
| `#minor` | Minor (new features) | v1.0.0 → v1.1.0 |
| `#fix` or `#patch` | Patch (bug fixes) | v1.0.0 → v1.0.1 |

**Example commits:**
```bash
git commit -m "Add new tracking feature #minor"
git commit -m "Fix crash on startup #fix"
git commit -m "Refactor API - breaking changes #major"
```

**To create a release:**
1. Commit your changes with the appropriate tag (#major, #minor, or #fix)
2. Push to main (or merge PR)
3. Go to Actions → Smart Release → Run workflow
4. The workflow will automatically:
   - Parse all commits since the last release
   - Determine the highest priority version bump
   - Build, package, and create a GitHub release

You can also manually trigger a release with a specific version bump type in the workflow dispatch UI.

### 🔒 Repository Setup

#### Branch Protection (Required)
1. Go to Settings → Branches → Add rule
2. Branch name pattern: `main`
3. Enable:
   - ✅ Require a pull request before merging
   - ✅ Require status checks to pass (select "Build PR" and "CodeQL")
   - ✅ Require branches to be up to date
4. Under "Restrict who can push to matching branches", only allow `feat/*` and `fix/*` patterns

#### GitHub Copilot Code Review (Optional)
1. Go to https://github.com/apps/copilot-pull-request-reviewer
2. Click "Install" or "Configure"
3. Select this repository
4. Copilot will automatically review all PRs

## Building Locally

```bash
dotnet restore
dotnet build --configuration Release
```

## Installation

1. Download the latest release from the [Releases](https://github.com/lolaiur/TRTracker/releases) page
2. Extract the ZIP file
3. Copy `TRTracker.dll` to your Traveler's Rest mods folder

## Development Workflow

1. Create a branch: `git checkout -b feat/my-feature` or `git checkout -b fix/bug-name`
2. Make changes and commit with tags: `git commit -m "Description #minor"`
3. Push and create PR: `git push origin feat/my-feature`
4. PR will auto-build and run CodeQL
5. After merge, run Smart Release workflow to create a new version

## Versioning

This project uses [Semantic Versioning](https://semver.org/):
- **Major** (v1.0.0 → v2.0.0): Breaking changes
- **Minor** (v1.0.0 → v1.1.0): New features (backwards compatible)
- **Patch** (v1.0.0 → v1.0.1): Bug fixes