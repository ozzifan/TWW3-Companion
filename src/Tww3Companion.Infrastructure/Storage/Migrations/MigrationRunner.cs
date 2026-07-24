using System.Globalization;
using Microsoft.Data.Sqlite;
using Tww3Companion.Application.Abstractions;
using Tww3Companion.Application.Common;
using Tww3Companion.Infrastructure.Storage.Backups;
using Tww3Companion.Infrastructure.Storage.Schema;

namespace Tww3Companion.Infrastructure.Storage.Migrations;

public sealed class MigrationRunner(
    SqliteConnectionFactory connectionFactory,
    WorkspaceBackupService backupService,
    IClock clock,
    IEnumerable<IMigration> migrations)
{
  public async Task<OperationResult<int>> MigrateAsync(string path, int targetVersion, CancellationToken token)
  {
    try
    {
      var (currentVersion, workspaceUuid) = await ReadIdentityAsync(path, token);
      if (targetVersion < currentVersion)
        return Failure("workspace.migration.downgrade", "Workspace schema downgrades are not supported.");
      if (targetVersion > SchemaVersion.Current)
        return Failure("workspace.schema.newer", "The requested schema is newer than this application supports.");
      if (targetVersion == currentVersion) return new OperationResult<int>.Success(currentVersion);

      var chain = SelectChain(currentVersion, targetVersion);
      if (chain is null)
        return Failure("workspace.migration.unsupported", "No complete migration path is available.");

      var backup = await backupService.CreateAsync(path, workspaceUuid, BackupReason.PreMigration, token);
      if (backup is OperationResult<string>.Failure backupFailure)
        return Failure(backupFailure.Error.Code, backupFailure.Error.Message);

      await using var connection = await connectionFactory.OpenAsync(path, token);
      await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(CancellationToken.None);
      try
      {
        foreach (var migration in chain)
        {
          await migration.ApplyAsync(connection, transaction, CancellationToken.None);
          await using var record = connection.CreateCommand();
          record.Transaction = transaction;
          record.CommandText = "INSERT INTO schema_migrations(version, applied_utc) VALUES($version, $utc); UPDATE application_metadata SET schema_version=$version WHERE singleton=1;";
          record.Parameters.AddWithValue("$version", migration.ToVersion);
          record.Parameters.AddWithValue("$utc", clock.UtcNow.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
          await record.ExecuteNonQueryAsync(CancellationToken.None);
        }
        await ValidateBeforeCommitAsync(connection, transaction, targetVersion);
        await transaction.CommitAsync(CancellationToken.None);
      }
      catch
      {
        await transaction.RollbackAsync(CancellationToken.None);
        throw;
      }

      try
      {
        await backupService.CleanupAsync(workspaceUuid, CancellationToken.None);
      }
      catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
      {
        return Failure("workspace.backup.cleanup.failed", "Migration committed, but old managed backups could not be removed.", true);
      }
      return new OperationResult<int>.Success(targetVersion);
    }
    catch (OperationCanceledException)
    {
      return Failure("workspace.migration.cancelled", "Workspace migration was cancelled.");
    }
    catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException or InvalidOperationException)
    {
      return Failure("workspace.migration.failed", "The Workspace migration failed and was rolled back.");
    }
  }

  private IMigration[]? SelectChain(int from, int target)
  {
    var available = migrations.ToArray();
    if (available.GroupBy(m => (m.FromVersion, m.ToVersion)).Any(group => group.Count() > 1)) return null;
    var selected = new List<IMigration>();
    while (from < target)
    {
      var next = available.Where(m => m.FromVersion == from).ToArray();
      if (next.Length != 1 || next[0].ToVersion != from + 1 || next[0].ToVersion > target) return null;
      selected.Add(next[0]); from = next[0].ToVersion;
    }
    return selected.ToArray();
  }

  private async Task<(int Version, string Uuid)> ReadIdentityAsync(string path, CancellationToken token)
  {
    await using var connection = await connectionFactory.OpenAsync(path, token);
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT a.schema_version, w.id FROM application_metadata a CROSS JOIN workspace w WHERE a.singleton=1 AND w.singleton=1;";
    await using var reader = await command.ExecuteReaderAsync(token);
    if (!await reader.ReadAsync(token)) throw new InvalidOperationException("Invalid metadata");
    var result = (reader.GetInt32(0), reader.GetString(1));
    if (await reader.ReadAsync(token)) throw new InvalidOperationException("Invalid metadata");
    return result;
  }

  private static async Task ValidateBeforeCommitAsync(SqliteConnection connection, SqliteTransaction transaction, int target)
  {
    await using (var version = connection.CreateCommand())
    {
      version.Transaction = transaction;
      version.CommandText = "SELECT (SELECT schema_version FROM application_metadata WHERE singleton=1), (SELECT MAX(version) FROM schema_migrations);";
      await using var reader = await version.ExecuteReaderAsync(CancellationToken.None);
      if (!await reader.ReadAsync() || reader.GetInt32(0) != target || reader.GetInt32(1) != target)
        throw new InvalidOperationException("Migration validation failed");
    }

    await WorkspaceSchemaInspector.ValidateAsync(connection, transaction, target, CancellationToken.None);
  }

  private static OperationResult<int>.Failure Failure(string code, string message, bool committed = false) =>
      new(new OperationError(code, message, committed, committed
          ? "Retry managed backup cleanup."
          : "Restore the managed backup or use a supported application version."));
}
