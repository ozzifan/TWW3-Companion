# Task 2 Report: Desktop import-shell wiring

## Status

DONE

## Commit

- `2716d47 feat: wire import entry points to shared engine`

## Implementation

- Added `ImportIntoNewWorkspaceCommand` and `ImportIntoCurrentWorkspaceCommand` to `ShellViewModel`.
- Both commands call the existing `IShellImportService.BuildPreviewAsync` seam with the required `ImportTargetContext`.
- Added Home and Workspace buttons that bind directly to those commands; no import logic was added to either view.
- Added the required target-context and entry-point tests, and updated previous shell-composition assertions that explicitly prohibited import actions.

## TDD evidence

- RED: the focused test command failed because `RunImportIntoNewWorkspaceForTestAsync` and `RunImportIntoCurrentWorkspaceForTestAsync` did not exist.
- GREEN: after the minimal command, context, and binding implementation, the focused command passed all 4 tests.

## Verification

- `dotnet test tests/Tww3Companion.Desktop.Tests --filter "Home_exposes_import_into_new_workspace|Workspace_shell_exposes_import_into_current_workspace|Home_import_action_uses_new_workspace_target_context|Workspace_import_action_uses_current_workspace_target_context" -v normal`
  - Passed: 4/4.
- `dotnet test tests/Tww3Companion.Desktop.Tests -v normal`
  - Passed: 41/41.
- `git diff --check`
  - Passed with no whitespace errors.

## Scope note

The existing shell has no loaded workspace identity or import-candidate selection flow. Per the task brief, this slice only wires the shared service seam using the required target contexts; it does not add those later-slice responsibilities.
