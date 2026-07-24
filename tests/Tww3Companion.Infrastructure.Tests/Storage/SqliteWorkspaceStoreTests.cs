using Microsoft.Data.Sqlite;
using Tww3Companion.Application.Abstractions;
using Tww3Companion.Application.Common;
using Tww3Companion.Domain.Validation;
using Tww3Companion.Domain.Workspaces;
using Tww3Companion.Infrastructure.Paths;
using Tww3Companion.Infrastructure.Storage;
using Tww3Companion.Infrastructure.Storage.Backups;
using Tww3Companion.Infrastructure.Storage.Migrations;
using Tww3Companion.Infrastructure.Tests.Storage.Fixtures;
using Xunit;

namespace Tww3Companion.Infrastructure.Tests.Storage;

public sealed class SqliteWorkspaceStoreTests
{
  [Fact]
  public async Task CreateAndOpen_RoundTripsWorkspaceAndCreatesSchemaV2Tables()
  {
    var token = TestContext.Current.CancellationToken;
    using var directory = new TemporaryDirectory();
    var path = Path.Combine(directory.Path, "campaign.tww3c");
    var workspace = CreateWorkspace("My Campaign");
    var store = new SqliteWorkspaceStore();

    var created = Assert.IsType<OperationResult<Workspace>.Success>(
        await store.CreateAsync(path, workspace, token));
    Assert.Equal(workspace, created.Value);
    Assert.Equal(workspace, Assert.IsType<OperationResult<Workspace>.Success>(
        await store.OpenAsync(path, token)).Value);

    Assert.Equal(2L, await ScalarAsync(path, "SELECT schema_version FROM application_metadata"));
    Assert.Equal(
        ["application_metadata", "collection_memberships", "collections",
         "mods", "schema_migrations", "source_references", "workspace"],
        await ReadTablesAsync(path));
  }

  [Fact]
  public async Task Open_MigratesSchemaV1AndCreatesPreMigrationBackup()
  {
    using var directory = new TemporaryDirectory();
    var path = Path.Combine(directory.Path, "v1.tww3c");
    await SchemaVersionOneFixture.CreateAsync(path);
    var store = new SqliteWorkspaceStore(migrationRunner: CreateRunner(directory.Path, new MigrateV1ToV2()));

    var result = Assert.IsType<OperationResult<Workspace>.Success>(
        await store.OpenAsync(path, TestContext.Current.CancellationToken));

    Assert.Equal(SchemaVersionOneFixture.WorkspaceId, result.Value.Id.ToString());
    Assert.Equal(
        2L,
        await ScalarAsync(
            path,
            "SELECT schema_version FROM application_metadata"));
    Assert.Single(Directory.GetFiles(
        Path.Combine(
            directory.Path,
            "Backups",
            SchemaVersionOneFixture.WorkspaceId)));
  }

  [Fact]
  public async Task Create_WhenDestinationExists_LeavesItUntouched()
  {
    var token = TestContext.Current.CancellationToken;
    using var directory = new TemporaryDirectory();
    var path = Path.Combine(directory.Path, "existing.tww3c");
    await File.WriteAllTextAsync(path, "original", token);

    var result = Assert.IsType<OperationResult<Workspace>.Failure>(
        await new SqliteWorkspaceStore().CreateAsync(path, CreateWorkspace("Name"), token));

    Assert.Equal("workspace.file.invalid", result.Error.Code);
    Assert.Equal("original", await File.ReadAllTextAsync(path, token));
    Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
  }

  [Fact]
  public async Task Create_WhenCancelled_RemovesOwnedTemporaryFile()
  {
    using var directory = new TemporaryDirectory();
    var path = Path.Combine(directory.Path, "cancelled.tww3c");
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
        new SqliteWorkspaceStore().CreateAsync(path, CreateWorkspace("Name"), cancellation.Token));

    Assert.False(File.Exists(path));
    Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
  }

  [Fact]
  public async Task Create_WhenMoveAndCleanupFail_ReturnsPrimaryTypedFailure()
  {
    using var directory = new TemporaryDirectory();
    var path = Path.Combine(directory.Path, "failed.tww3c");
    var store = new SqliteWorkspaceStore(
        fileSystem: new MoveFailingFileSystem(),
        deleteOwnedFile: _ => throw new UnauthorizedAccessException("cleanup denied"));

    var failure = Assert.IsType<OperationResult<Workspace>.Failure>(
        await store.CreateAsync(path, CreateWorkspace("Name"), TestContext.Current.CancellationToken));

    Assert.Equal("workspace.file.invalid", failure.Error.Code);
    Assert.False(failure.Error.PersistentChangeCommitted);
  }

  private static MigrationRunner CreateRunner(string root, params IMigration[] migrations)
  {
    var clock = new FixedClock();
    var paths = ManagedPaths.ForRoot(ApplicationMode.Portable, root);
    return new MigrationRunner(new(), new WorkspaceBackupService(new(), paths, clock), clock, migrations);
  }

  private static async Task<long> ScalarAsync(string path, string sql)
  {
    await using var connection = await new SqliteConnectionFactory().OpenAsync(path, CancellationToken.None);
    await using var command = connection.CreateCommand();
    command.CommandText = sql;
    return (long)(await command.ExecuteScalarAsync(CancellationToken.None))!;
  }

  private static async Task<string[]> ReadTablesAsync(string path)
  {
    await using var connection = await new SqliteConnectionFactory().OpenAsync(path, CancellationToken.None);
    await using var command = connection.CreateCommand();
    command.CommandText =
        "SELECT name FROM sqlite_schema WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
    await using var reader = await command.ExecuteReaderAsync(CancellationToken.None);
    var tables = new List<string>();
    while (await reader.ReadAsync(CancellationToken.None))
      tables.Add(reader.GetString(0));
    return tables.ToArray();
  }

  private sealed class FixedClock : IClock
  {
    public DateTimeOffset UtcNow => new(2026, 7, 18, 1, 2, 3, 456, TimeSpan.Zero);
  }

  private static Workspace CreateWorkspace(string name)
  {
    var id = Assert.IsType<ValidationResult<WorkspaceId>.Success>(WorkspaceId.Parse("12345678-1234-4abc-8def-1234567890ab")).Value;
    var workspaceName = Assert.IsType<ValidationResult<WorkspaceName>.Success>(WorkspaceName.Create(name)).Value;
    return Assert.IsType<ValidationResult<Workspace>.Success>(Workspace.Create(
        id, workspaceName,
        new DateTimeOffset(2026, 7, 18, 1, 2, 3, TimeSpan.Zero),
        new DateTimeOffset(2026, 7, 18, 4, 5, 6, TimeSpan.Zero))).Value;
  }
}

internal sealed class MoveFailingFileSystem : Tww3Companion.Infrastructure.Settings.IAtomicFileSystem
{
  public Stream CreateWriteProbe(string directory) => throw new NotSupportedException();
  public void MoveWithoutOverwrite(string source, string destination) => throw new IOException("move failed");
  public Task WriteAllTextAtomicallyAsync(string path, string content, CancellationToken token) => throw new NotSupportedException();
}

internal sealed class TemporaryDirectory : IDisposable
{
  public TemporaryDirectory() { Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path); }
  public string Path { get; }
  public void Dispose() => Directory.Delete(Path, true);
}
