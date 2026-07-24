using Tww3Companion.Application.Importing;
using Tww3Companion.Desktop.Services;
using Tww3Companion.Desktop.ViewModels;
using Xunit;

namespace Tww3Companion.Desktop.Tests.ViewModels;

public sealed class ShellViewModelTests
{
  [Fact]
  public void ShellViewModel_exposes_a_test_import_service_seam()
  {
    var shell = ShellViewModel.CreateForTest();

    Assert.NotNull(shell.ImportService);
  }

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
    shell.SetCurrentWorkspaceIdForTest("workspace-id-123");

    shell.ImportIntoCurrentWorkspaceCommand.Execute(null);

    Assert.Equal(
        ImportTargetContext.ForCurrentWorkspace("workspace-id-123"),
        importService.LastTargetContext);
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
}
