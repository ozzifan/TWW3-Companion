# Import Core and Target-Context Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the shared import core that can import Markdown and Steam inputs into either a brand-new Workspace or the currently open Workspace, using one validated preview-and-transaction path.

**Architecture:** The existing Markdown and Steam adapters produce source-specific candidates and diagnostics. This slice adds the shared import engine that takes those candidates plus a target context, performs normalisation, identity matching, preview, resolution, validation, and the atomic SQLite transaction, and returns a single import outcome for both entry points. Home and Workspace shell actions stay thin and delegate into the same application service.

**Tech Stack:** .NET 10, Avalonia 12.1.0, existing Application/Domain/Infrastructure layers, direct SQLite persistence in Infrastructure, xUnit.

## Global Constraints

- Keep imports additive-only; omission never removes existing Mods or Memberships.
- Preserve RFC-0004’s staged import pipeline: source adapter → candidates → normalisation → exact identity matching → suggested name matches → editable preview → required resolutions → domain validation → one atomic transaction.
- Do not infer dependencies, compatibility claims, or ordering rules from prose.
- Source references may match automatically; source-neutral candidates must be linked to an existing Mod, created with a display name, or skipped before application.
- Failed validation or persistence rolls back the entire confirmed import.
- The shared import engine must support both target contexts: import into new Workspace and import into current Workspace.

---

### Task 1: Define the shared import engine contract and target context model

**Files:**
- Create: `src/Tww3Companion.Application/Importing/IImportEngine.cs`
- Create: `src/Tww3Companion.Application/Importing/ImportTargetContext.cs`
- Create: `src/Tww3Companion.Application/Importing/ImportOutcome.cs`
- Create: `src/Tww3Companion.Application/Importing/ImportPreview.cs`
- Create: `tests/Tww3Companion.Application.Tests/Importing/ImportEngineTests.cs`

**Interfaces:**
- Consumes: Markdown/Steam import results plus a target context
- Produces: a shared import engine contract that can build preview, validate, and commit against either target context

- [ ] **Step 1: Write the failing tests**

Add tests that pin the engine API shape and target context behaviour:

```csharp
[Fact]
public void ImportTargetContext_can_represent_new_workspace_and_current_workspace()
{
    var newWorkspace = ImportTargetContext.ForNewWorkspace("My New Workspace", "C:\\Workspaces\\my-new.tww3c");
    var currentWorkspace = ImportTargetContext.ForCurrentWorkspace("workspace-id-123");

    Assert.Equal("My New Workspace", newWorkspace.DisplayName);
    Assert.Equal("workspace-id-123", currentWorkspace.WorkspaceId);
}

[Fact]
public async Task ImportEngine_builds_preview_from_candidates_and_target_context()
{
    var engine = new FakeImportEngine();
    var preview = await engine.BuildPreviewAsync(ImportTargetContext.ForCurrentWorkspace("workspace-id-123"), new[] { "candidate-1" });

    Assert.NotNull(preview);
}

private sealed class FakeImportEngine : IImportEngine
{
    public Task<ImportPreview> BuildPreviewAsync(
        ImportTargetContext targetContext,
        IReadOnlyList<object> candidates,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ImportPreview(targetContext, candidates, Applied: false));

    public Task<ImportOutcome> ApplyAsync(ImportPreview preview, bool confirm, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ImportOutcome(preview.TargetContext, preview.Candidates, confirm));
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Tww3Companion.Application.Tests --filter "ImportTargetContext_can_represent_new_workspace_and_current_workspace|ImportEngine_builds_preview_from_candidates_and_target_context" -v normal`

Expected: fail because the engine contract and target context types do not exist yet.

- [ ] **Step 3: Add the minimal contract**

Implement the smallest shared contract needed by later tasks:

```csharp
public sealed record ImportTargetContext(
    string? WorkspaceId,
    string? DisplayName,
    string? DestinationPath,
    bool CreateNewWorkspace)
{
  public static ImportTargetContext ForNewWorkspace(string displayName, string destinationPath);
  public static ImportTargetContext ForCurrentWorkspace(string workspaceId);
}

public interface IImportEngine
{
  Task<ImportPreview> BuildPreviewAsync(ImportTargetContext targetContext, IReadOnlyList<object> candidates, CancellationToken cancellationToken = default);
  Task<ImportOutcome> ApplyAsync(ImportPreview preview, bool confirm, CancellationToken cancellationToken = default);
}
```

