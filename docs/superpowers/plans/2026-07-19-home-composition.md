# Home Composition Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Connect the approved Home flow to the existing shell through one shared view model, with startup composition, recents, settings recovery, and test hooks.

**Architecture:** `MainWindow` remains the shell host. One shared `ShellViewModel` owns Home, Compatibility, Workspace, theme, and recovery state, while `HomeView.axaml` renders as a nested view when the shell is on Home. Startup is wired through a small composition root so pre-Avalonia failures happen in the right order and the smoke hooks exercise the same lifecycle as real startup.

**Tech Stack:** C# / .NET 10, Avalonia, xUnit, Windows-only desktop startup, Win32 `MessageBoxW` behind an interface, existing workspace foundation services and use cases.

## Global Constraints

- `MainWindow` stays the shell host and the shell swaps nested views instead of opening a separate Home window.
- One shared `ShellViewModel` owns Home, Compatibility, Workspace, theme, and high-contrast state.
- Import stays out of this slice.
- Failures before logging starts are reported via native dialog only; no log entry is written.
- `smoke-result.json` is written at `<directory>\smoke-result.json`.
- `--hold-single-instance` fails immediately if `TWW3_COMPANION_TEST_MANAGED_ROOT` is absent.
- `ShellViewModel.CurrentScreen` must not switch from Workspace to Home until workspace-scoped disposal is confirmed.
- `WindowPlacementService` and the Task 8 shell layout remain unchanged by this slice.

---

### Task 1: Write failing Home, startup, and composition tests

**Files:**
- Create: `tests/Tww3Companion.Desktop.Tests/ViewModels/HomeCompositionTests.cs`
- Create: `tests/Tww3Companion.Desktop.Tests/Composition/ApplicationCompositionTests.cs`
- Modify: `tests/Tww3Companion.Desktop.Tests/ViewModels/ShellViewModelTests.cs`

**Interfaces:**
- Consumes: `ShellViewModel`, `MainWindow`, `ApplicationComposition`, `IWorkspaceDialogService`, `NativeStartupDialog`, `SmokeTestCommand`, `ShellScreen`, `WorkspaceOperationState`
- Produces: exact property and command names that later tasks implement

- [ ] **Step 1: Add the failing shared-view-model tests**

Write a single test file that describes the required Home state and transitions:

```csharp
[Fact]
public void HomeStartsVisibleAndShowsOnlyApprovedActions()
{
    var subject = new ShellViewModel();

    Assert.Equal(ShellScreen.Home, subject.CurrentScreen);
    Assert.NotNull(subject.Home);
    Assert.Equal("Create Workspace", subject.Home.PrimaryActionLabel);
    Assert.Equal("Open Workspace", subject.Home.SecondaryActionLabel);
    Assert.Contains(subject.Home.Recents, recent => recent.DisplayName == "Missing Workspace");
    Assert.Equal("This Workspace contains no Mods or Collections yet. No data has been added.", subject.Workspace.EmptyStateMessage);
    Assert.DoesNotContain(subject.Home.NavigationItems, item => item.Contains("Import", StringComparison.OrdinalIgnoreCase));
}
```

Add tests for:

- a create/open busy flag that prevents duplicate commands
- `Finalizing — please wait` appearing only in the finalizing state
- settings save failure keeping the in-memory value and exposing `RetrySettingsSaveCommand` and `OpenSettingsFolderCommand`
- return-home disposal preventing `CurrentScreen` from changing until disposal succeeds
- recents being loaded eagerly so missing items are visible immediately

- [ ] **Step 2: Add the failing composition tests**

Write tests that describe the startup order and the test hooks:

```csharp
[Fact]
public void CompositionUsesTheApprovedStartupOrder()
{
    var composition = ApplicationComposition.CreateForTest();

    Assert.Equal(
        [
            "detect application mode",
            "initialize and probe managed paths",
            "acquire single-instance lease",
            "configure logging",
            "load settings",
            "construct Infrastructure adapters",
            "construct Application use cases",
            "construct the shared shell view model and nested views",
            "evaluate the current work area and show Compatibility or Home"
        ],
        composition.StartupSteps);
}
```

Add tests for:

- managed-path failure showing the native blocking dialog and not writing a log entry
- single-instance failure showing `TWW3 Companion is already running for this Windows user. Close the existing installed or portable copy and try again.`
- `--smoke-test` writing `smoke-result.json` at the directory root
- `--hold-single-instance` failing cleanly when `TWW3_COMPANION_TEST_MANAGED_ROOT` is missing

