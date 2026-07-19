# Task 3 Report: Verify the slice and prepare the branch for integration

## Status

Complete. Home composition on branch `codex/workspace-foundation` (HEAD `9f8199b`) is verified and ready for integration into the next packaging/documentation stage.

## Scope verified

This task re-ran the official verification set against the Task 2 implementation:

- Shared `ShellViewModel` with nested `HomeView` inside `MainWindow`
- `ApplicationComposition` startup root with nine-step order
- Native pre-logging failure dialogs
- Smoke and single-instance test hooks
- Return-home disposal boundary via `IWorkspaceDisposalCoordinator`

No spec-to-code mismatches were found; the plan file was left unchanged.

## Verification evidence

### Full desktop test project

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj
```

Exit code `0`; `Failed: 0, Passed: 32, Skipped: 0, Total: 32`.

### Focused Home/composition/shell tests

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj --filter "HomeCompositionTests|ApplicationCompositionTests|ShellViewModelTests"
```

Exit code `0`; `Failed: 0, Passed: 19, Skipped: 0, Total: 19`.

### Whitespace

```powershell
git diff --check
```

Exit code `0`; no whitespace errors.

## In-process smoke and composition hook evidence

The approved xUnit harness exercises all test hooks without a separate CLI invocation:

| Area | Test | Result |
|------|------|--------|
| Startup order | `CompositionUsesTheApprovedStartupOrder` | Nine approved steps match `ApplicationComposition.StartupSteps` |
| Pre-logging managed path | `ManagedPathFailureShowsNativeBlockingDialogAndDoesNotWriteLogEntry` | Exit `1`, dialog shown, zero `*.log` files |
| Pre-logging single instance | `SingleInstanceFailureShowsAlreadyRunningMessage` | Exit `1`, exact already-running message |
| Work-area gate | `RuntimeEvaluatesWorkAreaBeforeShellCanBePresented` | Compatibility shown when work area is below minimum |
| Initial screen wiring | `StartupNoLongerReliesOnMainWindowOpenedToChooseInitialScreen` | `AttachTopLevel` precedes `desktop.MainWindow` assignment |
| Smoke round trip | `SmokeTestWritesResultJsonAtDirectoryRoot` | Exit `0`; `smoke-result.json` at directory root with `workspaceId`, `displayName`, `applicationMode`, `managedRoot` |
| Smoke composition root | `SmokeTestUsesApplicationCompositionRootInsteadOfManualStartupWiring` | Uses `CreateSmokeTestRuntime`, no manual adapter wiring |
| Single-instance guard | `HoldSingleInstanceFailsCleanlyWhenManagedRootEnvironmentVariableIsMissing` | `ArgumentException` when `TWW3_COMPANION_TEST_MANAGED_ROOT` absent |
| Return-home disposal | `ReturnHomeDisposalPreventsScreenChangeUntilDisposalSucceeds` | `CurrentScreen` stays `Workspace` until coordinator completes |

## Tradeoffs

- **Production disposal stub:** `WorkspaceDisposalCoordinator.DisposeWorkspaceScopeAsync` returns `Task.CompletedTask`. The typed coordinator and async return-home path are in place so a future workspace runtime slice can plug in real teardown without changing the shell contract.
- **Test-only composition seams:** `CompositionTestOptions`, `RunStartupForTest`, and `CreateRuntimeForTest` exist solely for deterministic in-process startup tests; they are not part of the public application surface.
- **Dialog boundaries:** Create/open workspace flows depend on Avalonia `TopLevel` dialogs via `IWorkspaceDialogService`. Tests verify source copy and command wiring; interactive prompt layout is not automated in CI.

## Limitations (explicit)

### Pre-logging failure path

`ApplicationComposition.CreateRuntime` performs managed-path initialization and single-instance acquisition before `LoggingConfiguration.CreateProvider`. Failures at either gate call `NativeStartupDialog.ShowBlockingError` and return `null` (exit code `1`) with no log file created. This is verified by `ManagedPathFailureShowsNativeBlockingDialogAndDoesNotWriteLogEntry`, which asserts `RecordingNativeStartupDialog.Messages` is non-empty and `Directory.EnumerateFiles(..., "*.log")` is empty.

Failures that occur after logging is configured (settings load, adapter construction, etc.) are outside the native-dialog-only constraint and will use the logging provider.

### Return-home disposal boundary

`ShellViewModel.ReturnHomeAsync` sets `isDisposingWorkspace`, awaits `workspaceDisposalCoordinator.DisposeWorkspaceScopeAsync`, and only then calls `SetScreen(ShellScreen.Home)`. While disposal is in progress, `CurrentScreen` remains `Workspace` (verified by `ReturnHomeDisposalPreventsScreenChangeUntilDisposalSucceeds`).

Remaining edges:

- Disposal exceptions surface on `Home.SettingsSaveError` while the user remains on Workspace; there is no dedicated retry-disposal UI.
- Re-entrant Return Home calls are ignored while `isDisposingWorkspace` is true.
- Production disposal is currently a no-op; the async boundary is contractual until workspace resources require real teardown.

## Self-review checklist

- Home is a nested view, not a separate window.
- One shared `ShellViewModel`, not a second Home view model.
- Return-home disposal is called out explicitly in code and tests.
- `smoke-result.json` has one deterministic path: `<directory>\smoke-result.json`.
- Pre-logging failures are native-dialog only.
- Startup order matches the reviewed spec exactly.
- No placeholder steps remain.

## Concerns

None open.

## Commits

- Verification report: `test: verify home composition slice` (this task)