If the repository already has a better candidate/result shape to reuse, reuse it rather than inventing parallel types.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Tww3Companion.Application.Tests --filter "ImportTargetContext_can_represent_new_workspace_and_current_workspace|ImportEngine_builds_preview_from_candidates_and_target_context" -v normal`

Expected: pass after the contract exists.

- [ ] **Step 5: Commit**

```bash
git add src/Tww3Companion.Application/Importing tests/Tww3Companion.Application.Tests/Importing
git commit -m "feat: add shared import engine contract"
```

### Task 2: Implement import into current Workspace through the shared engine

**Files:**
- Modify: `src/Tww3Companion.Application/Importing/IImportEngine.cs`
- Create: `src/Tww3Companion.Application/Importing/ImportEngine.cs`
- Create: `src/Tww3Companion.Application/Importing/CurrentWorkspaceImportSession.cs`
- Modify: `tests/Tww3Companion.Application.Tests/Importing/ImportEngineTests.cs`

**Interfaces:**
- Consumes: `ImportTargetContext.ForCurrentWorkspace(...)`
- Produces: a shared import engine that can preview and apply into the currently open Workspace

- [ ] **Step 1: Write the failing tests**

Add tests that prove current-Workspace import stays in the same open Workspace through preview and applies atomically:

```csharp
[Fact]
public async Task CurrentWorkspace_import_builds_preview_without_changing_the_workspace()
{
    var engine = new TestImportEngine();
    var target = ImportTargetContext.ForCurrentWorkspace("workspace-id-123");

    var preview = await engine.BuildPreviewAsync(target, new[] { "candidate-1" });

    Assert.False(preview.Applied);
}

