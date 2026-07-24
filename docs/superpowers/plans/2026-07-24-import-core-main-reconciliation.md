# Import Core Main Reconciliation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Integrate the completed `origin/import-core` application and desktop import work into current `origin/main` without losing the collection-library overlay merged by PR #11.

**Architecture:** Treat current `origin/main` as the only branch base and `origin/import-core` at `8cfad5d` as source material, not as a branch to merge wholesale. First transplant the isolated Application import core and prove it independently. Then combine the import shell service with the existing workspace query, library view models, and overlay bindings so both features coexist in one composition root.

**Tech Stack:** C# / .NET 10, Avalonia 12.1.0, xUnit, existing Application and Desktop projects, GitHub-hosted `origin/import-core` provenance.

## Global Constraints

- Every implementation task in this plan must enter through the orchestrator and use the rigid `IMP` implementation role followed by the `REV` review role.
- The Product Owner's approval of this implementation plan is the only approval that may not be delegated.
- Start every managed task from the current `origin/main`; do not run the task in the old `import-core` worktree.
- Use `origin/import-core` commit `8cfad5d` only as the reviewed source for import-core files and behavior.
- Do not merge or rebase the complete `import-core` branch onto `main`; its stale task reports and project-document edits overlap newer canonical files.
- Preserve PR #11's `WorkspaceLibraryQuery`, `WorkspaceLibrarySnapshot`, `ModLibraryViewModel`, `CollectionDetailViewModel`, and `MainWindow.axaml` overlay bindings.
- Preserve the canonical `.superpowers/sdd/task-2-report.md` from PR #11. Do not restore any `.superpowers/sdd/` report from `origin/import-core`.
- Do not add SQLite catalog tables or real import persistence in this reconciliation. Those belong to the next local-persistence slice.
- Keep imports additive-only, preserve the staged preview/apply boundary, and keep both new-Workspace and current-Workspace target contexts.
- Use the installed .NET 10 executable at `C:\Users\steve\.dotnet\dotnet.exe`.

---

### Task 1: Transplant and verify the Application import core

**Files:**
- Create: `src/Tww3Companion.Application/Importing/CurrentWorkspaceImportSession.cs`
- Create: `src/Tww3Companion.Application/Importing/IImportEngine.cs`
- Create: `src/Tww3Companion.Application/Importing/IWorkspaceImportStore.cs`
- Create: `src/Tww3Companion.Application/Importing/ImportCandidate.cs`
- Create: `src/Tww3Companion.Application/Importing/ImportEngine.cs`
- Create: `src/Tww3Companion.Application/Importing/ImportOutcome.cs`
- Create: `src/Tww3Companion.Application/Importing/ImportPreview.cs`
- Create: `src/Tww3Companion.Application/Importing/ImportResolution.cs`
- Create: `src/Tww3Companion.Application/Importing/ImportTargetContext.cs`
- Create: `src/Tww3Companion.Application/Importing/NewWorkspaceImportSession.cs`
- Create: `tests/Tww3Companion.Application.Tests/Importing/ImportEngineTests.cs`

**Interfaces:**
- Consumes: existing `MarkdownImportCandidate`, `SteamImportCandidate`, and the two source adapters already on `main`
- Produces: `IImportEngine.BuildPreviewAsync(...)`, `IImportEngine.ApplyAsync(...)`, `IWorkspaceImportStore`, and the closed `ImportTargetContext` hierarchy used by Task 2

- [ ] **Step 1: Confirm the managed checkout and source commit**

Run:

```powershell
git status --short --branch
git fetch origin main import-core
git rev-parse origin/import-core
git merge-base --is-ancestor 8cfad5d origin/import-core
```

Expected:

- the managed checkout is clean and based on current `origin/main`;
- `git rev-parse origin/import-core` prints `8cfad5d...` or a descendant containing it;
- the ancestry check exits `0`.

- [ ] **Step 2: Restore only the import-engine test and verify the red state**

Run:

```powershell
git restore --source=8cfad5d -- tests/Tww3Companion.Application.Tests/Importing/ImportEngineTests.cs
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj --filter ImportEngineTests -v minimal
```

Expected: compilation fails because `IImportEngine`, `ImportEngine`, `ImportTargetContext`, `ImportPreview`, `ImportOutcome`, `ImportCandidate`, and `IWorkspaceImportStore` do not yet exist on `main`. This is the required failing-test evidence.