- [ ] **Step 3: Run the Home/composition tests and confirm red**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj --filter "HomeCompositionTests|ApplicationCompositionTests|ShellViewModelTests"
```

Expected: compile failures for the new Home/composition types.

- [ ] **Step 4: Commit the failing tests**

```powershell
git add tests/Tww3Companion.Desktop.Tests
git commit -m "test: define home composition coverage"
```

### Task 2: Implement the shared shell, nested Home view, and startup composition

**Files:**
- Create: `src/Tww3Companion.Desktop/Views/HomeView.axaml`
- Create: `src/Tww3Companion.Desktop/Views/HomeView.axaml.cs`
- Modify: `src/Tww3Companion.Desktop/Views/MainWindow.axaml`
- Modify: `src/Tww3Companion.Desktop/Views/MainWindow.axaml.cs`
- Modify: `src/Tww3Companion.Desktop/ViewModels/ShellViewModel.cs`
- Create: `src/Tww3Companion.Desktop/Services/IWorkspaceDialogService.cs`
- Create: `src/Tww3Companion.Desktop/Services/WorkspaceDialogService.cs`
- Create: `src/Tww3Companion.Desktop/Composition/ApplicationComposition.cs`
- Create: `src/Tww3Companion.Desktop/Startup/SmokeTestCommand.cs`
- Create: `src/Tww3Companion.Desktop/Startup/NativeStartupDialog.cs`
- Modify: `src/Tww3Companion.Desktop/App.axaml.cs`
- Modify: `src/Tww3Companion.Desktop/App.axaml`

**Interfaces:**
- Consumes: the workspace lifecycle use cases, settings store, single-instance lease, native dialog abstraction, and the existing shell `ShellViewModel`
- Produces: `ShellViewModel.Home`, `ShellViewModel.Workspace`, `ShellViewModel.CurrentScreen`, `HomeView`, `ApplicationComposition`, `SmokeTestCommand`, `NativeStartupDialog`

- [ ] **Step 1: Implement the shared shell state**

Add the Home-facing surface to `ShellViewModel` without creating a second view model. Keep the existing theme and compatibility state, then add:

```csharp
public sealed class ShellViewModel
{
    public ShellScreen CurrentScreen { get; }
    public HomeShellState Home { get; }
    public WorkspaceShellState Workspace { get; }
    public ICommand CreateWorkspaceCommand { get; }
    public ICommand OpenWorkspaceCommand { get; }
    public ICommand RemoveRecentCommand { get; }
    public ICommand RetrySettingsSaveCommand { get; }
    public ICommand OpenSettingsFolderCommand { get; }
    public ICommand ReturnHomeCommand { get; }
    public ICommand ContinueAnywayCommand { get; }
}

public sealed record RecentWorkspaceItem(
    string DisplayName,
    string Path,
    bool IsMissing,
    bool IsRemovable);

public sealed record HomeShellState(
    string PrimaryActionLabel,
    string SecondaryActionLabel,
    IReadOnlyList<RecentWorkspaceItem> Recents,
    string SettingsSaveError,
    bool IsBusy,
    bool IsFinalizing);

public sealed record WorkspaceShellState(string EmptyStateMessage);
```

Keep Home and Workspace state in the same model so the nested view can switch without re-hydrating a second state owner. Add a small code comment on the Home navigation model referencing RFC-0005 and the next import slice.

- [ ] **Step 2: Add the nested Home view**

Create `HomeView.axaml` as a child view that binds to the shared `ShellViewModel`. Include:

- create/open buttons
- a recents list with remove buttons
- the synchronized-folder warning text
- the settings failure banner with Retry and Open Settings Folder buttons
- the exact display-name prompt and `.tww3c` filter UI copy

Use standard Avalonia controls only.

- [ ] **Step 3: Wire the shell host to swap nested content**

Update `MainWindow.axaml` so the shell host selects between `HomeView`, `CompatibilityView`, and the fixed workspace shell based on `CurrentScreen`.

Keep the Task 8 workspace layout unchanged, including the `1024 × 640` minimum window, three columns, and accessibility semantics.

- [ ] **Step 4: Implement startup composition and test hooks**

Add `ApplicationComposition` so startup follows the approved order exactly. It should detect app mode, initialize managed paths, acquire the single-instance lease, configure logging, load settings, construct adapters/use cases, build the shared shell model, and then show Home or Compatibility.

The work-area check happens before Home is shown. If the current display cannot support the minimum shell, the app shows Compatibility instead.

Implement `NativeStartupDialog` as a thin wrapper around `MessageBoxW`, and use it for both managed-path and single-instance failures.

Implement `SmokeTestCommand` so it uses the same composition root as the real app and writes:

```json
{
  "workspaceId": "…",
  "displayName": "Smoke Workspace",
  "applicationMode": "Installed",
  "managedRoot": "…"
}
```

Write `smoke-result.json` to the `<directory>` argument root. Make `--hold-single-instance` fail immediately with a clear argument error if `TWW3_COMPANION_TEST_MANAGED_ROOT` is absent.

- [ ] **Step 5: Run focused and full tests**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj --filter "HomeCompositionTests|ApplicationCompositionTests|ShellViewModelTests"
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj
```

Expected: the focused Home/composition tests pass first, then the full desktop test project passes with no regressions in the Task 8 shell or earlier slices.

- [ ] **Step 6: Commit the implementation**

```powershell
git add src/Tww3Companion.Desktop tests/Tww3Companion.Desktop.Tests
git commit -m "feat: connect workspace lifecycle to home"
```

### Task 3: Verify the slice and prepare the branch for integration

**Files:**
- Modify: `.superpowers/sdd/task-9-report.md`
- Modify: `docs/superpowers/plans/2026-07-19-home-composition.md` if the implementation finds any unavoidable spec-to-code mismatch

**Interfaces:**
- Consumes: the completed Home composition implementation
- Produces: a verified desktop slice ready for the next packaging/documentation stage

- [ ] **Step 1: Run the exact verification set**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj
git diff --check
```

If the implementation adds smoke hooks or composition wiring that can be exercised in-process, run those too from the approved test harness and record the result in the task report.

- [ ] **Step 2: Update the task report with evidence**

Document the final test status, any noteworthy tradeoffs, and any explicit limitations that remain from the spec, especially the pre-logging failure path and the return-home disposal boundary.

- [ ] **Step 3: Commit the verified slice report**

```powershell
git add .superpowers/sdd/task-9-report.md
git commit -m "test: verify home composition slice"
```

## Self-Review Checklist

- Home is a nested view, not a separate window.
- The plan uses one shared `ShellViewModel`, not a second Home view model.
- Return-home disposal is called out explicitly.
- `smoke-result.json` has one deterministic path.
- Pre-logging failures are explicitly native-dialog only.
- The startup order matches the reviewed spec exactly.
- No placeholder steps remain.
