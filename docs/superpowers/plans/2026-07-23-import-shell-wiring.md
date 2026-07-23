# Import Shell Wiring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire the Desktop shell so Home can start import into a new Workspace and the Workspace shell can start import into the current Workspace through the shared import engine.

**Architecture:** Add one small shell-level import seam so the view-model can trigger import actions without the views knowing anything about import rules or target selection. Home and Workspace stay thin: they expose entry points and route into the same import service with different `ImportTargetContext` values. The application-layer import engine remains the source of truth for preview, validation, and commit.

**Tech Stack:** .NET 10, Avalonia 12.1.0, existing Desktop MVVM view-models and command bindings, xUnit.

## Global Constraints

- Keep imports additive-only; omission never removes existing Mods or Memberships.
- Preserve RFC-0004’s staged import pipeline: source adapter → candidates → normalisation → exact identity matching → suggested name matches → editable preview → required resolutions → domain validation → one atomic transaction.
- Do not infer dependencies, compatibility claims, or ordering rules from prose.
- Source references may match automatically; source-neutral candidates must be linked to an existing Mod, created with a display name, or skipped before application.
- Failed validation or persistence rolls back the entire confirmed import.
- The shared import engine must support both target contexts: import into new Workspace and import into current Workspace.

---

### Task 1: Add a small Desktop import service seam and test harness support

**Files:**
- Modify: `src/Tww3Companion.Desktop/ViewModels/ShellViewModel.cs`
- Modify: `tests/Tww3Companion.Desktop.Tests/ViewModels/ShellViewModelTests.cs`

**Interfaces:**
- Consumes: the shared import engine from `Tww3Companion.Application.Importing`
- Produces: a shell-level import service seam that later Home and Workspace commands can call with `ImportTargetContext`

- [ ] **Step 1: Write the failing tests**

Add tests that pin the new seam without wiring any new UI yet:

```csharp
[Fact]
public void ShellViewModel_exposes_a_test_import_service_seam()
{
    var shell = ShellViewModel.CreateForTest();

    Assert.NotNull(shell.ImportService);
}

[Fact]
public void ShellViewModel_test_seam_can_record_import_requests()
{
    var importService = new RecordingImportService();
    var shell = ShellViewModel.CreateForTest(importService: importService);

    shell.RequestImportIntoNewWorkspaceForTest();

    Assert.Equal(ImportTargetContext.ForNewWorkspace("My New Workspace", "C:\\Workspaces\\my-new.tww3c"), importService.LastTargetContext);
}

private sealed class RecordingImportService : IShellImportService
{
    public ImportTargetContext? LastTargetContext { get; private set; }

    public Task<ImportPreview> BuildPreviewAsync(
        ImportTargetContext targetContext,
        IReadOnlyList<object> candidates,
        CancellationToken cancellationToken = default)
    {
        LastTargetContext = targetContext;
        return Task.FromResult(new ImportPreview(targetContext, candidates, Applied: false));
    }

    public Task<ImportOutcome> ApplyAsync(
        ImportPreview preview,
        bool confirm,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ImportOutcome(preview.TargetContext, preview.Candidates, confirm));
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Tww3Companion.Desktop.Tests --filter "ShellViewModel_exposes_a_test_import_service_seam|ShellViewModel_test_seam_can_record_import_requests" -v normal`

Expected: fail because `IShellImportService`, the seam, and the test hook do not exist yet.

- [ ] **Step 3: Add the minimal seam**

Implement the smallest Desktop-facing import seam and test hook:

```csharp
public interface IShellImportService
{
  Task<ImportPreview> BuildPreviewAsync(
      ImportTargetContext targetContext,
      IReadOnlyList<object> candidates,
      CancellationToken cancellationToken = default);

  Task<ImportOutcome> ApplyAsync(
      ImportPreview preview,
      bool confirm,
      CancellationToken cancellationToken = default);
}

public sealed class ShellViewModel : ViewModelBase
{
  public IShellImportService ImportService { get; }

  public static ShellViewModel CreateForTest(
      IWorkspaceDialogService? workspaceDialogService = null,
      IApplicationSettingsStore? settingsStore = null,
      IWorkspaceDisposalCoordinator? workspaceDisposalCoordinator = null,
      IShellImportService? importService = null);

  public void RequestImportIntoNewWorkspaceForTest();
  public void RequestImportIntoCurrentWorkspaceForTest();
}
```

