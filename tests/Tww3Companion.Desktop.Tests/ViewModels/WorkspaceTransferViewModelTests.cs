using Tww3Companion.Application.Common;
using Tww3Companion.Application.Workspaces.Transfer;
using Tww3Companion.Desktop.Services;
using Tww3Companion.Desktop.ViewModels;
using Xunit;

namespace Tww3Companion.Desktop.Tests.ViewModels;

public sealed class WorkspaceTransferViewModelTests
{
  private static readonly DateTimeOffset FixedDate = new(2026, 7, 25, 10, 0, 0, TimeSpan.Zero);

  [Fact]
  public async Task Inspect_displays_summary_counts_and_destination_copy_for_new_workspace()
  {
    var coordinator = new ConfigurableTransferCoordinator
    {
      Inspected = SampleInspected()
    };
    var viewModel = new WorkspaceTransferViewModel(
        WorkspaceRestoreDestination.NewWorkspace,
        coordinator);

    await RunInspect(viewModel);

    Assert.True(viewModel.HasSummary);
    Assert.Equal("Backup Workspace", viewModel.WorkspaceDisplayName);
    Assert.Equal("workspace-export-v1", viewModel.Format);
    Assert.Equal(2, viewModel.ModCount);
    Assert.Equal(1, viewModel.CollectionCount);
    Assert.Equal(3, viewModel.MembershipCount);
    Assert.Contains("creates", viewModel.DestinationActionDescription, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("never merges", viewModel.DestinationActionDescription, StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public async Task Inspect_for_open_workspace_uses_replace_copy()
  {
    var coordinator = new ConfigurableTransferCoordinator { Inspected = SampleInspected() };
    var viewModel = new WorkspaceTransferViewModel(
        WorkspaceRestoreDestination.ReplaceOpenWorkspace,
        coordinator,
        @"C:\Workspaces\live.tww3c",
        "Live Workspace");

    await RunInspect(viewModel);

    Assert.Contains("replaces", viewModel.DestinationActionDescription, StringComparison.OrdinalIgnoreCase);
    Assert.Equal("Live Workspace", viewModel.OpenWorkspaceName);
  }

  [Fact]
  public async Task Cancelled_inspection_clears_busy_state_without_error()
  {
    var coordinator = new ConfigurableTransferCoordinator
    {
      InspectFailure = new OperationError(
          "workspace.transfer.cancelled",
          "The operation was cancelled.",
          false,
          "Try again.")
    };
    var viewModel = new WorkspaceTransferViewModel(
        WorkspaceRestoreDestination.NewWorkspace,
        coordinator);

    await RunInspect(viewModel);

    Assert.False(viewModel.IsBusy);
    Assert.False(viewModel.HasError);
    Assert.False(viewModel.HasSummary);
  }

  [Fact]
  public async Task Restore_success_raises_completed_event_with_workspace_path()
  {
    var coordinator = new ConfigurableTransferCoordinator
    {
      Inspected = SampleInspected(),
      LastRestoreDestinationPath = @"C:\Workspaces\restored.tww3c",
      RestoreSuccess = true
    };
    var viewModel = new WorkspaceTransferViewModel(
        WorkspaceRestoreDestination.NewWorkspace,
        coordinator);
    viewModel.SetInspectedForTest(SampleInspected());
    WorkspaceTransferCompletedEvent? completed = null;
    viewModel.Completed += (_, e) => completed = e;

    viewModel.RestoreCommand.Execute(null);
    await WaitForIdle(viewModel);

    Assert.NotNull(completed);
    Assert.True(completed.Applied);
    Assert.Equal(@"C:\Workspaces\restored.tww3c", completed.WorkspacePath);
  }

  [Fact]
  public async Task Restore_failure_retains_summary()
  {
    var coordinator = new ConfigurableTransferCoordinator
    {
      RestoreFailure = new OperationError(
          "workspace.restore.failed",
          "Restore failed.",
          false,
          "Retry restore.")
    };
    var viewModel = new WorkspaceTransferViewModel(
        WorkspaceRestoreDestination.NewWorkspace,
        coordinator);
    viewModel.SetInspectedForTest(SampleInspected());

    viewModel.RestoreCommand.Execute(null);
    await WaitForIdle(viewModel);

    Assert.True(viewModel.HasSummary);
    Assert.True(viewModel.HasError);
    Assert.Equal("Restore failed. No changes were made.", viewModel.StatusMessage);
  }

  [Fact]
  public void Busy_state_disables_restore_command()
  {
    var viewModel = new WorkspaceTransferViewModel(
        WorkspaceRestoreDestination.NewWorkspace,
        new ConfigurableTransferCoordinator());
    viewModel.SetInspectedForTest(SampleInspected());

    Assert.True(viewModel.RestoreCommand.CanExecute(null));
  }

  private static InspectedWorkspaceRestore SampleInspected() =>
      new(
          @"C:\Backups\backup.json",
          new WorkspaceTransferSnapshot(
              "workspace-export-v1",
              new WorkspaceTransferWorkspace(
                  "11111111-1111-1111-1111-111111111111",
                  "Backup Workspace",
                  FixedDate,
                  FixedDate),
              [],
              [],
              [],
              []),
          new WorkspaceRestoreSummary(
              "11111111-1111-1111-1111-111111111111",
              "Backup Workspace",
              "workspace-export-v1",
              2,
              1,
              3));

  private static async Task RunInspect(WorkspaceTransferViewModel viewModel)
  {
    viewModel.SelectExportCommand.Execute(null);
    await WaitForIdle(viewModel);
  }

  private static async Task WaitForIdle(WorkspaceTransferViewModel viewModel)
  {
    for (var attempt = 0; attempt < 100; attempt++)
    {
      if (!viewModel.IsBusy)
      {
        return;
      }

      await Task.Delay(10, TestContext.Current.CancellationToken);
    }

    throw new InvalidOperationException("The transfer view model did not become idle.");
  }

  private sealed class ConfigurableTransferCoordinator : IWorkspaceTransferCoordinator
  {
    public InspectedWorkspaceRestore? Inspected { get; init; }
    public OperationError? InspectFailure { get; init; }
    public OperationError? RestoreFailure { get; init; }
    public bool RestoreSuccess { get; init; }
    public string? LastRestoreDestinationPath { get; set; }

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

    public Task<OperationResult<InspectedWorkspaceRestore>> InspectRestoreAsync(CancellationToken cancellationToken)
    {
      if (InspectFailure is { } failure)
      {
        return Task.FromResult<OperationResult<InspectedWorkspaceRestore>>(
            new OperationResult<InspectedWorkspaceRestore>.Failure(failure));
      }

      return Task.FromResult<OperationResult<InspectedWorkspaceRestore>>(
          new OperationResult<InspectedWorkspaceRestore>.Success(Inspected ?? SampleInspected()));
    }

    public Task<OperationResult<Domain.Workspaces.Workspace>> RestoreNewAsync(
        InspectedWorkspaceRestore inspected,
        CancellationToken cancellationToken)
    {
      if (RestoreFailure is { } failure)
      {
        return Task.FromResult<OperationResult<Domain.Workspaces.Workspace>>(
            new OperationResult<Domain.Workspaces.Workspace>.Failure(failure));
      }

      LastRestoreDestinationPath ??= @"C:\Workspaces\restored.tww3c";
      return Task.FromResult<OperationResult<Domain.Workspaces.Workspace>>(
          new OperationResult<Domain.Workspaces.Workspace>.Success(CreateWorkspace()));
    }

    public Task<OperationResult<Domain.Workspaces.Workspace>> ReplaceOpenAsync(
        InspectedWorkspaceRestore inspected,
        string workspacePath,
        CancellationToken cancellationToken) =>
        RestoreNewAsync(inspected, cancellationToken);

    private static Domain.Workspaces.Workspace CreateWorkspace()
    {
      var id = Domain.Workspaces.WorkspaceId.Parse("12345678-1234-4abc-8def-1234567890ab");
      var name = Domain.Workspaces.WorkspaceName.Create("Backup Workspace");
      return Domain.Workspaces.Workspace.Create(
          ((Domain.Validation.ValidationResult<Domain.Workspaces.WorkspaceId>.Success)id).Value,
          ((Domain.Validation.ValidationResult<Domain.Workspaces.WorkspaceName>.Success)name).Value,
          FixedDate,
          FixedDate) is Domain.Validation.ValidationResult<Domain.Workspaces.Workspace>.Success workspace
          ? workspace.Value
          : throw new InvalidOperationException();
    }
  }
}
