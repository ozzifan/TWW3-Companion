using Tww3Companion.Application.Common;
using Tww3Companion.Application.Importing;
using Tww3Companion.Application.Workspaces;
using Tww3Companion.Application.Workspaces.Transfer;
using Tww3Companion.Desktop.Services;
using Tww3Companion.Desktop.ViewModels;
using Xunit;

namespace Tww3Companion.Desktop.Tests.ViewModels;

public sealed class ShellViewModelTests
{
  [Fact]
  public void RestoreFromHome_is_available_without_open_workspace()
  {
    var shell = ShellViewModel.CreateForTest(workspaceTransferCoordinator: new PassiveTransferCoordinator());

    Assert.True(shell.RestoreFromHomeCommand.CanExecute(null));
    Assert.False(shell.BackupWorkspaceCommand.CanExecute(null));
  }

  [Fact]
  public void Backup_is_available_only_for_open_workspace()
  {
    var shell = ShellViewModel.CreateForTest(workspaceTransferCoordinator: new PassiveTransferCoordinator());
    shell.OpenWorkspace();
    shell.SetCurrentWorkspaceForTest("workspace-1", @"C:\Data\workspace.tww3c", "Workspace");

    Assert.True(shell.BackupWorkspaceCommand.CanExecute(null));
    Assert.True(shell.RestoreOpenWorkspaceCommand.CanExecute(null));
  }

  [Fact]
  public void RestoreFromHome_opens_transfer_screen()
  {
    var shell = ShellViewModel.CreateForTest(workspaceTransferCoordinator: new PassiveTransferCoordinator());

    shell.RestoreFromHomeCommand.Execute(null);

    Assert.True(shell.IsWorkspaceTransferVisible);
    Assert.NotNull(shell.WorkspaceTransfer);
    Assert.True(shell.WorkspaceTransfer!.IsNewWorkspaceRestore);
  }

  [Fact]
  public async Task Backup_cancellation_clears_status_without_error()
  {
    var shell = ShellViewModel.CreateForTest(workspaceTransferCoordinator: new PassiveTransferCoordinator());
    shell.OpenWorkspace();
    shell.SetCurrentWorkspaceForTest("workspace-1", @"C:\Data\workspace.tww3c", "Workspace");

    shell.BackupWorkspaceCommand.Execute(null);
    await Task.Delay(50, TestContext.Current.CancellationToken);

    Assert.False(shell.Workspace.HasOperationError);
    Assert.Equal(string.Empty, shell.Workspace.OperationStatusMessage);
  }

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

  [Fact]
  public void Home_import_opens_task_with_new_workspace_launch_context()
  {
    var shell = ShellViewModel.CreateForTest();

    shell.ImportIntoNewWorkspaceCommand.Execute(null);

    Assert.True(shell.IsImportVisible);
    Assert.NotNull(shell.ImportWorkspace);
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

    Assert.True(shell.IsImportVisible);
    Assert.Equal("collection-1", shell.ImportWorkspace!.LaunchContext.SelectedCollectionId);
  }

  [Fact]
  public void Current_import_enabled_on_mod_library_without_selected_collection()
  {
    var shell = ShellViewModel.CreateForTest();
    shell.SetCurrentWorkspaceImportTargetForTest(
        "workspace-1",
        @"C:\Data\workspace.tww3c",
        collectionId: null);

    Assert.True(shell.ImportIntoCurrentWorkspaceCommand.CanExecute(null));
  }

  [Fact]
  public void Current_import_from_mod_library_passes_null_selected_collection()
  {
    var shell = ShellViewModel.CreateForTest();
    shell.SetCurrentWorkspaceImportTargetForTest(
        "workspace-1",
        @"C:\Data\workspace.tww3c",
        collectionId: null);

    shell.ImportIntoCurrentWorkspaceCommand.Execute(null);

    Assert.True(shell.IsImportVisible);
    Assert.Null(shell.ImportWorkspace!.LaunchContext.SelectedCollectionId);
  }