- [ ] **Step 3: Restore the isolated Application implementation**

Run:

```powershell
git restore --source=8cfad5d -- `
  src/Tww3Companion.Application/Importing/CurrentWorkspaceImportSession.cs `
  src/Tww3Companion.Application/Importing/IImportEngine.cs `
  src/Tww3Companion.Application/Importing/IWorkspaceImportStore.cs `
  src/Tww3Companion.Application/Importing/ImportCandidate.cs `
  src/Tww3Companion.Application/Importing/ImportEngine.cs `
  src/Tww3Companion.Application/Importing/ImportOutcome.cs `
  src/Tww3Companion.Application/Importing/ImportPreview.cs `
  src/Tww3Companion.Application/Importing/ImportResolution.cs `
  src/Tww3Companion.Application/Importing/ImportTargetContext.cs `
  src/Tww3Companion.Application/Importing/NewWorkspaceImportSession.cs
```

The restored API must retain these exact entry points:

```csharp
public interface IImportEngine
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

public abstract record ImportTargetContext
{
  public sealed record NewWorkspace(string DisplayName, string DestinationPath) : ImportTargetContext;
  public sealed record CurrentWorkspace(string WorkspaceId) : ImportTargetContext;
}
```

Do not restore `SteamSingleItemImportAdapterTests.cs`; current `main` already contains PR #11's formatting repair and the source models consumed by the engine.

- [ ] **Step 4: Verify the focused and full Application test suites**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj --filter ImportEngineTests -v minimal
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj -v minimal
git diff --check
```

Expected:

- all `ImportEngineTests` pass;
- all Application tests pass, including PR #11's `WorkspaceQueryTests` and `WorkspaceLibrarySnapshotTests`;
- `git diff --check` prints nothing.

- [ ] **Step 5: Inspect scope and commit**

Run:

```powershell
git status --short
git diff --stat
git diff --name-only
```

Expected: only the eleven Application/test files listed in this task are changed. No documentation, task report, Desktop, Infrastructure, or overlay file is modified.

Commit:

```powershell
git add src/Tww3Companion.Application/Importing tests/Tww3Companion.Application.Tests/Importing/ImportEngineTests.cs
git commit -m "feat: integrate shared import core"
```

The orchestrator must send the commit to `REV`. Task 2 must not start until that review accepts the Application contract and focused tests.

---

### Task 2: Reconcile desktop import wiring with the collection-library overlay

**Files:**
- Modify: `src/Tww3Companion.Desktop/Composition/ApplicationComposition.cs`
- Modify: `src/Tww3Companion.Desktop/ViewModels/ShellViewModel.cs`
- Modify: `src/Tww3Companion.Desktop/Views/HomeView.axaml`
- Modify: `src/Tww3Companion.Desktop/Views/MainWindow.axaml`
- Modify: `tests/Tww3Companion.Desktop.Tests/ViewModels/HomeCompositionTests.cs`
- Modify: `tests/Tww3Companion.Desktop.Tests/ViewModels/ShellViewModelTests.cs`
- Modify: `tests/Tww3Companion.Desktop.Tests/Views/MainWindowLayoutTests.cs`

**Interfaces:**
- Consumes: Task 1's `IImportEngine`, `ImportEngine`, `ImportTargetContext`, `ImportPreview`, `ImportOutcome`, and current `main`'s `WorkspaceLibraryQuery`
- Produces: a single `ShellViewModel` and `ApplicationComposition` that expose both import commands and the PR #11 library/collection overlay

- [ ] **Step 1: Add the import-shell tests without replacing overlay tests**

Restore `ShellViewModelTests.cs`, which has no PR #11 branch changes:

```powershell
git restore --source=8cfad5d -- tests/Tww3Companion.Desktop.Tests/ViewModels/ShellViewModelTests.cs
```

In `HomeCompositionTests.HomeStartsVisibleAndShowsOnlyApprovedActions`, replace:

```csharp
Assert.DoesNotContain(subject.Home.NavigationItems, item => item.Contains("Import", StringComparison.OrdinalIgnoreCase));
```

with:

```csharp
Assert.Contains("Import into new Workspace", subject.Home.NavigationItems);
```

Keep `LibraryAndCollectionPanelsStartEmptyWhenNoQueryIsAvailable` and every other PR #11 overlay test unchanged.

In `MainWindowLayoutTests`, replace the old negative import assertion with:

```csharp
Assert.Contains("Import into current Workspace", text);
Assert.Contains("Command=\"{Binding ImportIntoCurrentWorkspaceCommand}\"", text);
```

Keep all assertions for `CollectionDetail.Collections`, `ModLibrary.Mods`, inspector bindings, and the three-column overlay layout.

- [ ] **Step 2: Run the desktop tests and verify the red state**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj --filter "ShellViewModelTests|HomeCompositionTests|MainWindowLayoutTests" -v minimal
```