Keep the seam passive: do not wire buttons or route UI events yet.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Tww3Companion.Desktop.Tests --filter "ShellViewModel_exposes_a_test_import_service_seam|ShellViewModel_test_seam_can_record_import_requests" -v normal`

Expected: pass after the seam and test hook exist.

- [ ] **Step 5: Commit**

```bash
git add src/Tww3Companion.Desktop/ViewModels/ShellViewModel.cs tests/Tww3Companion.Desktop.Tests/ViewModels/ShellViewModelTests.cs
git commit -m "feat: add desktop import service seam"
```

### Task 2: Wire the Home and Workspace shell actions to the shared import engine

**Files:**
- Modify: `src/Tww3Companion.Desktop/ViewModels/ShellViewModel.cs`
- Modify: `src/Tww3Companion.Desktop/Views/HomeView.axaml.cs`
- Modify: `src/Tww3Companion.Desktop/Views/MainWindow.axaml.cs`
- Modify: `tests/Tww3Companion.Desktop.Tests/ViewModels/ShellViewModelTests.cs`
- Modify: `tests/Tww3Companion.Desktop.Tests/Views/MainWindowLayoutTests.cs`

**Interfaces:**
- Consumes: `ShellViewModel.ImportService` and the import target contexts from Tasks 2–4 of the import-core slice
- Produces: Home and Workspace actions that call the same import service with different `ImportTargetContext` values

- [ ] **Step 1: Write the failing tests**

Add tests that pin the two entry points and their target contexts:

```csharp
[Fact]
public void Home_exposes_import_into_new_workspace()
{
    var shell = ShellViewModel.CreateForTest(importService: new RecordingImportService());

    Assert.Contains("Import into new Workspace", shell.Home.NavigationItems);
}

[Fact]
public void Workspace_shell_exposes_import_into_current_workspace()
{
    var shell = ShellViewModel.CreateForTest(importService: new RecordingImportService());

    Assert.Contains("Import into current Workspace", shell.WorkspaceDestinations);
}

[Fact]
public async Task Home_import_action_uses_new_workspace_target_context()
{
    var importService = new RecordingImportService();
    var shell = ShellViewModel.CreateForTest(importService: importService);

    await shell.RunImportIntoNewWorkspaceForTestAsync();

    Assert.Equal(
        ImportTargetContext.ForNewWorkspace("My New Workspace", "C:\\Workspaces\\my-new.tww3c"),
        importService.LastTargetContext);
}

[Fact]
public async Task Workspace_import_action_uses_current_workspace_target_context()
{
    var importService = new RecordingImportService();
    var shell = ShellViewModel.CreateForTest(importService: importService);

    await shell.RunImportIntoCurrentWorkspaceForTestAsync("workspace-id-123");

    Assert.Equal(
        ImportTargetContext.ForCurrentWorkspace("workspace-id-123"),
        importService.LastTargetContext);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Tww3Companion.Desktop.Tests --filter "Home_exposes_import_into_new_workspace|Workspace_shell_exposes_import_into_current_workspace|Home_import_action_uses_new_workspace_target_context|Workspace_import_action_uses_current_workspace_target_context" -v normal`

Expected: fail until the shell actions and bindings exist.

- [ ] **Step 3: Wire the UI actions**

Connect the commands and view bindings:

```csharp
- Home → Import into new Workspace;
- Workspace shell → Import into current Workspace;
- both actions into the same shared import service seam.
```

Keep the view code thin: the views should only bind buttons or menu items to view-model commands. They should not embed import logic.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Tww3Companion.Desktop.Tests --filter "Home_exposes_import_into_new_workspace|Workspace_shell_exposes_import_into_current_workspace|Home_import_action_uses_new_workspace_target_context|Workspace_import_action_uses_current_workspace_target_context" -v normal`

Expected: pass after the shell actions are wired.

- [ ] **Step 5: Commit**

```bash
git add src/Tww3Companion.Desktop tests/Tww3Companion.Desktop.Tests
git commit -m "feat: wire import entry points to shared engine"
```

### Task 3: Verify the desktop shell integration end to end

**Files:**
- None expected unless the UI wiring exposes a small fix

**Interfaces:**
- Consumes: the shell seam and the Home/Workspace import actions
- Produces: a verified Desktop slice that is ready for full branch verification

- [ ] **Step 1: Run the full verification commands**

Run:

```powershell
dotnet test tests/Tww3Companion.Desktop.Tests -v normal
dotnet test Tww3Companion.sln -c Release --no-build
git diff --check
```

Expected: all commands succeed.

- [ ] **Step 2: Fix any small verification issues**

If a test or diff-check failure appears, make the smallest code change needed, then rerun the same verification commands.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: complete desktop import shell wiring"
```
