# Task 2 Report: Shared Import Pipeline Contracts and Persistence Port

## Status

DONE

## Delivered

- Added `ImportCandidate` with linked, create-with-display-name, and skipped factory methods.
- Added `ImportResolution` with required-link and optional-skip factory methods.
- Added `IWorkspaceImportStore` for reading candidates, atomically saving previews with resolutions, and committing confirmed previews.
- Kept `IImportEngine` source-neutral while documenting that `ImportCandidate` is the shared pipeline model.
- Added contract coverage for the typed models and an engine fake constructed through the persistence port.

## TDD Evidence

- RED: the required focused test command failed before implementation because `IWorkspaceImportStore`, `ImportCandidate`, and `ImportResolution` did not exist (`CS0246`).
- GREEN: the same focused command passed after implementation: 3 passed, 0 failed.

## Verification

- `dotnet test tests/Tww3Companion.Application.Tests --filter "ImportCandidate_can_represent_link_create_and_skip|ImportResolution_can_represent_required_and_optional_resolutions|ImportEngine_builds_preview_through_a_store_port" -v normal`
  - Passed: 3; Failed: 0.
- `dotnet test tests/Tww3Companion.Application.Tests -v normal`
  - Passed: 41; Failed: 0.
- `git diff --check`
  - Passed.

## Commit

- `ec9abc2 feat: add shared import pipeline contracts`

## Concerns

- None. The existing `IImportEngine` candidate boundary already matched the brief's source-neutral signature; this task adds the typed shared model and persistence seam without changing UI or engine behavior.

## Review Fix

- Removed the premature `ImportEngine` and `CurrentWorkspaceImportSession` implementation, along with its current-workspace behavior tests.
- Retained the Task 2 contract types, `IWorkspaceImportStore`, source-neutral `IImportEngine`, and the three specified contract tests.
- Re-ran the specified filtered test command: 3 passed, 0 failed.
- Re-ran `dotnet test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj -v minimal`: 37 passed, 0 failed.
## Review Fix 2
- Updated `ImportEngine_builds_preview_through_a_store_port` so the fake engine actually calls `IWorkspaceImportStore.ReadCandidatesAsync(...)` and the test asserts that the store was touched.
- Re-ran the specified filtered test command: 3 passed, 0 failed.
- Re-ran `dotnet test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj -v minimal`: 37 passed, 0 failed.