  [Fact]
  public void StartsOnHomeWithImportWorkspaceDestination()
  {
    var subject = new ShellViewModel();

    Assert.Equal(ShellScreen.Home, subject.CurrentScreen);
    Assert.Equal(["Mod Library", "Collections", "Import into current Workspace"], subject.Workspace.WorkspaceDestinations);
    Assert.DoesNotContain(subject.Workspace.WorkspaceDestinations, destination =>
        destination.Contains("Search", StringComparison.OrdinalIgnoreCase)
        || destination.Contains("Profile", StringComparison.OrdinalIgnoreCase)
        || destination.Contains("Health", StringComparison.OrdinalIgnoreCase));
    Assert.Equal("This Workspace contains no Mods or Collections yet. No data has been added.", subject.Workspace.EmptyStateMessage);
  }

  [Fact]
  public void HighContrastOverridesButDoesNotReplaceStoredTheme()
  {
    var subject = new ShellViewModel();

    Assert.Equal(ThemeChoice.System, subject.StoredTheme);
    Assert.Equal(ThemeChoice.System, subject.EffectiveTheme);

    subject.SetTheme(ThemeChoice.Dark);
    subject.SetHighContrast(true);

    Assert.Equal(ThemeChoice.Dark, subject.StoredTheme);
    Assert.Equal(ThemeChoice.HighContrast, subject.EffectiveTheme);

    subject.SetHighContrast(false);
    Assert.Equal(ThemeChoice.Dark, subject.EffectiveTheme);
  }

  [Fact]
  public void UndersizedWorkAreaRequiresCompatibilityDecisionAndRetainsWarningAfterContinue()
  {
    var subject = new ShellViewModel();

    subject.EvaluateWorkArea(1000, 620);

    Assert.Equal(ShellScreen.Compatibility, subject.CurrentScreen);
    Assert.Equal([CompatibilityAction.Exit, CompatibilityAction.ContinueAnyway], subject.CompatibilityActions);

    subject.ContinueAnyway();
    Assert.Equal(ShellScreen.Home, subject.CurrentScreen);
    Assert.True(subject.HasCompatibilityWarning);
  }

  [Fact]
  public async Task Successful_new_workspace_import_completion_sets_active_workspace_and_enters_workspace()
  {
    const string workspaceId = "new-workspace-id";
    const string workspacePath = @"C:\Workspaces\new.tww3c";
    var reader = new RecordingCatalogReader();
    var query = new WorkspaceLibraryQuery(reader);
    var coordinator = new ConfigurableImportCoordinator();
    var shell = ShellViewModel.CreateForTest(
        importCoordinator: coordinator,
        workspaceLibraryQuery: query);

    shell.ImportIntoNewWorkspaceCommand.Execute(null);
    var importTask = shell.ImportWorkspace!;
    importTask.Source.Select(ImportSourceKind.SteamItems);
    importTask.Source.InputText = "123456789";
    await importTask.ContinueFromSourceAsync(TestContext.Current.CancellationToken);
    importTask.Destination.SelectLibraryOnly();
    await importTask.ContinueFromDestinationAsync(TestContext.Current.CancellationToken);
    await importTask.ContinueFromPreviewAsync(TestContext.Current.CancellationToken);

    var appliedTarget = ImportTargetContext.ForCurrentWorkspace(
        workspaceId,
        workspacePath,
        ImportMembershipDestination.ForLibraryOnly());
    coordinator.ConfigureApplyOutcome(new ImportOutcome(appliedTarget, [], Applied: true));

    await importTask.ApplyAsync(TestContext.Current.CancellationToken);

    Assert.Equal(ShellScreen.Workspace, shell.CurrentScreen);
    Assert.False(shell.IsImportVisible);
    Assert.Equal(workspacePath, reader.LastPath);
    Assert.Null(shell.ModLibrary.SelectedCollection);
    Assert.Contains(shell.ModLibrary.Mods, mod => mod.DisplayName == "Persisted Mod");
  }

