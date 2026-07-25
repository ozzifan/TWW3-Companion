# Task 5 Report: Implement the staged import-session ViewModels

## Status

DONE

## TDD Evidence

### RED (Step 1)

New test types did not compile until ViewModels and `ImportTaskModels` extensions were added.

### GREEN (Step 6)

```text
ImportWorkspaceViewModelTests + ImportPreviewViewModelTests + ImportResolutionViewModelTests: 15 passed
git diff --check: clean (committed files)
```

## Commits

| Subject | Scope |
|---------|-------|
| `463f77a` — `feat: add staged import task view models` | Six staged ViewModels, models, shared command, 15 unit tests |

## Files Changed

| File | Change |
|------|--------|
| `ImportTaskModels.cs` | Added `ImportTaskStage`, `ImportLaunchContext`, `ImportPreviewFingerprint`, `ImportPreviewFilter`, `ImportConfirmationSummary`, `ImportTaskCompletedEvent` |
| `ViewModelCommand.cs` | Shared `ICommand` helper for import ViewModels |
| `ImportSourceViewModel.cs` | Source kind, input, diagnostics, file picker, continue |
| `ImportDestinationViewModel.cs` | Home/current destination modes, one-time suggestions, `BuildTargetContext` |
| `ImportPreviewViewModel.cs` | Rows, enum filters, confirmation counts, blocking detection, SHA-256 digest helper |
| `ImportResolutionViewModel.cs` | Needs Attention queue: link/create/skip/scalar selection via coordinator |
| `ImportConfirmationViewModel.cs` | Immutable summary, Apply command, finalizing gate |
| `ImportWorkspaceViewModel.cs` | Session owner: stage navigation, fingerprint cache, discard confirmation, Apply lifecycle |
| `ImportWorkspaceViewModelTests.cs` | 7 tests (launch context, fingerprint reuse/rebuild, discard, Apply, failure retention) |
| `ImportPreviewViewModelTests.cs` | 4 tests (filters, summary counts, blocking gate) |
| `ImportResolutionViewModelTests.cs` | 4 tests (link/skip advance, owner collision, scalar choice) |

## Implementation Notes

- **Session ownership:** All import-session state lives in `ImportWorkspaceViewModel`; `ShellViewModel` was not modified in the commit.
- **Fingerprint:** SHA-256 digest of kind + document name + input text; unchanged fingerprint skips coordinator load/preview on destination continue.
- **Coordinator boundary:** ViewModels delegate load/preview/resolve/apply to `IImportTaskCoordinator` only; no SQL, files, or Steam HTTP in ViewModels.
- **Logging:** Microsoft.Extensions.Logging only; apply failures log stage code without imported content or paths.
- **Confirmation:** `ImportConfirmationSummary` exposes integer counts and `WarningsRemaining` only (no “warnings accepted”).
- **Scalar conflicts:** Competing values encoded in validation issue messages (`valueA|valueB`) until Application adds structured fields.

## Self-Review

- Import-session state did not move into `ShellViewModel` (verified staged diff excludes Shell files).
- Preview/resolution/Back/confirmation paths do not call Apply or mutate workspace data directly.
- `.superpowers/` and `.orchestrator-work-packet.json` excluded from commit.
- Unstaged `ShellViewModel` / `ShellViewModelTests` compile-fix leftovers left for Task 6.

## Concerns

1. ~~**Resolution retention on fingerprint change:**~~ Fixed — see Fix Evidence below.
2. **Scalar conflict data:** Competing values rely on pipe-delimited validation messages; structured Application support would be cleaner.
3. **Shell wiring deferred:** Task 6 must register coordinator/file service, wire shell entry, and land Shell compile fixes.

## Test Summary

```text
Passed: 16 (ImportWorkspaceViewModelTests: 8, ImportPreviewViewModelTests: 4, ImportResolutionViewModelTests: 4)
Failed: 0
```

## Fix Evidence (Important review findings)

### Resolution retention on fingerprint change

- Added `ImportPreviewResolutionRetention.MergeAsync` — on fingerprint change, rebuilds preview then re-applies prior resolutions when source candidate identity and available choices are unchanged (uses `cachedCandidates` + `cachedPreview`).
- `ImportPreviewViewModel.Loaded` keeps `cachedPreview` in sync after in-session resolutions.
- New test: `Changed_destination_retains_resolution_when_candidates_and_choices_unchanged`.

### Confirmation invalidation assertion

- `Changed_source_rebuilds_preview_and_invalidates_confirmation` now asserts `Confirmation.Summary is null` after rebuild.

### TDD (fix commit)

```text
ImportWorkspaceViewModelTests + ImportPreviewViewModelTests + ImportResolutionViewModelTests: 16 passed
```

Commit: `c7ba055` — `fix: retain import resolutions across fingerprint rebuilds`
