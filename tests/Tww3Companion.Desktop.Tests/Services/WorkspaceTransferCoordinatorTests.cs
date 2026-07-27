using Tww3Companion.Application.Abstractions;
using Tww3Companion.Application.Common;
using Tww3Companion.Application.Workspaces.Transfer;
using Tww3Companion.Desktop.Services;
using Tww3Companion.Domain.Validation;
using Tww3Companion.Domain.Workspaces;
using Xunit;

namespace Tww3Companion.Desktop.Tests.Services;

public sealed class WorkspaceTransferCoordinatorTests
{
  private static readonly DateTimeOffset FixedDate = new(2026, 7, 25, 10, 0, 0, TimeSpan.Zero);

  [Fact]
  public async Task BackupAsync_UsesSanitizedSuggestedFileNameFromClock()
  {
    var dialog = new RecordingWorkspaceDialogService
    {
      BackupPath = @"C:\Backups\My-Workspace-2026-07-25.json"
    };
    var store = new FakeTransferStore();
    var coordinator = CreateCoordinator(dialog, store);

    var result = await coordinator.BackupAsync(
        @"C:\Workspaces\live.tww3c",
        "My/Workspace",
        TestContext.Current.CancellationToken);

    Assert.IsType<OperationResult<string>.Success>(result);
    Assert.Equal("My-Workspace-2026-07-25.json", dialog.LastSuggestedBackupFileName);
    Assert.Equal(@"C:\Workspaces\live.tww3c", store.LastSnapshotPath);
    Assert.Equal(@"C:\Backups\My-Workspace-2026-07-25.json", store.LastExportPath);
  }

  [Fact]
  public async Task BackupAsync_WhenCancelled_ReturnsTypedCancellation()
  {
    var coordinator = CreateCoordinator(new RecordingWorkspaceDialogService(), new FakeTransferStore());

    var result = await coordinator.BackupAsync(
        @"C:\Workspaces\live.tww3c",
        "Workspace",
        TestContext.Current.CancellationToken);

    var failure = Assert.IsType<OperationResult<string>.Failure>(result);
    Assert.Equal("workspace.transfer.cancelled", failure.Error.Code);
  }

  [Fact]
  public async Task InspectRestoreAsync_WhenCancelled_ReturnsTypedCancellation()
  {
    var coordinator = CreateCoordinator(new RecordingWorkspaceDialogService(), new FakeTransferStore());

    var result = await coordinator.InspectRestoreAsync(TestContext.Current.CancellationToken);

    var failure = Assert.IsType<OperationResult<InspectedWorkspaceRestore>.Failure>(result);
    Assert.Equal("workspace.transfer.cancelled", failure.Error.Code);
  }

  [Fact]
  public async Task RestoreNewAsync_RecordsDestinationPath()
  {
    var inspected = SampleInspected();
    var dialog = new RecordingWorkspaceDialogService
    {
      RestoredWorkspacePath = @"C:\Workspaces\restored.tww3c"
    };
    var store = new FakeTransferStore();
    var coordinator = CreateCoordinator(dialog, store);

    var result = await coordinator.RestoreNewAsync(inspected, TestContext.Current.CancellationToken);

    Assert.IsType<OperationResult<Workspace>.Success>(result);
    Assert.Equal(@"C:\Workspaces\restored.tww3c", coordinator.LastRestoreDestinationPath);
    Assert.Equal(@"C:\Workspaces\restored.tww3c", store.LastRestoreDestinationPath);
  }

  [Fact]
  public async Task ReplaceOpenAsync_RequiresConfirmationBeforeReplace()
  {
    var inspected = SampleInspected();
    var dialog = new RecordingWorkspaceDialogService { ConfirmReplacement = false };
    var store = new FakeTransferStore();
    var coordinator = CreateCoordinator(dialog, store);

    var result = await coordinator.ReplaceOpenAsync(
        inspected,
        @"C:\Workspaces\live.tww3c",
        TestContext.Current.CancellationToken);

    var failure = Assert.IsType<OperationResult<Workspace>.Failure>(result);
    Assert.Equal("workspace.restore.unconfirmed", failure.Error.Code);
    Assert.Null(store.LastReplaceDestinationPath);
  }

  [Theory]
  [InlineData("  ", "Workspace")]
  [InlineData("My/Workspace:", "My-Workspace-")]
  public void WorkspaceFileName_SanitizesInvalidCharacters(string input, string expectedPrefix)
  {
    var sanitized = WorkspaceFileName.Sanitize(input);
    Assert.StartsWith(expectedPrefix, sanitized);
    Assert.DoesNotContain('/', sanitized);
  }

  private static WorkspaceTransferCoordinator CreateCoordinator(
      RecordingWorkspaceDialogService dialog,
      FakeTransferStore store) =>
      new(
          new ExportWorkspace(store),
          new InspectWorkspaceRestore(store),
          new RestoreWorkspace(store),
          dialog,
          new FixedClock(FixedDate),
          @"C:\Workspaces");