  [Fact]
  public async Task Successful_import_completion_reloads_library_and_enters_workspace()
  {
    const string workspaceId = "workspace-id-123";
    const string workspacePath = @"C:\Workspaces\current.tww3c";
    const string collectionId = "collection-id-123";
    var reader = new RecordingCatalogReader();
    var query = new WorkspaceLibraryQuery(reader);
    var coordinator = new ConfigurableImportCoordinator();
    var shell = ShellViewModel.CreateForTest(
        importCoordinator: coordinator,
        workspaceLibraryQuery: query);

    shell.SetCurrentWorkspaceImportTargetForTest(workspaceId, workspacePath, collectionId);
    shell.ImportIntoCurrentWorkspaceCommand.Execute(null);
    var importTask = shell.ImportWorkspace!;
    importTask.Source.Select(ImportSourceKind.SteamItems);
    importTask.Source.InputText = "123456789";
    await importTask.ContinueFromSourceAsync(TestContext.Current.CancellationToken);
    importTask.Destination.SelectLibraryOnly();
    await importTask.ContinueFromDestinationAsync(TestContext.Current.CancellationToken);
    await importTask.ContinueFromPreviewAsync(TestContext.Current.CancellationToken);

    var appliedTarget = ImportTargetContext.ForCurrentWorkspace(
        workspaceId,
        workspacePath,
        ImportMembershipDestination.ForLibraryOnly());
    coordinator.ConfigureApplyOutcome(new ImportOutcome(appliedTarget, [], Applied: true));

    await importTask.ApplyAsync(TestContext.Current.CancellationToken);

    Assert.Equal(ShellScreen.Workspace, shell.CurrentScreen);
    Assert.False(shell.IsImportVisible);
    Assert.Equal(workspacePath, reader.LastPath);
    Assert.Contains(shell.ModLibrary.Mods, mod => mod.DisplayName == "Persisted Mod");
  }

