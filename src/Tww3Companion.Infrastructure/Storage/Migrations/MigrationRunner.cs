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
      version.Transaction = transaction; version.CommandText = "SELECT (SELECT schema_version FROM application_metadata WHERE singleton=1), (SELECT MAX(version) FROM schema_migrations);";
      await using var reader = await version.ExecuteReaderAsync(CancellationToken.None);
      if (!await reader.ReadAsync() || reader.GetInt32(0) != target || reader.GetInt32(1) != target)
        throw new InvalidOperationException("Migration validation failed");
    }
    var tables = new List<string>();
    await using (var structure = connection.CreateCommand())
    {
      structure.Transaction = transaction; structure.CommandText = "SELECT name FROM sqlite_schema WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
      await using var reader = await structure.ExecuteReaderAsync(CancellationToken.None);
      while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
    }
    if (!tables.SequenceEqual(["application_metadata", "schema_migrations", "workspace"]))
      throw new InvalidOperationException("Migration structure is invalid");
    foreach (var (table, expected) in new[]
             {
                     ("application_metadata", new[] { "singleton|INTEGER|0|1", "application_id|TEXT|1|0", "schema_version|INTEGER|1|0" }),
                     ("schema_migrations", new[] { "version|INTEGER|0|1", "applied_utc|TEXT|1|0" }),
                     ("workspace", new[] { "singleton|INTEGER|0|1", "id|TEXT|1|0", "display_name|TEXT|1|0", "created_utc|TEXT|1|0", "modified_utc|TEXT|1|0" })
                 })
      if (!await HasColumnsAsync(connection, transaction, table, expected))
        throw new InvalidOperationException("Migration columns are invalid");
    if (!await HasRequiredConstraintsAsync(connection, transaction))
      throw new InvalidOperationException("Migration constraints are invalid");
    await using (var check = connection.CreateCommand())
    {
      check.Transaction = transaction; check.CommandText = "PRAGMA integrity_check; PRAGMA foreign_key_check;";
      await using var reader = await check.ExecuteReaderAsync(CancellationToken.None);
      if (!await reader.ReadAsync() || reader.GetString(0) != "ok" || await reader.ReadAsync())
        throw new InvalidOperationException("Migration integrity check failed");
      if (!await reader.NextResultAsync() || await reader.ReadAsync())
        throw new InvalidOperationException("Migration foreign keys are invalid");
    }
  }

  private static async Task<bool> HasColumnsAsync(SqliteConnection connection, SqliteTransaction transaction, string table, IReadOnlyList<string> expected)
  {
    await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = $"PRAGMA table_info({table});";
    await using var reader = await command.ExecuteReaderAsync(CancellationToken.None); var actual = new List<string>();
    while (await reader.ReadAsync()) actual.Add($"{reader.GetString(1)}|{reader.GetString(2)}|{reader.GetInt32(3)}|{reader.GetInt32(5)}");
    return actual.SequenceEqual(expected);
  }

  private static async Task<bool> HasRequiredConstraintsAsync(SqliteConnection connection, SqliteTransaction transaction)
  {
    await using var command = connection.CreateCommand(); command.Transaction = transaction;
    command.CommandText = "SELECT name, sql FROM sqlite_schema WHERE type='table' AND name NOT LIKE 'sqlite_%';";
    await using var reader = await command.ExecuteReaderAsync(CancellationToken.None); var sql = new Dictionary<string, string>();
    while (await reader.ReadAsync()) sql[reader.GetString(0)] = reader.GetString(1).Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant();
    return sql["application_metadata"].Contains("check(singleton=1)", StringComparison.Ordinal) &&
           sql["application_metadata"].Contains("check(application_id='com.ozzifan.tww3-companion.workspace')", StringComparison.Ordinal) &&
           sql["application_metadata"].Contains("check(schema_version>=1)", StringComparison.Ordinal) &&
           sql["schema_migrations"].Contains("check(version>=1)", StringComparison.Ordinal) &&
           sql["workspace"].Contains("check(singleton=1)", StringComparison.Ordinal) &&
           sql["workspace"].Contains("idtextnotnullunique", StringComparison.Ordinal) &&
           sql["workspace"].Contains("check(length(trim(display_name))>0)", StringComparison.Ordinal) &&
           sql["workspace"].Contains("check(modified_utc>=created_utc)", StringComparison.Ordinal);
  }

  private static OperationResult<int>.Failure Failure(string code, string message, bool committed = false) =>
      new(new OperationError(code, message, committed, committed
          ? "Retry managed backup cleanup."
          : "Restore the managed backup or use a supported application version."));
}
