using Microsoft.Data.Sqlite;
using Tww3Companion.Application.Common;
using Tww3Companion.Application.Workspaces.Transfer;
using Tww3Companion.Domain.Validation;
using Tww3Companion.Domain.Workspaces;
using Tww3Companion.Infrastructure.Paths;
using Tww3Companion.Infrastructure.Storage;
using Tww3Companion.Infrastructure.Storage.Backups;
using Tww3Companion.Infrastructure.Storage.Transfer;
using Tww3Companion.Infrastructure.Tests.Storage.Fixtures;
using Xunit;

namespace Tww3Companion.Infrastructure.Tests.Storage;

public sealed class WorkspaceTransferRoundTripTests
{
  [Fact]
  public async Task Export_restore_and_reexport_produces_identical_json_and_database_values()
  {
    var token = TestContext.Current.CancellationToken;
    using var directory = new TemporaryDirectory();
    var sourcePath = Path.Combine(directory.Path, "source.tww3c");
    var exportPath = Path.Combine(directory.Path, "backup.json");
    var restoredPath = Path.Combine(directory.Path, "restored.tww3c");
    var reexportPath = Path.Combine(directory.Path, "backup-again.json");

    await CreateRoundTripWorkspaceAsync(sourcePath, token);
    var store = CreateStore(directory.Path);
    var export = new ExportWorkspace(store);
    var restore = new RestoreWorkspace(store);
    var inspect = new InspectWorkspaceRestore(store);

    Assert.IsType<OperationResult<string>.Success>(
        await export.ExecuteAsync(sourcePath, exportPath, token));
    var inspected = Assert.IsType<OperationResult<InspectedWorkspaceRestore>.Success>(
        await inspect.ExecuteAsync(exportPath, token));
    Assert.IsType<OperationResult<Workspace>.Success>(
        await restore.RestoreNewAsync(inspected.Value, restoredPath, token));
    Assert.IsType<OperationResult<string>.Success>(
        await export.ExecuteAsync(restoredPath, reexportPath, token));

    var firstJson = await File.ReadAllBytesAsync(exportPath, token);
    var secondJson = await File.ReadAllBytesAsync(reexportPath, token);
    Assert.Equal(firstJson, secondJson);

    await AssertDatabaseValuesEqualAsync(sourcePath, restoredPath, token);
  }

  private static async Task CreateRoundTripWorkspaceAsync(string path, CancellationToken cancellationToken)
  {
    const string workspaceId = "12345678-1234-4abc-8def-1234567890ab";
    const string modAId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
    const string modBId = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb";
    const string collectionAId = "cccccccc-cccc-cccc-cccc-cccccccccccc";
    const string collectionBId = "dddddddd-dddd-dddd-dddd-dddddddddddd";

    var workspace = CreateWorkspace(
        workspaceId,
        "Round Trip Workspace",
        DateTimeOffset.Parse("2026-07-25T10:00:00Z"),
        DateTimeOffset.Parse("2026-07-25T11:00:00Z"));
    await new SqliteWorkspaceStore().CreateAsync(path, workspace, cancellationToken);

    await using var connection = await new SqliteConnectionFactory().OpenAsync(path, cancellationToken);
    await using var command = connection.CreateCommand();
    command.CommandText = """
        INSERT INTO mods (id, display_name) VALUES
          ($modA, 'Alpha'),
          ($modB, 'Beta');
        INSERT INTO source_references (source_type, external_id, mod_id) VALUES
          ('steam-workshop', '1', $modA),
          ('steam-workshop', '2', $modB);
        INSERT INTO collections (id, display_name) VALUES
          ($collectionA, 'Collection A'),
          ($collectionB, 'Collection B');
        INSERT INTO collection_memberships (collection_id, mod_id, position) VALUES
          ($collectionA, $modA, 0),
          ($collectionA, $modB, 2),
          ($collectionB, $modB, 0);
        """;
    command.Parameters.AddWithValue("$modA", modAId);
    command.Parameters.AddWithValue("$modB", modBId);
    command.Parameters.AddWithValue("$collectionA", collectionAId);
    command.Parameters.AddWithValue("$collectionB", collectionBId);
    await command.ExecuteNonQueryAsync(cancellationToken);
  }

  private static SqliteWorkspaceTransferStore CreateStore(string managedRoot)
  {
    var paths = ManagedPaths.ForRoot(ApplicationMode.Installed, managedRoot);
    var connectionFactory = new SqliteConnectionFactory();
    var backupService = new WorkspaceBackupService(
        connectionFactory,
        paths,
        new FixedClock(DateTimeOffset.Parse("2026-07-25T12:00:00Z")));
    return new SqliteWorkspaceTransferStore(connectionFactory, backupService: backupService);
  }

  private static async Task AssertDatabaseValuesEqualAsync(
      string leftPath,
      string rightPath,
      CancellationToken cancellationToken)
  {
    var left = await ReadAuthoritativeTablesAsync(leftPath, cancellationToken);
    var right = await ReadAuthoritativeTablesAsync(rightPath, cancellationToken);
    Assert.Equal(left, right);
  }

  private static async Task<string> ReadAuthoritativeTablesAsync(string path, CancellationToken cancellationToken)
  {
    await using var connection = await new SqliteConnectionFactory().OpenAsync(path, cancellationToken);
    await using var command = connection.CreateCommand();
    command.CommandText = """
        SELECT id, display_name, created_utc, modified_utc FROM workspace WHERE singleton = 1;
        SELECT id, display_name FROM mods ORDER BY id;
        SELECT source_type, external_id, mod_id FROM source_references ORDER BY source_type, external_id;
        SELECT id, display_name FROM collections ORDER BY id;
        SELECT collection_id, mod_id, position FROM collection_memberships ORDER BY collection_id, position, mod_id;
        """;
    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    var rows = new List<string>();
    do
    {
      while (await reader.ReadAsync(cancellationToken))
      {
        var values = new object[reader.FieldCount];
        reader.GetValues(values);
        rows.Add(string.Join("|", values));
      }
    }
    while (await reader.NextResultAsync(cancellationToken));

    return string.Join("\n", rows);
  }

  private static Workspace CreateWorkspace(
      string workspaceId,
      string displayName,
      DateTimeOffset createdUtc,
      DateTimeOffset modifiedUtc)
  {
    var id = WorkspaceId.Parse(workspaceId);
    var name = WorkspaceName.Create(displayName);
    return Workspace.Create(
        ((ValidationResult<WorkspaceId>.Success)id).Value,
        ((ValidationResult<WorkspaceName>.Success)name).Value,
        createdUtc,
        modifiedUtc) is ValidationResult<Workspace>.Success workspace
        ? workspace.Value
        : throw new InvalidOperationException();
  }

  private sealed class TemporaryDirectory : IDisposable
  {
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public TemporaryDirectory() => Directory.CreateDirectory(Path);

    public void Dispose() => Directory.Delete(Path, recursive: true);
  }

  private sealed class FixedClock(DateTimeOffset utcNow) : Application.Abstractions.IClock
  {
    public DateTimeOffset UtcNow => utcNow;
  }
}