  [Fact]
  public async Task Failed_apply_retains_import_task_with_preview()
  {
    const string workspaceId = "workspace-id-123";
    const string workspacePath = @"C:\Workspaces\current.tww3c";
    const string collectionId = "collection-id-123";
    var coordinator = new ConfigurableImportCoordinator
    {
      ApplyException = new InvalidOperationException("Import persistence failed.")
    };
    var shell = ShellViewModel.CreateForTest(importCoordinator: coordinator);

    shell.SetCurrentWorkspaceImportTargetForTest(workspaceId, workspacePath, collectionId);
    shell.ImportIntoCurrentWorkspaceCommand.Execute(null);
    var importTask = shell.ImportWorkspace!;
    importTask.Source.Select(ImportSourceKind.SteamItems);
    importTask.Source.InputText = "123456789";
    await importTask.ContinueFromSourceAsync(TestContext.Current.CancellationToken);
    importTask.Destination.SelectLibraryOnly();
    await importTask.ContinueFromDestinationAsync(TestContext.Current.CancellationToken);
    await importTask.ContinueFromPreviewAsync(TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<InvalidOperationException>(() =>
        importTask.ApplyAsync(TestContext.Current.CancellationToken));

    Assert.True(shell.IsImportVisible);
    Assert.Equal(ImportTaskStage.Preview, importTask.Stage);
    Assert.NotNull(importTask.Preview.Preview);
  }

  [Fact]
  public async Task WorkspaceAndReturnHomeActionsChangeScreen()
  {
    var coordinator = new CompletingWorkspaceDisposalCoordinator();
    var subject = ShellViewModel.CreateForTest(workspaceDisposalCoordinator: coordinator);

    subject.OpenWorkspace();
    Assert.Equal(ShellScreen.Workspace, subject.CurrentScreen);

    subject.ReturnHome();
    await WaitForScreen(subject, ShellScreen.Home);
    Assert.True(coordinator.WasDisposed);
    Assert.Equal(ShellScreen.Home, subject.CurrentScreen);
  }

  private static async Task WaitForScreen(ShellViewModel subject, ShellScreen screen)
  {
    for (var attempt = 0; attempt < 50; attempt++)
    {
      if (subject.CurrentScreen == screen)
      {
        return;
      }

      await Task.Delay(10);
    }

    throw new InvalidOperationException($"The shell did not enter {screen}.");
  }

  private sealed class CompletingWorkspaceDisposalCoordinator : IWorkspaceDisposalCoordinator
  {
    public bool WasDisposed { get; private set; }

    public Task DisposeWorkspaceScopeAsync(CancellationToken cancellationToken)
    {
      WasDisposed = true;
      return Task.CompletedTask;
    }
  }

  private sealed class ConfigurableImportCoordinator : IImportTaskCoordinator
  {
    private ImportOutcome? applyOutcome;

    public Exception? ApplyException { get; init; }

    public void ConfigureApplyOutcome(ImportOutcome outcome) => applyOutcome = outcome;

    public Task<ImportSourceLoadResult> LoadSourceAsync(
        ImportSourceRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ImportSourceLoadResult(
            [new SteamImportCandidate("123456789", "Example Mod")],
            [],
            ["123456789"]));

    public Task<ImportPreview> BuildPreviewAsync(
        ImportTargetContext targetContext,
        IReadOnlyList<object> candidates,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ImportPreview(
            targetContext,
            [ImportCandidate.CreateWithDisplayName("candidate-1", "Example Mod")],
            Applied: false,
            Operations:
            [
                new ImportPreviewOperation(
                    "candidate-1",
                    ImportLibraryAction.Create,
                    ImportMembershipAction.None,
                    [])
            ]));

    public Task<ImportPreview> ResolveAsync(
        ImportPreview preview,
        ImportCandidate resolvedCandidate,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(preview);

    public Task<ImportOutcome> ApplyAsync(
        ImportPreview preview,
        CancellationToken cancellationToken = default)
    {
      if (ApplyException is not null)
      {
        return Task.FromException<ImportOutcome>(ApplyException);
      }

      return Task.FromResult(
          applyOutcome ?? new ImportOutcome(preview.TargetContext, preview.Candidates, Applied: true));
    }
  }

  private sealed class RecordingCatalogReader : IWorkspaceCatalogReader
  {
    public string? LastPath { get; set; }

    public Task<WorkspaceLibrarySnapshot> ReadLibrarySnapshotAsync(
        string workspacePath,
        CancellationToken cancellationToken = default)
    {
      LastPath = workspacePath;
      return Task.FromResult(new WorkspaceLibrarySnapshot(
          [new WorkspaceLibraryMod("mod-1", "Persisted Mod")],
          [],
          []));
    }
  }

  private sealed class PassiveTransferCoordinator : IWorkspaceTransferCoordinator
  {
    public string? LastRestoreDestinationPath => null;

    public Task<OperationResult<string>> BackupAsync(
        string workspacePath,
        string workspaceDisplayName,
        CancellationToken cancellationToken) =>
        Task.FromResult<OperationResult<string>>(
            new OperationResult<string>.Failure(new OperationError(
                "workspace.transfer.cancelled",
                "cancelled",
                false,
                "retry")));

    public Task<OperationResult<InspectedWorkspaceRestore>> InspectRestoreAsync(CancellationToken cancellationToken) =>
        Task.FromResult<OperationResult<InspectedWorkspaceRestore>>(
            new OperationResult<InspectedWorkspaceRestore>.Failure(new OperationError(
                "workspace.transfer.cancelled",
                "cancelled",
                false,
                "retry")));

    public Task<OperationResult<Domain.Workspaces.Workspace>> RestoreNewAsync(
        InspectedWorkspaceRestore inspected,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<OperationResult<Domain.Workspaces.Workspace>> ReplaceOpenAsync(
        InspectedWorkspaceRestore inspected,
        string workspacePath,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();
  }
}
