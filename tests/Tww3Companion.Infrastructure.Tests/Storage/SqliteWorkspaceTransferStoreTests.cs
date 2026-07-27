using Microsoft.Data.Sqlite;
using Tww3Companion.Application.Abstractions;
using Tww3Companion.Application.Common;
using Tww3Companion.Application.Workspaces.Transfer;
using Tww3Companion.Domain.Workspaces;
using Tww3Companion.Infrastructure.Paths;
using Tww3Companion.Infrastructure.Settings;
using Tww3Companion.Infrastructure.Storage;
using Tww3Companion.Infrastructure.Storage.Backups;
using Tww3Companion.Infrastructure.Storage.Transfer;
using Tww3Companion.Infrastructure.Tests.Storage.Fixtures;
using Xunit;

namespace Tww3Companion.Infrastructure.Tests.Storage;

public sealed class SqliteWorkspaceTransferStoreTests
{
  [Fact]
  public async Task ReadSnapshotAsync_OrdersRecordsDeterministicallyRegardlessOfInsertionOrder()
  {
    using var directory = new TemporaryDirectory();
    var path = Path.Combine(directory.Path, "workspace.tww3c");
    await SchemaVersionTwoFixture.CreatePopulatedAsync(path);
    var store = new SqliteWorkspaceTransferStore();

    var snapshot = Assert.IsType<OperationResult<WorkspaceTransferSnapshot>.Success>(
        await store.ReadSnapshotAsync(path, TestContext.Current.CancellationToken));

    Assert.Equal(
        [SchemaVersionTwoFixture.ModAId, SchemaVersionTwoFixture.ModBId],
        snapshot.Value.Mods.Select(mod => mod.Id).ToArray());
    Assert.Equal(["1", "2"], snapshot.Value.SourceReferences.Select(reference => reference.ExternalId).ToArray());
    Assert.Equal([0, 2], snapshot.Value.Memberships.Select(membership => membership.Position).ToArray());
    Assert.DoesNotContain("schema_migrations", Serialize(snapshot.Value));
    Assert.DoesNotContain("application_metadata", Serialize(snapshot.Value));
  }

