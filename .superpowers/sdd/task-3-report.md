# Task 3 Report: Current-Workspace Atomic Import

## Status

DONE

## Implementation

- Added `ImportEngine` for the supported `ImportTargetContext.CurrentWorkspace` path.
- Added `CurrentWorkspaceImportSession` to build current-workspace previews, reject skipped or unresolved candidates before apply, preserve no-op behaviour when confirmation is false, and delegate confirmed changes to one atomic store operation.
- Renamed the store commit port to `CommitAtomicallyAsync` to make the transaction boundary explicit.
- Replaced the temporary fake engine test seam with focused engine tests for required-resolution validation and atomic commit delegation.

## TDD Evidence

- The focused test command initially failed because the fake store did not implement the new atomic commit port and the concrete engine did not exist.
- After implementation, the focused command passed: 2/2 tests.

## Verification

- `dotnet test tests\\Tww3Companion.Application.Tests --filter "CurrentWorkspace_import_requires_all_required_resolutions|CurrentWorkspace_import_commits_all_changes_atomically" -v normal`
  - Passed: 2/2 tests.
- `dotnet test tests\\Tww3Companion.Application.Tests -v normal`
  - Passed: 39/39 tests.
- `git diff --check`
  - Passed with no output.

## Scope and Concerns

- The implementation intentionally supports only the current-workspace target; new-workspace and UI wiring remain out of scope.
- The application layer exposes the atomic boundary through `IWorkspaceImportStore`; the concrete persistence transaction implementation is deferred to the store adapter work.

## Review Fixes

- The shared engine now previews and applies both `CurrentWorkspace` and `NewWorkspace` target contexts; Task 3 remains scoped to the current-workspace session and atomic store boundary.
- Candidate inputs are normalized from `ImportCandidate`, Steam, and Markdown candidate entries before exact source-reference matching against the store's existing candidates.
- Apply passes the exact confirmed `ImportPreview` into `CommitAtomicallyAsync`, preserving editable preview state rather than reconstructing it from candidates.

## Review Fix Verification

- Added regression coverage for new-workspace support, confirmed-preview identity, and Steam normalization/exact matching.
- `dotnet test tests\\Tww3Companion.Application.Tests --filter "CurrentWorkspace_import_requires_all_required_resolutions|CurrentWorkspace_import_commits_all_changes_atomically" -v normal`
  - Passed: 2/2 tests.
- `dotnet test tests\\Tww3Companion.Application.Tests\\Tww3Companion.Application.Tests.csproj -v minimal`
  - Passed: 42/42 tests.
