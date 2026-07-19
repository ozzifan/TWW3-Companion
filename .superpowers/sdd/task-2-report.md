# Task 2 Report: Shared shell, nested Home view, and startup composition

## Status

Complete. The shared `ShellViewModel`, nested `HomeView`, `MainWindow` host swap, startup composition root, native startup dialog, and smoke/native test hooks are implemented on branch `codex/workspace-foundation`.

## Implementation summary

- Expanded `ShellViewModel` into the shared Home/Compatibility/Workspace state owner required by the Task 1 tests.
  - Added `ShellViewModel.Home`, `ShellViewModel.Workspace`, `ShellViewModel.CurrentScreen`, Home commands, operation state, recents, settings-save recovery state, and return-home disposal registration.
  - Preserved Task 8 theme/high-contrast behavior, compatibility warning behavior, `1024 x 640` minimums, and fixed workspace destinations.
  - Added the requested RFC-0005 Home navigation comment that points to the next Import slice.
- Added `HomeView.axaml` and code-behind as a nested child view bound to the shared `ShellViewModel`.
  - Includes Create/Open buttons, recents with Remove, synchronized-folder warning guidance, settings failure recovery buttons, display-name prompt copy, and `.tww3c` filter copy.
- Updated `MainWindow.axaml` and code-behind so the shell hosts `HomeView`, `CompatibilityView`, and the unchanged three-column `WorkspaceShell`.
- Added `IWorkspaceDialogService` and `WorkspaceDialogService` for Home command dialog boundaries.
- Added `ApplicationComposition`, `CompositionTestOptions`, and `ApplicationRuntime`.
  - Startup order exactly matches the approved nine-step list.
  - Managed-path failures use native dialog before logging.
  - Single-instance failures use the exact already-running message before settings load.
  - Real startup composes managed paths, logging, settings, infrastructure adapters, use cases, shared shell, and view attachment through one root.
- Added virtual `NativeStartupDialog` as a thin `MessageBoxW` wrapper.
- Added `SmokeTestCommand`.
  - `--smoke-test <directory>` initializes managed paths, creates `Smoke Workspace` at `<directory>\smoke.tww3c`, reopens through `OpenWorkspace`, validates the round trip, and writes `<directory>\smoke-result.json`.
  - `--hold-single-instance <milliseconds>` fails immediately with an `ArgumentException` if `TWW3_COMPANION_TEST_MANAGED_ROOT` is absent, otherwise acquires the normal guard and writes `lease-acquired.signal`.
- Updated `Program.cs` and `App.axaml.cs` to use the composition root while preserving existing Avalonia theme/high-contrast wiring.
- Made a minimal test-harness-only analyzer fix in `HomeCompositionTests`: `Task.Run` now receives `TestContext.Current.CancellationToken`. Assertions and API expectations were not changed.

## TDD RED evidence

Initial focused command:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj --filter "HomeCompositionTests|ApplicationCompositionTests|ShellViewModelTests"
```

Exit code: `1`.

Expected missing-surface failures:

- `CS0234: The type or namespace name 'Composition' does not exist in the namespace 'Tww3Companion.Desktop'`
- `CS0246: The type or namespace name 'NativeStartupDialog' could not be found`
- `CS0246: The type or namespace name 'IWorkspaceDialogService' could not be found`
- `CS0246: The type or namespace name 'CompositionTestOptions' could not be found`

After those missing production surfaces were added, the next run exposed only the Task 1 test harness analyzer issue:

- `xUnit1051: Calls to methods which accept CancellationToken should use TestContext.Current.CancellationToken`

That was fixed without changing behavioral assertions.

## TDD GREEN evidence

Focused Home/composition/shell command:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj --filter "HomeCompositionTests|ApplicationCompositionTests|ShellViewModelTests"
```

Final result: exit code `0`; `Failed: 0, Passed: 15, Skipped: 0, Total: 15`.

Full desktop test command:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj
```

Final result: exit code `0`; `Failed: 0, Passed: 28, Skipped: 0, Total: 28`.

Whitespace validation:

```powershell
git diff --check
```

Exit code `0`. Git reported only the repository's normal LF-to-CRLF working-copy warnings for Windows files; no whitespace errors were reported.

## Self-review

- Verified `MainWindow.axaml` still declares `Width="1280"`, `Height="800"`, `MinWidth="1024"`, `MinHeight="640"`, and the `WorkspaceShell` grid columns `208`, `*`, and `384`.
- Verified no `WindowPlacementService` changes were made.
- Verified `CompatibilityView` and existing single-instance startup tests still pass.
- Verified return-home disposal holds the Workspace screen until the registered disposal completion callback runs.
- Verified successful create/open command paths reset Home operation state before entering Workspace, so later Return Home does not leave Home busy.
- Verified settings-save post-commit failures surface the recovery banner without blocking the workspace transition.
- Verified smoke output contains `workspaceId`, `displayName`, `applicationMode`, and `managedRoot` and uses application use cases rather than direct SQLite calls.

## Concerns

None open.
