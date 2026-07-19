# Home Composition Design

## Purpose

Task 9 connects the approved workspace foundation to the app's Home flow without replacing the fixed shell from Task 8. The shell remains the top-level window, and one shared `ShellViewModel` owns the visible state for Home, Compatibility, and Workspace. The window swaps nested views inside that shell instead of opening a separate Home window.

This slice deliberately excludes Import and all later workspace features.

## Scope

Included:

- a shared shell view model that owns screen state, theme state, compatibility state, and Home commands
- a nested `HomeView` hosted by the existing shell window
- create/open workspace commands
- recent workspace display and removal
- settings save failure recovery state
- startup composition in the approved order
- test hooks for smoke testing and single-instance hold behavior

Excluded:

- Import
- separate Home window or separate window navigation model
- direct SQLite access from test hooks
- any change to the fixed workspace shell layout from Task 8

## Architecture

`MainWindow` stays the shell host. Its job is to display one of three nested surfaces based on `ShellViewModel.CurrentScreen`:

- Home
- Compatibility
- Workspace

`ShellViewModel` is the single owner of this screen state. It exposes the commands and properties needed by the Home view and the workspace shell, and it remains the state source for theme and high-contrast precedence.

`HomeView` is a nested child view. It binds to the shared shell model and only renders the Home surface:

- create workspace
- open workspace
- recents
- settings failure recovery
- the synchronized-folder warning and safe copy guidance

The workspace shell remains the same three-column layout approved in Task 8.

## Home Behavior

Home is intentionally small and command-driven.

Create and open actions are busy-aware. While one is in progress, the UI prevents duplicate invocations. Once the operation enters finalizing, cancellation is disabled and the UI shows `Finalizing — please wait`.

Successful create/open transitions to the workspace shell. Failures stay on Home and preserve the current screen.

Recents remain visible even when some entries are missing. Removing a recent only affects that entry.

If settings save fails, the in-memory value remains active for the current session and the UI exposes recovery actions:

- Retry
- Open Settings Folder

## Startup Composition

Startup uses one fixed order:

1. detect installed vs portable mode
2. initialize and probe managed paths
3. acquire the single-instance lease
4. configure logging
5. load settings
6. construct Infrastructure adapters
7. construct Application use cases
8. construct the shared shell view model and nested views
9. show Compatibility or Home

Failure handling:

- managed-path failure shows a blocking native error and exits
- single-instance failure shows `TWW3 Companion is already running for this Windows user. Close the existing installed or portable copy and try again.` and exits before settings load
- settings failure does not prevent startup; the app keeps the in-memory state and surfaces recovery inside Home

## Test Hooks

The slice includes two test hooks, both gated by `TWW3_COMPANION_TEST_MODE=1`.

`--smoke-test <directory>` uses the normal composition root to create `Smoke Workspace`, close it, reopen it, and write `smoke-result.json` with:

- `workspaceId`
- `displayName`
- `applicationMode`
- `managedRoot`

The hook succeeds only when the reopened UUID and display name match.

`--hold-single-instance <milliseconds>` acquires the normal single-instance lease, writes `lease-acquired.signal` under `TWW3_COMPANION_TEST_MANAGED_ROOT`, holds for the requested duration, and exits.

Neither hook may bypass lifecycle use cases or call SQLite directly.

## Verification

This slice is complete when:

- Home and composition tests pass
- the full desktop test project passes
- the smoke hooks are covered by the implementation plan and use the same composition root as real startup
- the shell from Task 8 remains unchanged in layout and accessibility behavior

## Notes

The nested-view approach keeps the shell stable while still letting Home and Compatibility participate in one shared state model. That reduces startup complexity and keeps the transition from Home to Workspace easy to test.