  [Fact]
  public async Task WriteExportAsync_LeavesExistingDestinationUnchangedOnSerializationFailure()
  {
    using var directory = new TemporaryDirectory();
    var exportPath = Path.Combine(directory.Path, "backup.json");
    await File.WriteAllTextAsync(exportPath, "original", TestContext.Current.CancellationToken);
    var store = new SqliteWorkspaceTransferStore();
    var invalid = new WorkspaceTransferSnapshot(
        "workspace-export-v2",
        new WorkspaceTransferWorkspace("11111111-1111-1111-1111-111111111111", "Name", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
        [],
        [],
        [],
        []);

    var result = await store.WriteExportAsync(invalid, exportPath, TestContext.Current.CancellationToken);

    Assert.IsType<OperationResult<string>.Failure>(result);
    Assert.Equal("original", await File.ReadAllTextAsync(exportPath, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task ReadExportAsync_RoundTripsWrittenExport()
  {
    using var directory = new TemporaryDirectory();
    var workspacePath = Path.Combine(directory.Path, "workspace.tww3c");
    var exportPath = Path.Combine(directory.Path, "backup.json");
    await SchemaVersionTwoFixture.CreatePopulatedAsync(workspacePath);
    var store = new SqliteWorkspaceTransferStore();
    var snapshot = Assert.IsType<OperationResult<WorkspaceTransferSnapshot>.Success>(
        await store.ReadSnapshotAsync(workspacePath, TestContext.Current.CancellationToken));
    Assert.IsType<OperationResult<string>.Success>(
        await store.WriteExportAsync(snapshot.Value, exportPath, TestContext.Current.CancellationToken));
    var restored = Assert.IsType<OperationResult<WorkspaceTransferSnapshot>.Success>(
        await store.ReadExportAsync(exportPath, TestContext.Current.CancellationToken));

    Assert.True(WorkspaceTransferValidation.ContentEquals(snapshot.Value, restored.Value));
  }

  [Fact]
  public async Task RestoreNewAsync_PreservesIdentitiesTimestampsAndMembershipPositions()
  {
    var token = TestContext.Current.CancellationToken;
    using var directory = new TemporaryDirectory();
    var sourcePath = Path.Combine(directory.Path, "source.tww3c");
    var exportPath = Path.Combine(directory.Path, "backup.json");
    var destinationPath = Path.Combine(directory.Path, "restored.tww3c");
    await SchemaVersionTwoFixture.CreatePopulatedAsync(sourcePath);
    var store = new SqliteWorkspaceTransferStore();
    var snapshot = Assert.IsType<OperationResult<WorkspaceTransferSnapshot>.Success>(
        await store.ReadSnapshotAsync(sourcePath, token)).Value;
    Assert.IsType<OperationResult<string>.Success>(
        await store.WriteExportAsync(snapshot, exportPath, token));

    var restored = Assert.IsType<OperationResult<Workspace>.Success>(
        await store.RestoreNewAsync(snapshot, destinationPath, token));

    Assert.Equal(SchemaVersionTwoFixture.WorkspaceUuid, restored.Value.Id.ToString());
    Assert.Equal("Fixture Workspace", restored.Value.Name.ToString());
    Assert.Equal(DateTimeOffset.Parse("2026-07-25T10:00:00Z"), restored.Value.CreatedUtc);
    Assert.Equal(DateTimeOffset.Parse("2026-07-25T11:00:00Z"), restored.Value.ModifiedUtc);

    var roundTrip = Assert.IsType<OperationResult<WorkspaceTransferSnapshot>.Success>(
        await store.ReadSnapshotAsync(destinationPath, token)).Value;
    Assert.True(WorkspaceTransferValidation.ContentEquals(snapshot, roundTrip));
    Assert.Empty(Directory.GetFiles(directory.Path, "*.restore.tmp"));
  }

  [Fact]
  public async Task RestoreNewAsync_WhenDestinationExists_LeavesItUntouched()
  {
    var token = TestContext.Current.CancellationToken;
    using var directory = new TemporaryDirectory();
    var destinationPath = Path.Combine(directory.Path, "existing.tww3c");
    await File.WriteAllTextAsync(destinationPath, "original", token);
    var snapshot = ValidRestoreSnapshot();

    var failure = Assert.IsType<OperationResult<Workspace>.Failure>(
        await new SqliteWorkspaceTransferStore().RestoreNewAsync(snapshot, destinationPath, token));

    Assert.Equal("workspace.file.invalid", failure.Error.Code);
    Assert.Equal("original", await File.ReadAllTextAsync(destinationPath, token));
  }

  [Fact]
  public async Task RestoreNewAsync_WhenCancelled_RemovesOwnedTemporaryFile()
  {
    using var directory = new TemporaryDirectory();
    var destinationPath = Path.Combine(directory.Path, "cancelled.tww3c");
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    var failure = Assert.IsType<OperationResult<Workspace>.Failure>(
        await new SqliteWorkspaceTransferStore()
            .RestoreNewAsync(ValidRestoreSnapshot(), destinationPath, cancellation.Token));

    Assert.Equal("workspace.restore.cancelled", failure.Error.Code);
    Assert.False(File.Exists(destinationPath));
    Assert.Empty(Directory.GetFiles(directory.Path, "*.restore.tmp"));
  }

  [Fact]
  public async Task RestoreNewAsync_WhenRowWriteFails_RemovesOwnedTemporaryFile()
  {
    using var directory = new TemporaryDirectory();
    var destinationPath = Path.Combine(directory.Path, "failed.tww3c");
    var store = new SqliteWorkspaceTransferStore(
        afterPersistStage: stage =>
        {
          if (stage == "mods")
          {
            throw new IOException("seeded failure");
          }
        });

    var failure = Assert.IsType<OperationResult<Workspace>.Failure>(
        await store.RestoreNewAsync(ValidRestoreSnapshot(), destinationPath, TestContext.Current.CancellationToken));

    Assert.Equal("workspace.restore.failed", failure.Error.Code);
    Assert.False(File.Exists(destinationPath));
    Assert.Empty(Directory.GetFiles(directory.Path, "*.restore.tmp"));
  }

  [Fact]
  public async Task ReplaceAsync_WhenReconstructionFails_LeavesDestinationUnchanged()
  {
    var token = TestContext.Current.CancellationToken;
    using var directory = new TemporaryDirectory();
    var destinationPath = Path.Combine(directory.Path, "workspace.tww3c");
    await SchemaVersionTwoFixture.CreatePopulatedAsync(destinationPath);
    var originalBytes = await File.ReadAllBytesAsync(destinationPath, token);
    var snapshot = ValidRestoreSnapshot() with
    {
      Mods = [new("not-a-uuid", "Broken")]
    };
    var store = CreateReplaceStore(directory.Path);

    var failure = Assert.IsType<OperationResult<Workspace>.Failure>(
        await store.ReplaceAsync(snapshot, destinationPath, token));

    Assert.Equal("workspace.transfer.identity.invalid", failure.Error.Code);
    Assert.Equal(originalBytes, await File.ReadAllBytesAsync(destinationPath, token));
  }

  [Fact]
  public async Task ReplaceAsync_CreatesUsablePreRestoreBackupAndReplacesWorkspace()
  {
    var token = TestContext.Current.CancellationToken;
    using var directory = new TemporaryDirectory();
    var sourcePath = Path.Combine(directory.Path, "source.tww3c");
    var destinationPath = Path.Combine(directory.Path, "destination.tww3c");
    await SchemaVersionTwoFixture.CreatePopulatedAsync(sourcePath);
    File.Copy(sourcePath, destinationPath, overwrite: true);
    var store = CreateReplaceStore(directory.Path);
    var snapshot = Assert.IsType<OperationResult<WorkspaceTransferSnapshot>.Success>(
        await store.ReadSnapshotAsync(sourcePath, token)).Value;
    snapshot = snapshot with
    {
      Workspace = snapshot.Workspace with { DisplayName = "Replaced Workspace" }
    };

    var restored = Assert.IsType<OperationResult<Workspace>.Success>(
        await store.ReplaceAsync(snapshot, destinationPath, token));

    Assert.Equal("Replaced Workspace", restored.Value.Name.ToString());
    var backupFolder = Path.Combine(directory.Path, "Backups", SchemaVersionTwoFixture.WorkspaceUuid);
    Assert.Single(Directory.GetFiles(backupFolder, "*.pre-restore.tww3c"));
    await using var backup = await new SqliteConnectionFactory().OpenAsync(
        Directory.GetFiles(backupFolder, "*.pre-restore.tww3c").Single(),
        token);
    await using var command = backup.CreateCommand();
    command.CommandText = "SELECT display_name FROM workspace WHERE singleton = 1;";
    Assert.Equal("Fixture Workspace", (string)(await command.ExecuteScalarAsync(token))!);
  }

  [Fact]
  public async Task ReplaceAsync_WhenPlacementFails_RestoresOriginalDestination()
  {
    var token = TestContext.Current.CancellationToken;
    using var directory = new TemporaryDirectory();
    var sourcePath = Path.Combine(directory.Path, "source.tww3c");
    var destinationPath = Path.Combine(directory.Path, "destination.tww3c");
    await SchemaVersionTwoFixture.CreatePopulatedAsync(sourcePath);
    File.Copy(sourcePath, destinationPath, overwrite: true);
    var originalBytes = await File.ReadAllBytesAsync(destinationPath, token);
    var snapshot = Assert.IsType<OperationResult<WorkspaceTransferSnapshot>.Success>(
        await new SqliteWorkspaceTransferStore().ReadSnapshotAsync(sourcePath, token)).Value;
    var store = new SqliteWorkspaceTransferStore(
        backupService: CreateBackupService(directory.Path),
        fileSystem: new ReplaceFailingFileSystem());

    var failure = Assert.IsType<OperationResult<Workspace>.Failure>(
        await store.ReplaceAsync(snapshot, destinationPath, token));

    Assert.Equal("workspace.restore.failed", failure.Error.Code);
    Assert.Equal(originalBytes, await File.ReadAllBytesAsync(destinationPath, token));
  }

  [Fact]
  public async Task ReplaceAsync_WhenPostPlacementValidationFails_RestoresOriginalDestination()
  {
    var token = TestContext.Current.CancellationToken;
    using var directory = new TemporaryDirectory();
    var sourcePath = Path.Combine(directory.Path, "source.tww3c");
    var destinationPath = Path.Combine(directory.Path, "destination.tww3c");
    await SchemaVersionTwoFixture.CreatePopulatedAsync(sourcePath);
    File.Copy(sourcePath, destinationPath, overwrite: true);
    var originalBytes = await File.ReadAllBytesAsync(destinationPath, token);
    var snapshot = Assert.IsType<OperationResult<WorkspaceTransferSnapshot>.Success>(
        await new SqliteWorkspaceTransferStore().ReadSnapshotAsync(sourcePath, token)).Value;
    var openAttempts = 0;
    var store = new SqliteWorkspaceTransferStore(
        backupService: CreateBackupService(directory.Path),
        openWorkspaceAsync: async (path, cancellationToken) =>
        {
          openAttempts++;
          if (openAttempts == 1 && path == destinationPath)
          {
            return new OperationResult<Workspace>.Failure(new OperationError(
                "workspace.file.invalid",
                "Validation failed.",
                false,
                "Retry."));
          }

          return await new WorkspaceFileValidator().OpenAsync(path, cancellationToken);
        });

    var failure = Assert.IsType<OperationResult<Workspace>.Failure>(
        await store.ReplaceAsync(snapshot, destinationPath, token));

    Assert.Equal("workspace.file.invalid", failure.Error.Code);
    Assert.Equal(originalBytes, await File.ReadAllBytesAsync(destinationPath, token));
  }

  [Fact]
  public async Task ReplaceAsync_WhenRecoveryRestoreFails_ReportsBlockingFailureWithRecoveryPath()
  {
    var token = TestContext.Current.CancellationToken;
    using var directory = new TemporaryDirectory();
    var sourcePath = Path.Combine(directory.Path, "source.tww3c");
    var destinationPath = Path.Combine(directory.Path, "destination.tww3c");
    await SchemaVersionTwoFixture.CreatePopulatedAsync(sourcePath);
    File.Copy(sourcePath, destinationPath, overwrite: true);
    var snapshot = Assert.IsType<OperationResult<WorkspaceTransferSnapshot>.Success>(
        await new SqliteWorkspaceTransferStore().ReadSnapshotAsync(sourcePath, token)).Value;
    var store = new SqliteWorkspaceTransferStore(
        backupService: CreateBackupService(directory.Path),
        fileSystem: new RecoveryRestoreFailingFileSystem());

    var failure = Assert.IsType<OperationResult<Workspace>.Failure>(
        await store.ReplaceAsync(snapshot, destinationPath, token));

    Assert.Equal("workspace.restore.replacement.blocked", failure.Error.Code);
    Assert.True(failure.Error.PersistentChangeCommitted);
    Assert.Contains(".replace.recovery", failure.Error.SafeNextAction);
  }

  [Fact]
  public async Task ReplaceAsync_WhenRecoveryRestoreFails_RetainsRecoveryFileOnDisk()
  {
    var token = TestContext.Current.CancellationToken;
    using var directory = new TemporaryDirectory();
    var sourcePath = Path.Combine(directory.Path, "source.tww3c");
    var destinationPath = Path.Combine(directory.Path, "destination.tww3c");
    await SchemaVersionTwoFixture.CreatePopulatedAsync(sourcePath);
    File.Copy(sourcePath, destinationPath, overwrite: true);
    var snapshot = Assert.IsType<OperationResult<WorkspaceTransferSnapshot>.Success>(
        await new SqliteWorkspaceTransferStore().ReadSnapshotAsync(sourcePath, token)).Value;
    var store = new SqliteWorkspaceTransferStore(
        backupService: CreateBackupService(directory.Path),
        fileSystem: new RecoveryRetainedFileSystem());

    var failure = Assert.IsType<OperationResult<Workspace>.Failure>(
        await store.ReplaceAsync(snapshot, destinationPath, token));

    Assert.Equal("workspace.restore.replacement.blocked", failure.Error.Code);
    var recoveryPath = Assert.Single(Directory.GetFiles(directory.Path, "*.replace.recovery"));
    Assert.True(File.Exists(recoveryPath));
    Assert.Contains(recoveryPath, failure.Error.SafeNextAction, StringComparison.Ordinal);
  }

  private static WorkspaceTransferSnapshot ValidRestoreSnapshot() =>
      new(
          WorkspaceTransferValidation.SupportedFormat,
          new WorkspaceTransferWorkspace(
              SchemaVersionTwoFixture.WorkspaceUuid,
              "Fixture Workspace",
              DateTimeOffset.Parse("2026-07-25T10:00:00Z"),
              DateTimeOffset.Parse("2026-07-25T11:00:00Z")),
          [
            new(SchemaVersionTwoFixture.ModAId, "Alpha"),
            new(SchemaVersionTwoFixture.ModBId, "Beta")
          ],
          [
            new("steam-workshop", "1", SchemaVersionTwoFixture.ModAId),
            new("steam-workshop", "2", SchemaVersionTwoFixture.ModBId)
          ],
          [new(SchemaVersionTwoFixture.CollectionId, "Collection")],
          [
            new(SchemaVersionTwoFixture.CollectionId, SchemaVersionTwoFixture.ModAId, 0),
            new(SchemaVersionTwoFixture.CollectionId, SchemaVersionTwoFixture.ModBId, 2)
          ]);

  private static SqliteWorkspaceTransferStore CreateReplaceStore(string root) =>
      new(backupService: CreateBackupService(root));

  private static WorkspaceBackupService CreateBackupService(string root) =>
      new(new(), ManagedPaths.ForRoot(ApplicationMode.Portable, root), new FixedClock());

  private static string Serialize(WorkspaceTransferSnapshot snapshot) =>
      Assert.IsType<OperationResult<string>.Success>(WorkspaceJsonCodec.Serialize(snapshot)).Value;

  private sealed class FixedClock : IClock
  {
    public DateTimeOffset UtcNow => new(2026, 7, 18, 1, 2, 3, 456, TimeSpan.Zero);
  }

  private sealed class ReplaceFailingFileSystem : IAtomicFileSystem
  {
    public Stream CreateWriteProbe(string directory) => Stream.Null;

    public void MoveWithoutOverwrite(string source, string destination) =>
        File.Move(source, destination, overwrite: false);

    public void ReplaceWithRecovery(string preparedPath, string destinationPath, string recoveryPath)
    {
      File.Move(destinationPath, recoveryPath, overwrite: false);
      try
      {
        throw new IOException("placement failed");
      }
      catch
      {
        File.Move(recoveryPath, destinationPath, overwrite: false);
        throw;
      }
    }

    public Task WriteAllTextAtomicallyAsync(string path, string content, CancellationToken token) =>
        throw new NotSupportedException();
  }

  private sealed class RecoveryRestoreFailingFileSystem : IAtomicFileSystem
  {
    public Stream CreateWriteProbe(string directory) => Stream.Null;

    public void MoveWithoutOverwrite(string source, string destination) =>
        File.Move(source, destination, overwrite: false);

    public void ReplaceWithRecovery(string preparedPath, string destinationPath, string recoveryPath) =>
        throw new WorkspaceReplacementException(recoveryPath);

    public Task WriteAllTextAtomicallyAsync(string path, string content, CancellationToken token) =>
        throw new NotSupportedException();
  }

  private sealed class RecoveryRetainedFileSystem : IAtomicFileSystem
  {
    public Stream CreateWriteProbe(string directory) => Stream.Null;

    public void MoveWithoutOverwrite(string source, string destination) =>
        File.Move(source, destination, overwrite: false);

    public void ReplaceWithRecovery(string preparedPath, string destinationPath, string recoveryPath)
    {
      File.Move(destinationPath, recoveryPath, overwrite: false);
      throw new WorkspaceReplacementException(recoveryPath);
    }

    public Task WriteAllTextAtomicallyAsync(string path, string content, CancellationToken token) =>
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
