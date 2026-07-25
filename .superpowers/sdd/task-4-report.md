# Task 4 Report: Build the stateless Desktop import coordinator and file service

## Status

DONE

## TDD Evidence

### RED (Step 1)

New test types did not compile until models, interfaces, and services were added.

### GREEN (Step 5)

```text
ImportSourceFileServiceTests + ImportTaskCoordinatorTests: 17 passed
git diff --check: clean (committed files)
```

## Commits

| Subject | Scope |
|---------|-------|
| `feat: coordinate import sources and previews` | Coordinator, file service, models, tests, `InternalsVisibleTo` |

## Files Changed

| File | Change |
|------|--------|
| `src/Tww3Companion.Desktop/ViewModels/ImportTaskModels.cs` | Created presentation contracts (`ImportSourceKind`, `ImportSourceRequest`, diagnostics, load result) |
| `src/Tww3Companion.Desktop/Services/IImportSourceFileService.cs` | Created file-picker abstraction |
| `src/Tww3Companion.Desktop/Services/ImportSourceFileService.cs` | Avalonia picker, 4 MiB bounded read, `ImportTextDecoder`, filename-only return |
| `src/Tww3Companion.Desktop/Services/IImportTaskCoordinator.cs` | Created coordinator façade interface |
| `src/Tww3Companion.Desktop/Services/ImportTaskCoordinator.cs` | Stateless load/preview/resolve/apply over adapters, metadata, engine |
| `src/Tww3Companion.Desktop/Tww3Companion.Desktop.csproj` | Added `InternalsVisibleTo` for test file-picker seam |
| `tests/.../ImportSourceFileServiceTests.cs` | Created 7 tests (picker filters, bounded read, decode, cancellation) |
| `tests/.../ImportTaskCoordinatorTests.cs` | Created 10 tests (disclosure, metadata gating, enrichment, Apply delegation) |

## Implementation Notes

- **Stateless coordinator:** Only readonly `IImportEngine` and `ISteamMetadataClient` dependencies; no session fields.
- **Metadata gating:** Markdown/Steam collection/items disclose Workshop IDs locally; Steam adapters called only when `RequestMetadata` is true.
- **Failed lookups:** Produce `ImportCandidate.Unresolved` plus non-blocking `import.source.steam.lookup.failed` diagnostic; candidates are not dropped.
- **Apply:** Always delegates `engine.ApplyAsync(preview, confirm: true, ...)`.
- **File service:** Returns `Path.GetFileName` only; no logging of paths or source text. Internal constructor seam avoids mocking non-implementable Avalonia storage types in tests.
- **Workshop ID validation:** Duplicated in coordinator (Application `SteamImportAdapter` is internal).

## Self-Review

- Coordinator statelessness confirmed: no source text, destination, preview, or resolution fields.
- No logging of imported prose, clipboard, display names, notes, or full paths in new services.
- Preview/Resolve/Apply delegate to `IImportEngine` without persistence in Desktop layer.
- Scope limited to Task 4 files per brief; `.superpowers/` and `.orchestrator-work-packet.json` excluded from commit.

## Concerns

1. **ShellViewModel compile debt (uncommitted):** Branch still has stale `ImportTargetContext` call sites in `ShellViewModel.cs` and `ShellViewModelTests.cs` from Task 1 API migration; fixed locally to run tests but left out of this commit per brief file list. Task 6 composition wiring should include or precede those fixes.
2. **Composition wiring deferred:** `ApplicationComposition` does not yet register coordinator/file service (Task 6 per plan).

## Test Summary

```text
Passed: 17 (ImportSourceFileServiceTests: 7, ImportTaskCoordinatorTests: 10)
Failed: 0
```
