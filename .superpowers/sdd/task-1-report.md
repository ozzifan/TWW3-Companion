# Task 1 Report: Make import destinations and previews explicit and resolvable

## Status

DONE

## Summary

Revised Application import contracts so Workspace targeting and optional Collection membership are separate via `ImportMembershipDestination`, added typed `ImportPreview.Candidates` and `ImportPreview.Operations`, and implemented non-persistent `IImportEngine.ResolveAsync` backed by a shared `BuildNormalizedPreviewAsync` builder.

## TDD Evidence

### RED

Command:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj --filter "FullyQualifiedName~ImportEngineTests|FullyQualifiedName~DependencyRulesTests" -v minimal
```

Output (compilation failure as expected):

```
C:\Users\steve\AppData\Local\AI Dev Orchestrator\work\ozzifan-TWW3-Companion\IMPL-TWW3-0008\tests\Tww3Companion.Application.Tests\Importing\ImportEngineTests.cs(10,18): error CS0246: The type or namespace name 'ImportMembershipDestination' could not be found (are you missing a using directive or an assembly reference?)
C:\Users\steve\AppData\Local\AI Dev Orchestrator\work\ozzifan-TWW3-Companion\IMPL-TWW3-0008\tests\Tww3Companion.Application.Tests\Importing\ImportEngineTests.cs(13,18): error CS0246: The type or namespace name 'ImportMembershipDestination' could not be found (are you missing a using directive or an assembly reference?)
```

### GREEN (focused)

Command:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj --filter "FullyQualifiedName~ImportEngineTests|FullyQualifiedName~DependencyRulesTests" -v minimal
```

Output:

```
Passed!  - Failed:     0, Passed:    21, Skipped:     0, Total:    21, Duration: 125 ms
```

### GREEN (full Application.Tests)

Command:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj -v minimal
```

Output:

```
Passed!  - Failed:     0, Passed:    56, Skipped:     0, Total:    56, Duration: 51 ms
```

## Commit

- `d6d5c78` — feat: support explicit import membership destinations

## Files Changed

### Created

- `src/Tww3Companion.Application/Importing/ImportMembershipDestination.cs` — sealed hierarchy: `LibraryOnly`, `ExistingCollection`, `NewCollection`
- `src/Tww3Companion.Application/Importing/ImportPreviewOperation.cs` — `ImportLibraryAction`, `ImportMembershipAction`, `ImportPreviewOperation`

### Modified

- `src/Tww3Companion.Application/Importing/ImportTargetContext.cs` — `MembershipDestination` replaces bare Collection string fields; old string overloads removed
- `src/Tww3Companion.Application/Importing/ImportCandidate.cs` — added `Unresolved` factory
- `src/Tww3Companion.Application/Importing/ImportPreview.cs` — typed `Candidates`, added `Operations` and `WarningCount`
- `src/Tww3Companion.Application/Importing/IImportEngine.cs` — added `ResolveAsync`
- `src/Tww3Companion.Application/Importing/ImportEngine.cs` — `BuildNormalizedPreviewAsync`, `ResolveAsync`, operation/warning builders
- `src/Tww3Companion.Application/Importing/NewWorkspaceImportSession.cs` — validates `LibraryOnly`, `NewCollection`; rejects `ExistingCollection`
- `src/Tww3Companion.Application/Importing/CurrentWorkspaceImportSession.cs` — added `ValidateDestination` for all three membership variants
- `tests/Tww3Companion.Application.Tests/Importing/ImportEngineTests.cs` — new contract tests, updated callers, `CommitCalls` on fake store

### Not changed (no edits required)

- `src/Tww3Companion.Application/Importing/ImportResolution.cs` — existing shape already sufficient
- `tests/Tww3Companion.Application.Tests/Architecture/DependencyRulesTests.cs` — already enforces no Infrastructure/Serilog in Application; passed unchanged

## Self-Review

1. **Old factory signatures removed** — `ForNewWorkspace` / `ForCurrentWorkspace` now require `ImportMembershipDestination`; Infrastructure/Desktop callers will fail compile until Task 2+ (intentional breaking change per plan).

2. **`ResolveAsync` is non-persistent** — replaces one candidate by ID, rebuilds via `BuildNormalizedPreviewAsync`, calls only `SavePreviewAsync` and `ReadCandidatesAsync`; never calls `CommitAtomicallyAsync` or `CommitNewWorkspaceAtomicallyAsync`. Verified by `ResolveAsync_replaces_one_candidate_without_persisting` (`CommitCalls == 0`).

3. **Destination validation in Application** — `NewWorkspaceImportSession.ValidateDestination` accepts `LibraryOnly` and non-blank `NewCollection`, rejects `ExistingCollection`. `CurrentWorkspaceImportSession.ValidateDestination` accepts all three variants and rejects blank Collection UUID or display name. Uses `ArgumentException` with `nameof(targetContext)`; no SQLite inspection.

4. **Typed preview** — `ImportPreview.Candidates` is `IReadOnlyList<ImportCandidate>`; `Operations` populated per candidate with library/membership actions and attached issues; `WarningCount` counts warning-code issues on non-skipped candidates only (currently 0 for existing conflict-only issues).

5. **Layer boundaries preserved** — Application depends only on Domain and Microsoft logging abstractions; `DependencyRulesTests` passes.

6. **Known follow-up** — Infrastructure (`SqliteWorkspaceCatalogStore`), Desktop (`ShellViewModel`), and their tests still reference superseded `ImportTargetContext` string overloads; addressed in later tasks per plan.

## Concerns

None blocking Task 1 scope. Operation/membership semantics (`Existing`, `Enrich`) will deepen when Task 2 adds persistence-backed membership lookups.

---

## Important Review Fixes (2026-07-25)

### Status

DONE

### Findings addressed

1. **ResolveAsync rejection tests** — Added `ResolveAsync_rejects_missing_candidate_id` and `ResolveAsync_rejects_duplicate_candidate_id`; both assert `ArgumentException` with `ParamName == "resolvedCandidate"`.
2. **CurrentWorkspace destination validation tests** — Added `CurrentWorkspace_import_accepts_all_membership_destination_variants` (LibraryOnly, ExistingCollection, NewCollection) and `CurrentWorkspace_import_rejects_blank_collection_uuid_and_display_name` (blank UUID and blank display name); rejections assert `ParamName == "targetContext"`.

### Test run

Command:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj --filter "FullyQualifiedName~ImportEngineTests" -v minimal
```

Output:

```
Passed!  - Failed:     0, Passed:    24, Skipped:     0, Total:    24, Duration: 59 ms
```

### Commit

- `2c526a0` — test: cover ResolveAsync and CurrentWorkspace destination validation

### Files changed

- `tests/Tww3Companion.Application.Tests/Importing/ImportEngineTests.cs` — 4 new tests (+111 lines)

### Concerns

None.
