# Development Guide

This guide collects the commands you need to install, build, test, publish, and smoke-test TWW3 Companion on Windows.

## SDK

Install the pinned SDK from `global.json`:

- .NET SDK `10.0.302`

If `dotnet` resolves to a different SDK, point it at your pinned install or adjust the command to match your environment:

```powershell
dotnet --info
```

## Restore

```powershell
dotnet restore TWW3Companion.sln
```

## Format

```powershell
dotnet format TWW3Companion.sln
```

To verify formatting without changing files:

```powershell
dotnet format TWW3Companion.sln --verify-no-changes
```

## Build

```powershell
dotnet build TWW3Companion.sln -c Release
```

## Test

```powershell
dotnet test TWW3Companion.sln
```

## Run

Run the desktop app from source:

```powershell
dotnet run --project src/Tww3Companion.Desktop/Tww3Companion.Desktop.csproj
```

## Publish

Create the self-contained Windows x64 portable artifact:

```powershell
dotnet publish src/Tww3Companion.Desktop/Tww3Companion.Desktop.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o artifacts/portable/win-x64
```

## Smoke Test

Run the repository smoke script after publishing:

```powershell
& scripts/smoke-test-portable.ps1 -PublishDirectory artifacts/portable/win-x64 -WorkingDirectory artifacts/smoke-installed -Mode Installed
& scripts/smoke-test-portable.ps1 -PublishDirectory artifacts/portable/win-x64 -WorkingDirectory artifacts/smoke-portable -Mode Portable
```

The smoke runs create operation-owned working directories and leave them behind for inspection.

## Paths

- Installed mode uses `%LOCALAPPDATA%\TWW3 Companion`
- Portable mode uses `Data\` beside the executable when `portable.flag` exists
- Workspace files use the `.tww3c` extension

## Safe Manual Copy

Copy a completed `.tww3c` file only after closing TWW3 Companion on every machine that might have the same file open. Never open the same live Workspace from multiple synced machines at once.

## Known Limitation

There is one instance per Windows user. Installed and portable copies for the same Windows user share the single-instance guard.

## Manual Import Workspace Verification

Use a disposable `.tww3c` Workspace outside the repository. Do not commit test Workspaces. After each run, inspect the application log and confirm no import source text, clipboard content, display names, or full local paths appear.

### Source and destination matrix

| Check | Steps | Expected |
|-------|-------|----------|
| Markdown paste | Home → Import into a new Workspace → Markdown → paste a short mod list → Continue | Source loads; disclosed Workshop IDs shown if present |
| Markdown file | Choose file with a Markdown mod list → Continue | Document name shown; content loaded |
| Steam Collection | Select Steam Collection → paste one public collection ID → review disclosure → Continue | Member Workshop IDs disclosed before metadata request |
| Steam items paste | Select Steam items → paste multiple IDs or URLs → Continue | Each ID disclosed; partial metadata failure shows diagnostics, not a blocking failure for valid IDs |
| Steam items file | Choose file with Workshop IDs → Continue | Same as paste |
| Library-only new Workspace | Destination → Library only → name/path → preview → Apply | Mods in library; no Collection created |
| Library-only current Workspace | Open Workspace → Import → Library only → Apply | Mods added to library only |
| Existing Collection | Import → select existing Collection → Apply | New Memberships appended; existing Memberships unchanged |
| New Collection | Import → New Collection → name → Apply | Collection created with new Memberships |
| Metadata partial failure | Steam source where one item metadata fails | Diagnostic shown; item importable with manual display name |
| Back unchanged | Reach Preview → Back to Destination without changing destination → Continue | Preview rebuilds; prior resolutions cleared only if source/destination changed |
| Back changed destination | Change destination after preview → Continue | Preview and confirmation reset for new target |
| Blocking resolution | Preview with unmatched identity → resolve or Skip each blocking item | Apply disabled until resolved; Skip removes candidate from commit |
| Failed Apply | Force persistence failure if possible, or use invalid path for new Workspace | Preview and resolutions retained; banner states no commit |
| Successful reload | Complete Apply on current Workspace | Library reloads; shell shows imported Mods/Memberships |

### Layout and accessibility

Run on Windows 10 or later x64 with the window at **1024 × 640** logical pixels:

- Primary actions remain reachable without horizontal scrolling.
- Preview and Needs Attention panels remain usable (stacked layout).

Repeat representative checks with Windows text scaling at **125%** and **150%**.

Enable **Windows High Contrast** and confirm controls remain readable and focus-visible.

Complete the import workflow using keyboard only (Tab, Shift+Tab, arrows, Enter, Escape).

With **Windows Narrator** enabled, confirm stage changes, blocking counts, and Apply result are announced without stealing focus from the active control.

Record Windows version, display scale, and any skipped accessibility check in the Task 7 report.
