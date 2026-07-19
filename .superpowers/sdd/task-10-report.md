# Task 10 Report: Workspace foundation slice packaging and developer workflow

## Status

Verified. Commit `9a22617` completes the workspace foundation slice deliverables and the supporting developer workflow.

## Verification evidence

Full solution formatting check:

```powershell
dotnet format TWW3Companion.sln --verify-no-changes
```

Exit code `0`; no format changes pending.

Full solution build:

```powershell
dotnet build TWW3Companion.sln -c Release
```

Exit code `0`; build succeeded.

Full solution test run:

```powershell
dotnet test TWW3Companion.sln -c Release --no-build
```

Exit code `0`; `Failed: 0, Passed: 107, Skipped: 0, Total: 107`.

Installed smoke verification:

```powershell
& 'scripts/smoke-test-portable.ps1' -PublishDirectory 'artifacts/portable/win-x64' -WorkingDirectory 'artifacts/smoke-installed-5' -Mode Installed
```

Exit code `0`.

Portable smoke verification:

```powershell
& 'scripts/smoke-test-portable.ps1' -PublishDirectory 'artifacts/portable/win-x64' -WorkingDirectory 'artifacts/smoke-portable-5' -Mode Portable
```

Exit code `0`.

Whitespace validation:

```powershell
git diff --check
```

Exit code `0`; no whitespace errors.

## Deliverables

- Added `.github/workflows/ci.yml` for restore, format, build, test, publish, and smoke verification on Windows.
- Added `docs/development.md` with the supported local workflow.
- Added `scripts/smoke-test-portable.ps1` for installed and portable smoke verification.
- Kept the portable/installed mode detection aligned with the test harness.
- Added a desktop composition test covering portable marker detection in test mode.

## Notes

- The smoke script exercises the compiled executable and therefore validates the published artifact path, not just in-process command handling.
- The repo still contains user-owned scratch directories such as `.agents/` and `.claude/`; they were intentionally left untracked.
