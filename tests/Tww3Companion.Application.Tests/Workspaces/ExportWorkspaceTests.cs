using Tww3Companion.Application.Common;
using Tww3Companion.Application.Workspaces.Transfer;
using Xunit;

namespace Tww3Companion.Application.Tests.Workspaces;

public sealed class ExportWorkspaceTests
{
  [Fact]
  public async Task ExecuteAsync_ReadFailure_ReturnsFailureWithoutWriting()
  {
    var store = new FakeTransferStore
    {
      ReadSnapshotResult = new OperationResult<WorkspaceTransferSnapshot>.Failure(new OperationError(
          "workspace.export.failed",
          "Read failed.",
          false,
          "Retry."))
    };
    var exportPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");

    var result = await new ExportWorkspace(store).ExecuteAsync(
        @"C:\Workspaces\source.tww3c",
        exportPath,
        TestContext.Current.CancellationToken);

    Assert.IsType<OperationResult<string>.Failure>(result);
    Assert.Equal(0, store.WriteCalls);
    Assert.False(File.Exists(exportPath));
  }

  [Fact]
  public async Task ExecuteAsync_Success_WritesExport()
  {
    using var directory = new TemporaryDirectory();
    var exportPath = Path.Combine(directory.Path, "backup.json");
    var snapshot = WorkspaceTransferValidationTestsExtensions.ValidSnapshot();
    var store = new FakeTransferStore
    {
      ReadSnapshotResult = new OperationResult<WorkspaceTransferSnapshot>.Success(snapshot),
      WriteExportResult = new OperationResult<string>.Success(exportPath)
    };

    var result = await new ExportWorkspace(store).ExecuteAsync(
        @"C:\Workspaces\source.tww3c",
        exportPath,
        TestContext.Current.CancellationToken);

    Assert.IsType<OperationResult<string>.Success>(result);
    Assert.Equal(1, store.ReadSnapshotCalls);
    Assert.Equal(1, store.WriteCalls);
    Assert.Same(snapshot, store.WrittenSnapshot);
  }

  private sealed class FakeTransferStore : IWorkspaceTransferStore
  {
    public OperationResult<WorkspaceTransferSnapshot> ReadSnapshotResult { get; init; } =
        new OperationResult<WorkspaceTransferSnapshot>.Failure(new OperationError(
            "workspace.export.failed",
            "Not configured.",
            false,
            "Retry."));

    public OperationResult<string> WriteExportResult { get; init; } =
        new OperationResult<string>.Success(string.Empty);

    public int ReadSnapshotCalls { get; private set; }
    public int WriteCalls { get; private set; }
    public WorkspaceTransferSnapshot? WrittenSnapshot { get; private set; }

    public Task<OperationResult<WorkspaceTransferSnapshot>> ReadSnapshotAsync(
        string workspacePath,
        CancellationToken cancellationToken)
    {
      ReadSnapshotCalls++;
      return Task.FromResult(ReadSnapshotResult);
    }

    public Task<OperationResult<WorkspaceTransferSnapshot>> ReadExportAsync(
        string exportPath,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<OperationResult<string>> WriteExportAsync(
        WorkspaceTransferSnapshot snapshot,
        string exportPath,
        CancellationToken cancellationToken)
    {
      WriteCalls++;
      WrittenSnapshot = snapshot;
      return Task.FromResult(WriteExportResult);
    }

    public Task<OperationResult<Domain.Workspaces.Workspace>> RestoreNewAsync(
        WorkspaceTransferSnapshot snapshot,
        string destinationPath,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<OperationResult<Domain.Workspaces.Workspace>> ReplaceAsync(
        WorkspaceTransferSnapshot snapshot,
        string destinationPath,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();
  }

  private sealed class TemporaryDirectory : IDisposable
  {
    public TemporaryDirectory()
    {
      Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose() => Directory.Delete(Path, recursive: true);
  }
}

internal static class WorkspaceTransferValidationTestsExtensions
{
  public static WorkspaceTransferSnapshot ValidSnapshot() =>
      new(
          Format: "workspace-export-v1",
          Workspace: new WorkspaceTransferWorkspace(
              "11111111-1111-1111-1111-111111111111",
              "My Workspace",
              DateTimeOffset.Parse("2026-07-25T10:00:00Z"),
              DateTimeOffset.Parse("2026-07-25T11:00:00Z")),
          Mods: [new("22222222-2222-2222-2222-222222222222", "Mod A")],
          SourceReferences: [new("steam-workshop", "1234567890", "22222222-2222-2222-2222-222222222222")],
          Collections: [new("33333333-3333-3333-3333-333333333333", "Collection A")],
          Memberships: [new("33333333-3333-3333-3333-333333333333", "22222222-2222-2222-2222-222222222222", 0)]);
}