  private static InspectedWorkspaceRestore SampleInspected() =>
      new(
          @"C:\Backups\backup.json",
          SampleSnapshot(),
          new WorkspaceRestoreSummary(
              "11111111-1111-1111-1111-111111111111",
              "Backup Workspace",
              "workspace-export-v1",
              2,
              1,
              3));

  private static WorkspaceTransferSnapshot SampleSnapshot() =>
      new(
          "workspace-export-v1",
          new WorkspaceTransferWorkspace(
              "11111111-1111-1111-1111-111111111111",
              "Backup Workspace",
              FixedDate,
              FixedDate),
          [],
          [],
          [],
          []);

  private static Workspace CreateWorkspace()
  {
    var id = WorkspaceId.Parse("12345678-1234-4abc-8def-1234567890ab");
    var name = WorkspaceName.Create("Backup Workspace");
    return Workspace.Create(
        ((ValidationResult<WorkspaceId>.Success)id).Value,
        ((ValidationResult<WorkspaceName>.Success)name).Value,
        FixedDate,
        FixedDate) is ValidationResult<Workspace>.Success workspace
        ? workspace.Value
        : throw new InvalidOperationException();
  }

  private sealed class FixedClock(DateTimeOffset utcNow) : IClock
  {
    public DateTimeOffset UtcNow => utcNow;
  }

  private sealed class FakeTransferStore : IWorkspaceTransferStore
  {
    public string? LastSnapshotPath { get; private set; }
    public string? LastExportPath { get; private set; }
    public string? LastRestoreDestinationPath { get; private set; }
    public string? LastReplaceDestinationPath { get; private set; }

    public Task<OperationResult<WorkspaceTransferSnapshot>> ReadSnapshotAsync(
        string workspacePath,
        CancellationToken cancellationToken)
    {
      LastSnapshotPath = workspacePath;
      return Task.FromResult<OperationResult<WorkspaceTransferSnapshot>>(
          new OperationResult<WorkspaceTransferSnapshot>.Success(SampleSnapshot()));
    }

    public Task<OperationResult<WorkspaceTransferSnapshot>> ReadExportAsync(
        string exportPath,
        CancellationToken cancellationToken) =>
        Task.FromResult<OperationResult<WorkspaceTransferSnapshot>>(
            new OperationResult<WorkspaceTransferSnapshot>.Success(SampleSnapshot()));

    public Task<OperationResult<string>> WriteExportAsync(
        WorkspaceTransferSnapshot snapshot,
        string exportPath,
        CancellationToken cancellationToken)
    {
      LastExportPath = exportPath;
      return Task.FromResult<OperationResult<string>>(new OperationResult<string>.Success(exportPath));
    }

    public Task<OperationResult<Workspace>> RestoreNewAsync(
        WorkspaceTransferSnapshot snapshot,
        string destinationPath,
        CancellationToken cancellationToken)
    {
      LastRestoreDestinationPath = destinationPath;
      return Task.FromResult<OperationResult<Workspace>>(
          new OperationResult<Workspace>.Success(CreateWorkspace()));
    }

    public Task<OperationResult<Workspace>> ReplaceAsync(
        WorkspaceTransferSnapshot snapshot,
        string destinationPath,
        CancellationToken cancellationToken)
    {
      LastReplaceDestinationPath = destinationPath;
      return Task.FromResult<OperationResult<Workspace>>(
          new OperationResult<Workspace>.Success(CreateWorkspace()));
    }
  }

  private sealed class RecordingWorkspaceDialogService : IWorkspaceDialogService
  {
    public string? BackupPath { get; init; }
    public string? RestoredWorkspacePath { get; init; }
    public bool ConfirmReplacement { get; init; } = true;
    public string? LastSuggestedBackupFileName { get; private set; }

    public Task<string?> PromptForCreateDisplayNameAsync(CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);

    public Task<string?> PromptForOpenPathAsync(CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);

    public Task<string?> PromptForBackupPathAsync(string suggestedFileName, CancellationToken cancellationToken)
    {
      LastSuggestedBackupFileName = suggestedFileName;
      return Task.FromResult(BackupPath);
    }

    public Task<string?> PromptForRestoreJsonPathAsync(CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);

    public Task<string?> PromptForRestoredWorkspacePathAsync(string suggestedFileName, CancellationToken cancellationToken) =>
        Task.FromResult(RestoredWorkspacePath);

    public Task<bool> ConfirmWorkspaceReplacementAsync(
        string currentWorkspaceName,
        WorkspaceRestoreSummary source,
        CancellationToken cancellationToken) =>
        Task.FromResult(ConfirmReplacement);
  }
}