Expected: failures show that the current shell lacks `IShellImportService`, import commands, import navigation labels, and import buttons. Overlay-specific tests must continue compiling.

- [ ] **Step 3: Combine the shell dependencies**

Add `using Tww3Companion.Application.Importing;` and restore the `IShellImportService` contract from `8cfad5d`:

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
```

The merged `ShellViewModel` must retain both dependency seams:

```csharp
private readonly WorkspaceLibraryQuery? workspaceLibraryQuery;
private readonly IShellImportService importService;
```

Extend, rather than replace, the current factory signatures:

```csharp
public static ShellViewModel CreateForTest(
    IWorkspaceDialogService? workspaceDialogService = null,
    IApplicationSettingsStore? settingsStore = null,
    IWorkspaceDisposalCoordinator? workspaceDisposalCoordinator = null,
    IShellImportService? importService = null,
    WorkspaceLibraryQuery? workspaceLibraryQuery = null)

public static ShellViewModel Create(
    ApplicationSettings initialSettings,
    IApplicationSettingsStore settingsStore,
    IWorkspaceDialogService workspaceDialogService,
    CreateWorkspace createWorkspace,
    OpenWorkspace openWorkspace,
    string defaultWorkspaceDirectory,
    string settingsDirectory,
    IWorkspaceDisposalCoordinator workspaceDisposalCoordinator,
    WorkspaceLibraryQuery? workspaceLibraryQuery = null,
    IShellImportService? importService = null)
```

Assign both options and preserve construction of the overlay view models:

```csharp
workspaceLibraryQuery = options.WorkspaceLibraryQuery;
importService = options.ImportService;
ModLibrary = new ModLibraryViewModel(workspaceLibraryQuery);
CollectionDetail = new CollectionDetailViewModel();
```

The combined workspace destinations are:

```csharp
private static readonly IReadOnlyList<string> DefaultWorkspaceDestinations =
    ["Mod Library", "Collections", "Import into current Workspace"];
```

The Home navigation items are:

```csharp
["Home", "Mod Library", "Collections", "Import into new Workspace"]
```

Restore the two import commands, `RunImportIntoNewWorkspaceAsync`, `RunImportIntoCurrentWorkspaceAsync`, the passive test service, and their test hooks from `8cfad5d`. Preserve PR #11's `LoadWorkspaceLibraryAsync`, `ClearWorkspaceLibrary`, `SelectMod`, and `SelectCollection` methods.

When create/open succeeds, set the import target ID and load the overlay from the same result:

```csharp
if (result is OperationResult<Workspace>.Success success)
{
  currentWorkspaceId = success.Value.Id.ToString();
  settings = await settingsStore.LoadAsync(CancellationToken.None);
  UpdateHome(Home.SettingsSaveError);
  await LoadWorkspaceLibraryAsync(path);
}
```

When returning Home after successful workspace disposal, clear both forms of workspace state:

```csharp
currentWorkspaceId = null;
ClearWorkspaceLibrary();
```

- [ ] **Step 4: Combine the composition root**

Preserve the current lifecycle and query:

```csharp
var lifecycle = CreateWorkspaceLifecycle(settingsStore);
var workspaceLibraryQuery = new WorkspaceLibraryQuery(lifecycle.WorkspaceStore);
```

Restore the `EngineShellImportService` and `CompositionWorkspaceImportStore` adapters from `8cfad5d`, then pass both services by name:

```csharp
var shell = ShellViewModel.Create(
    settings,
    settingsStore,
    workspaceDialogService,
    lifecycle.CreateWorkspace,
    lifecycle.OpenWorkspace,
    paths.WorkspacesDirectory,
    Path.GetDirectoryName(paths.SettingsFile)!,
    workspaceDisposalCoordinator,
    workspaceLibraryQuery: workspaceLibraryQuery,
    importService: new EngineShellImportService(
        new ImportEngine(new CompositionWorkspaceImportStore())));
