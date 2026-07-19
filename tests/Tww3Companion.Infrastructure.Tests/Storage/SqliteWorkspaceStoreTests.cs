using Microsoft.Data.Sqlite;
using Tww3Companion.Application.Common;
using Tww3Companion.Domain.Validation;
using Tww3Companion.Domain.Workspaces;
using Tww3Companion.Infrastructure.Storage;
using Xunit;

namespace Tww3Companion.Infrastructure.Tests.Storage;

public sealed class SqliteWorkspaceStoreTests
{
  [Fact]
  public async Task CreateAndOpen_RoundTripsWorkspaceAndCreatesOnlyVersionOneTables()
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

    await using var connection = await new SqliteConnectionFactory().OpenAsync(path, token);
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT name FROM sqlite_schema WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
    await using var reader = await command.ExecuteReaderAsync(token);
    var tables = new List<string>();
    while (await reader.ReadAsync(token)) tables.Add(reader.GetString(0));
    Assert.Equal(["application_metadata", "schema_migrations", "workspace"], tables);
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