[Fact]
public async Task CurrentWorkspace_import_applies_atomically_when_confirmed()
{
    var engine = new TestImportEngine();
    var target = ImportTargetContext.ForCurrentWorkspace("workspace-id-123");
    var preview = await engine.BuildPreviewAsync(target, new[] { "candidate-1" });

    var outcome = await engine.ApplyAsync(preview, confirm: true);

    Assert.True(outcome.Applied);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Tww3Companion.Application.Tests --filter "CurrentWorkspace_import_builds_preview_without_changing_the_workspace|CurrentWorkspace_import_applies_atomically_when_confirmed" -v normal`

Expected: fail until the shared import engine exists.

- [ ] **Step 3: Implement the current-Workspace path**

Make the engine:

```csharp
- accept the current Workspace target context;
- build preview from supplied candidates;
- validate before apply;
- preserve additive-only semantics;
- reject unresolved required entries before committing;
- keep the Workspace unchanged when confirmation is false or validation fails.
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Tww3Companion.Application.Tests --filter "CurrentWorkspace_import_builds_preview_without_changing_the_workspace|CurrentWorkspace_import_applies_atomically_when_confirmed" -v normal`

Expected: pass after the current-Workspace path is implemented.

- [ ] **Step 5: Commit**

```bash
git add src/Tww3Companion.Application/Importing tests/Tww3Companion.Application.Tests/Importing
git commit -m "feat: import into current workspace through shared engine"
```

### Task 3: Implement import into new Workspace through the shared engine

**Files:**
- Modify: `src/Tww3Companion.Application/Importing/IImportEngine.cs`
- Modify: `src/Tww3Companion.Application/Importing/ImportEngine.cs`
- Create: `src/Tww3Companion.Application/Importing/NewWorkspaceImportSession.cs`
- Modify: `tests/Tww3Companion.Application.Tests/Importing/ImportEngineTests.cs`

**Interfaces:**
- Consumes: `ImportTargetContext.ForNewWorkspace(...)`
- Produces: a shared import engine that can create a new Workspace and import into it before opening it

- [ ] **Step 1: Write the failing tests**

Add tests that prove the new-Workspace flow validates the destination data and keeps the source Workspace isolated:

```csharp
[Fact]
public async Task NewWorkspace_import_requires_a_display_name_and_destination_path()
{
    var engine = new TestImportEngine();
    var target = ImportTargetContext.ForNewWorkspace("My New Workspace", "C:\\Workspaces\\my-new.tww3c");

    var preview = await engine.BuildPreviewAsync(target, new[] { "candidate-1" });

    Assert.Equal("My New Workspace", preview.Target.DisplayName);
}

[Fact]
public async Task NewWorkspace_import_applies_into_the_new_workspace()
{
    var engine = new TestImportEngine();
    var target = ImportTargetContext.ForNewWorkspace("My New Workspace", "C:\\Workspaces\\my-new.tww3c");
    var preview = await engine.BuildPreviewAsync(target, new[] { "candidate-1" });

    var outcome = await engine.ApplyAsync(preview, confirm: true);

    Assert.True(outcome.Applied);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Tww3Companion.Application.Tests --filter "NewWorkspace_import_requires_a_display_name_and_destination_path|NewWorkspace_import_applies_into_the_new_workspace" -v normal`

Expected: fail until the new-Workspace path exists.

- [ ] **Step 3: Implement the new-Workspace path**

Make the engine:

```csharp
- validate the destination display name and path;
- create a fresh Workspace target context;
- keep the new Workspace isolated from any already-open Workspace;
- apply the confirmed import atomically into the new Workspace;
- roll back the new Workspace import if validation or persistence fails.
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Tww3Companion.Application.Tests --filter "NewWorkspace_import_requires_a_display_name_and_destination_path|NewWorkspace_import_applies_into_the_new_workspace" -v normal`

Expected: pass after the new-Workspace path is implemented.

- [ ] **Step 5: Commit**

```bash
git add src/Tww3Companion.Application/Importing tests/Tww3Companion.Application.Tests/Importing
git commit -m "feat: import into new workspace through shared engine"
```

### Task 4: Wire the Home and Workspace shell actions to the shared engine

**Files:**
- Modify: `src/Tww3Companion.Desktop/ViewModels/ShellViewModel.cs`
- Modify: `src/Tww3Companion.Desktop/Views/HomeView.axaml.cs`
- Modify: `src/Tww3Companion.Desktop/Views/MainWindow.axaml.cs`
- Modify: `tests/Tww3Companion.Desktop.Tests/*` for Home and shell action coverage

**Interfaces:**
- Consumes: the shared import engine from Tasks 1–3
- Produces: Home and Workspace shell actions that call the same import service with different target contexts

- [ ] **Step 1: Write the failing tests**

Add tests that pin the two entry points:

```csharp
[Fact]
public void Home_exposes_import_into_new_workspace()
{
    var shell = ShellViewModel.CreateForTest();

    Assert.Contains("Import into new Workspace", shell.Home.NavigationItems);
}

[Fact]
public void Workspace_shell_exposes_import_into_current_workspace()
{
    var shell = ShellViewModel.CreateForTest();

    Assert.Contains("Import into current Workspace", shell.WorkspaceDestinations);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Tww3Companion.Desktop.Tests --filter "Home_exposes_import_into_new_workspace|Workspace_shell_exposes_import_into_current_workspace" -v normal`

Expected: fail until the shell actions are wired.

- [ ] **Step 3: Wire the UI actions**

Connect:

```csharp
- Home → Import into new Workspace;
- Workspace shell → Import into current Workspace;
- both actions into the same shared import engine.
```

Keep the UI thin: do not duplicate import rules between entry points.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Tww3Companion.Desktop.Tests --filter "Home_exposes_import_into_new_workspace|Workspace_shell_exposes_import_into_current_workspace" -v normal`

Expected: pass after the UI wiring is complete.

- [ ] **Step 5: Commit**

```bash
git add src/Tww3Companion.Desktop tests/Tww3Companion.Desktop.Tests
git commit -m "feat: wire import entry points to shared engine"
```

### Task 5: Verify the full slice with build, tests, and diff check

**Files:**
- None expected unless the prior tasks expose a small fix

**Interfaces:**
- Consumes: the completed import engine and UI wiring
- Produces: a verified branch ready for review

- [ ] **Step 1: Run the full verification commands**

Run:

```powershell
dotnet format Tww3Companion.sln --verify-no-changes
dotnet build Tww3Companion.sln -c Release --no-restore
dotnet test Tww3Companion.sln -c Release --no-build
git diff --check
```

Expected: all commands succeed.

- [ ] **Step 2: Fix any small verification issues**

If formatting or test failures appear, make the smallest code change needed, then rerun the same verification commands.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: complete import core and target-context slice"
```
