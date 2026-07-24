# Task 2 Report: Collection library overlay

## Status

Completed and verified.

## Delivered

- Added `WorkspaceLibrarySnapshot`, `WorkspaceLibraryMod`, `WorkspaceCollection`, and `WorkspaceCollectionMembership` as the overlay contract.
- Added `IWorkspaceQuery` returning `Task<WorkspaceLibrarySnapshot>` and `WorkspaceLibraryQuery` as the read-only adapter over the active workspace path.
- Wired `WorkspaceLibraryQuery` through `ApplicationComposition` into `ShellViewModel`, which owns `ModLibrary` and `CollectionDetail`.
- `ModLibraryViewModel.LoadAsync` maps one snapshot into existing view models (including membership markers via collection names).
- Query failures fail closed: workspace error surface is shown and both panels are cleared.
- `MainWindow` binds library/collection panels to the shell-owned view models.

## Test-first evidence

Prescribed filtered tests failed before implementation with `CS0246` for missing overlay types and query/view-model members.

After implementation:

```powershell
dotnet test tests/Tww3Companion.Application.Tests --filter "WorkspaceQueryTests|WorkspaceLibrarySnapshotTests"
dotnet test tests/Tww3Companion.Desktop.Tests --filter "ModLibraryViewModelTests|HomeCompositionTests|MainWindowLayoutTests"
```

Result: all filtered tests passed.

## Final verification

```powershell
dotnet test tests/Tww3Companion.Application.Tests
dotnet test tests/Tww3Companion.Desktop.Tests
git diff --check
```

Result:

- Application tests: 34 passed, 0 failed
- Desktop tests: 42 passed, 0 failed
- `git diff --check`: clean

## Deliberate limitations

- The overlay query is read-only and does not mutate the workspace file.
- Schema v1 has no mod/collection tables yet, so a successfully opened workspace currently returns an empty snapshot. Catalog rows will arrive in a later persistence slice without changing the desktop mapping contract.
- The UI does not assemble membership data from separate queries; it consumes one `WorkspaceLibrarySnapshot` and maps it into view models.

## Commits

- `2fd16d5 test: define collection library overlay`
- `97109a6 feat: add collection library overlay`
