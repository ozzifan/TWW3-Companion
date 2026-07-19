# Task 9 Report: Home composition slice verification

## Status

Verified. Branch `codex/workspace-foundation` at `9f8199b` is ready for the next packaging/documentation stage.

## Verification evidence

Full desktop test project:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj
```

Exit code `0`; `Failed: 0, Passed: 32, Skipped: 0, Total: 32`.

Focused Home/composition/shell filter:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj --filter "HomeCompositionTests|ApplicationCompositionTests|ShellViewModelTests"
```

Exit code `0`; `Failed: 0, Passed: 19, Skipped: 0, Total: 19`.

Whitespace validation:

```powershell
git diff --check
```

Exit code `0`; no whitespace errors.

## In-process hook coverage

Smoke and composition hooks are exercised from the approved xUnit harness:

| Test | Hook exercised |
|------|----------------|
| `ManagedPathFailureShowsNativeBlockingDialogAndDoesNotWriteLogEntry` | Pre-logging managed-path failure via `RunStartupForTest`; native dialog only, no `*.log` files |
| `SingleInstanceFailureShowsAlreadyRunningMessage` | Pre-logging single-instance failure with exact already-running copy |
| `SmokeTestWritesResultJsonAtDirectoryRoot` | `--smoke-test` in-process via `SmokeTestCommand.Run`; writes `<directory>\smoke-result.json` with required fields |
| `SmokeTestUsesApplicationCompositionRootInsteadOfManualStartupWiring` | Source guard that smoke uses `ApplicationComposition.CreateSmokeTestRuntime` |
| `HoldSingleInstanceFailsCleanlyWhenManagedRootEnvironmentVariableIsMissing` | `--hold-single-instance` argument guard |
| `ReturnHomeDisposalPreventsScreenChangeUntilDisposalSucceeds` | Return-home disposal boundary via `IWorkspaceDisposalCoordinator` |

## Self-review checklist

- Home is a nested view inside `MainWindow`, not a separate window.
- One shared `ShellViewModel` owns Home, Compatibility, Workspace, theme, and recovery state.
- Return-home disposal is explicit: `CurrentScreen` stays `Workspace` until `DisposeWorkspaceScopeAsync` completes.
- `smoke-result.json` is written only at `<directory>\smoke-result.json`.
- Pre-logging failures (managed paths, single-instance) use `NativeStartupDialog` only; logging is configured after both gates pass.
- Startup order matches the nine approved steps in `ApplicationComposition.StartupSteps`.
- No placeholder implementation steps remain in this slice.

## Tradeoffs

- `WorkspaceDisposalCoordinator` is a production no-op stub; real workspace-scope teardown will land with the workspace runtime slice. The disposal boundary contract is typed and tested now so return-home behavior does not regress.
- `CompositionTestOptions` / `RunStartupForTest` remain test-only seams rather than public API.
- Display-name and open-file prompts are Avalonia dialogs behind `IWorkspaceDialogService`; headless CI cannot exercise the visual prompt, only source and command-path guards.

## Limitations

**Pre-logging failure path:** Only managed-path initialization and single-instance acquisition failures occur before `LoggingConfiguration.CreateProvider`. Both call `ShowBlockingError` and return exit code `1` without creating log files. Failures after logging starts follow the normal logging path and are not covered by the native-dialog-only constraint.

**Return-home disposal boundary:** On disposal failure, `CurrentScreen` remains `Workspace` and the exception message is surfaced on `Workspace.OperationError` (bound near Return Home). There is no retry-disposal command; the user must attempt Return Home again. Concurrent Return Home calls are ignored while `isDisposingWorkspace` is true. Production disposal is currently immediate (`Task.CompletedTask`), so the async wait is a contract placeholder until workspace resources exist.

**Return-home fire-and-forget:** `ReturnHome()` discards `ReturnHomeAsync()` (`_ = ReturnHomeAsync()`). Cancellation and other exceptions are caught inside `ReturnHomeAsync` and surfaced on `Workspace.OperationError`. If a future disposal coordinator cancels, the UI still receives the failure instead of a silently lost discarded-task exception.

## Concerns

None open.

## Final whole-branch review fixes - 2026-07-19

### Scope

- Fixed Important 1 by gating `TWW3_COMPANION_TEST_MANAGED_ROOT` behind `TWW3_COMPANION_TEST_MODE=1`; normal runtime path detection now uses `ManagedPaths.Detect(...)` when test mode is off.
- Fixed Important 2 by adding Workspace-visible operation error state and binding it near `Return Home`; disposal failure keeps `CurrentScreen == Workspace` and displays the failure on the Workspace shell.
- Removed the stale no-op `CompleteWorkspaceDisposalForTest()` seam; tests now use `IWorkspaceDisposalCoordinator`.
- Did not modify historical Task 3 reports. Disposal errors are surfaced through `Workspace.OperationError`, not `Home.SettingsSaveError`.

### Regression coverage

- `ApplicationCompositionTests.RuntimeWithoutTestModeIgnoresManagedRootEnvironmentVariable`
- `HomeCompositionTests.ReturnHomeDisposalFailureStaysOnWorkspaceAndShowsWorkspaceError`
- `ShellViewModelTests.WorkspaceAndReturnHomeActionsChangeScreen` now exercises the real coordinator seam.

### Required verification evidence

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj --filter "HomeCompositionTests|ApplicationCompositionTests|ShellViewModelTests"
```

Exit code `0`; `Failed: 0, Passed: 21, Skipped: 0, Total: 21`.

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj
```

Exit code `0`; `Failed: 0, Passed: 34, Skipped: 0, Total: 34`.

```powershell
git diff --check
```

Exit code `0`; no whitespace errors. Git reported line-ending normalization warnings for touched files only.

## External review fixes - 2026-07-20

### Must fix

1. Removed stray Create-dialog description `TextBlock`s from `HomeView.axaml` (dialog copy remains only in `WorkspaceDialogService`).
2. Settings save from `SetTheme`, `RemoveRecent`, and `RetrySettingsSave` is now async (`SaveSettingsAsync`) and no longer blocks the UI thread with `.GetAwaiter().GetResult()`.
3. `RecentWorkspace` now stores `DisplayName` at create/open time; Home recents prefer that value over the sanitized filename.

### Noted

4. Replaced reflection-based managed-path test seam with `IManagedPathInitializer`.
5. `CompositionTestOptions.NativeStartupDialog` is now `IStartupNotification`.
6. Corrected the stale disposal-error limitation in this report.
7. Documented Return Home fire-and-forget cancellation handling above.