```

Keep `WorkspaceLifecycle` in its PR #11 form:

```csharp
internal sealed record WorkspaceLifecycle(
    IWorkspaceStore WorkspaceStore,
    CreateWorkspace CreateWorkspace,
    OpenWorkspace OpenWorkspace);
```

The composition store remains an explicit non-persistent seam in this reconciliation. Do not claim that its commit methods write catalog rows; real SQLite persistence is the next slice.

- [ ] **Step 5: Add both import buttons without replacing overlay bindings**

In `HomeView.axaml`, add:

```xml
<Button Content="Import into new Workspace"
        AutomationProperties.Name="Import into new Workspace"
        Command="{Binding ImportIntoNewWorkspaceCommand}" />
```

In `MainWindow.axaml`, add this button after the existing `Mod Library` button:

```xml
<Button HorizontalContentAlignment="Left"
        Content="Import into current Workspace"
        AutomationProperties.Name="Import into current Workspace"
        Command="{Binding ImportIntoCurrentWorkspaceCommand}" />
```

Do not replace or simplify the PR #11 `ItemsControl` bindings for collections, mods, inspector details, or empty-state visibility.

- [ ] **Step 6: Verify the focused desktop behavior**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj --filter "ShellViewModelTests|HomeCompositionTests|ModLibraryViewModelTests|MainWindowLayoutTests" -v minimal
```

Expected:

- the Home action builds a `NewWorkspace` import target;
- the Workspace action builds a `CurrentWorkspace` target from the loaded Workspace ID;
- the mod library and collection panels still load from `WorkspaceLibrarySnapshot`;
- collection membership markers and inspector selection tests still pass;
- both import buttons are present and bound.

- [ ] **Step 7: Run the complete repository verification**

Run each command separately and record every exit code:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' format Tww3Companion.sln --verify-no-changes
& 'C:\Users\steve\.dotnet\dotnet.exe' build Tww3Companion.sln -c Release --no-restore
& 'C:\Users\steve\.dotnet\dotnet.exe' test Tww3Companion.sln -c Release --no-build
git diff --check
```

Expected: all four commands exit `0`. Do not use one chained command for this verification because an earlier combined run obscured which process had stalled.

- [ ] **Step 8: Inspect scope and commit**

Run:

```powershell
git status --short
git diff --stat
git diff --name-only
```

Expected:

- only the seven Desktop/test files listed in this task are changed;
- no `.superpowers/sdd/` report is changed;
- no roadmap, changelog, project-history, schema, migration, or persistence file is changed.

Commit:

```powershell
git add `
  src/Tww3Companion.Desktop/Composition/ApplicationComposition.cs `
  src/Tww3Companion.Desktop/ViewModels/ShellViewModel.cs `
  src/Tww3Companion.Desktop/Views/HomeView.axaml `
  src/Tww3Companion.Desktop/Views/MainWindow.axaml `
  tests/Tww3Companion.Desktop.Tests/ViewModels/HomeCompositionTests.cs `
  tests/Tww3Companion.Desktop.Tests/ViewModels/ShellViewModelTests.cs `
  tests/Tww3Companion.Desktop.Tests/Views/MainWindowLayoutTests.cs
git commit -m "feat: reconcile import shell with library overlay"
```

The orchestrator must send the combined diff to `REV`. Acceptance requires explicit reviewer confirmation that neither import behavior nor PR #11's overlay behavior was lost.

---

## Final Integration Gate

After both reviewed tasks merge, the orchestrator must re-read the merged `main` state and confirm:

```powershell
git fetch origin main
git status --short --branch
& 'C:\Users\steve\.dotnet\dotnet.exe' format Tww3Companion.sln --verify-no-changes
& 'C:\Users\steve\.dotnet\dotnet.exe' build Tww3Companion.sln -c Release --no-restore
& 'C:\Users\steve\.dotnet\dotnet.exe' test Tww3Companion.sln -c Release --no-build
git diff --check
```

The reconciliation is complete only when:

- `main` contains the Application import engine types and tests from `8cfad5d`;
- Home and current-Workspace import actions both use that engine;
- `WorkspaceLibraryQuery` and the collection-library overlay remain wired;
- the canonical PR #11 task report remains unchanged;
- no catalog persistence is implied or implemented;
- the complete solution passes formatting, build, tests, and whitespace verification.
