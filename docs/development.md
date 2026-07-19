# Development Guide

This guide collects the commands you need to install, build, test, publish, and smoke-test TWW3 Companion on Windows.

## SDK

Install the pinned SDK from `global.json`:

- .NET SDK `10.0.302`

If `dotnet` resolves to a different SDK, use the bundled user profile copy:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' --info
```

## Restore

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' restore TWW3Companion.sln
```

## Format

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' format TWW3Companion.sln
```

To verify formatting without changing files:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' format TWW3Companion.sln --verify-no-changes
```

## Build

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' build TWW3Companion.sln -c Release
```

## Test

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test TWW3Companion.sln
```

## Run

Run the desktop app from source:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' run --project src/Tww3Companion.Desktop/Tww3Companion.Desktop.csproj
```

## Publish

Create the self-contained Windows x64 portable artifact:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' publish src/Tww3Companion.Desktop/Tww3Companion.Desktop.csproj `
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
