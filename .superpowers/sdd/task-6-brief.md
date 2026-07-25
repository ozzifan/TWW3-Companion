### Task 6: Add the full-page Avalonia task and wire shell/composition

**Files:**
- Create: `src/Tww3Companion.Desktop/Views/ImportWorkspaceView.axaml`
- Create: `src/Tww3Companion.Desktop/Views/ImportWorkspaceView.axaml.cs`
- Modify: `src/Tww3Companion.Desktop/Views/MainWindow.axaml`
- Modify: `src/Tww3Companion.Desktop/ViewModels/ShellViewModel.cs`
- Modify: `src/Tww3Companion.Desktop/Composition/ApplicationComposition.cs`
- Modify: `src/Tww3Companion.Desktop/App.axaml`
- Modify: `tests/Tww3Companion.Desktop.Tests/ViewModels/ShellViewModelTests.cs`
- Modify: `tests/Tww3Companion.Desktop.Tests/Views/MainWindowLayoutTests.cs`
- Modify: `tests/Tww3Companion.Desktop.Tests/Composition/ApplicationCompositionTests.cs`

**Interfaces:**
- Consumes: Task 5 `ImportWorkspaceViewModel` and Task 4 coordinator/file service
- Produces: user-visible Home/current-Workspace import entry, full staged page, success navigation, and production Steam metadata wiring

- [ ] **Step 1: Write failing shell, composition, and layout tests**

Add tests asserting:

```csharp
[Fact]
public void Home_import_opens_task_with_new_workspace_launch_context()
{
  var shell = ShellViewModel.CreateForTest();

  shell.ImportIntoNewWorkspaceCommand.Execute(null);

  Assert.True(shell.IsImportVisible);
  Assert.True(shell.ImportWorkspace.LaunchContext.IsNewWorkspace);
  Assert.False(shell.ImportWorkspace.Destination.CanChooseExistingCollection);
}

[Fact]
public void Current_import_passes_workspace_and_selected_collection()
{
  var shell = ShellViewModel.CreateForTest();
  shell.SetCurrentWorkspaceImportTargetForTest(
      "workspace-1",
      @"C:\Data\workspace.tww3c",
      "collection-1");

  shell.ImportIntoCurrentWorkspaceCommand.Execute(null);

  Assert.Equal("collection-1", shell.ImportWorkspace.LaunchContext.SelectedCollectionId);
}
```

Layout tests load `ImportWorkspaceView.axaml` and assert four stages, three source actions, full preview table, Needs Attention pane, Back, Continue, Apply, accessible names, and no future roadmap controls.

- [ ] **Step 2: Add an Import screen to shell navigation**

Extend `ShellScreen` with `Import`. `ShellViewModel` owns only:

- current `ImportWorkspaceViewModel`;
- launch context construction;
- `IsImportVisible`;
- enter/leave commands;
- completion handling.

Do not move source text, preview rows, resolutions, or confirmation counts into the shell.

- [ ] **Step 3: Build the full-page Avalonia view**

Use standard controls:

- top step indicator bound to current stage;
- three source-choice buttons;
- multiline TextBox and Choose file;
- destination RadioButtons, ComboBox, and TextBoxes;
- DataGrid or ItemsControl preview with Library and Membership columns;
- filter controls;
- persistent Needs Attention pane;
- immutable confirmation counts;
- Back, Continue, Cancel, and Apply.

At 1024 × 640, place preview and Needs Attention in rows rather than clipping fixed columns. Every button/control has `AutomationProperties.Name`; status TextBlocks use live-region behavior available in the existing Avalonia version.

- [ ] **Step 4: Wire successful completion**

For a new Workspace:

- use `ImportOutcome.TargetContext` to obtain the newly created Workspace identity/path returned by persistence;
- set active Workspace;
- load library;
- enter Workspace screen;
- select created Collection when applicable, otherwise Mod Library.

For current Workspace:

- reload the library snapshot;
- select target existing/new Collection when applicable;
- otherwise select Mod Library.

Failure leaves `ImportWorkspaceViewModel` visible with preview/resolutions intact.

- [ ] **Step 5: Wire production dependencies once**

In `ApplicationComposition`:

```csharp
var steamHttpClient = new HttpClient
{
  BaseAddress = new Uri("https://api.steampowered.com/"),
  Timeout = TimeSpan.FromSeconds(30)
};
var steamMetadataClient = new SteamWebApiMetadataClient(steamHttpClient);
var importEngine = new ImportEngine(catalogStore);
var importFileService = new ImportSourceFileService(() => topLevel);
var importCoordinator = new ImportTaskCoordinator(
    importEngine,
    steamMetadataClient);
```

Pass coordinator and file service into the shell/import ViewModel factory. Extend `ApplicationRuntime.Dispose` to dispose the owned `HttpClient`. Do not create a client per metadata request.

- [ ] **Step 6: Run focused Desktop tests**

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj --filter "FullyQualifiedName~ShellViewModelTests|FullyQualifiedName~MainWindowLayoutTests|FullyQualifiedName~ApplicationCompositionTests|FullyQualifiedName~Import" -v minimal
```

Expected: all import task, layout, composition, reload, and navigation tests pass.

- [ ] **Step 7: Review and commit Task 6**

```powershell
git diff --check
git add src/Tww3Companion.Desktop tests/Tww3Companion.Desktop.Tests
git commit -m "feat: add complete import workspace UI"
```

Review must confirm the full preview remains visible during resolution and no runtime test hook was added.

---
