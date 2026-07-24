using System.Globalization;
using Microsoft.Data.Sqlite;
using Tww3Companion.Application.Common;
using Tww3Companion.Domain.Validation;
using Tww3Companion.Domain.Workspaces;
using Tww3Companion.Infrastructure.Storage.Schema;

namespace Tww3Companion.Infrastructure.Storage;

public sealed class WorkspaceFileValidator
{
  private readonly SqliteConnectionFactory connectionFactory;
  private readonly Func<string, Stream> openHeaderStream;

  public WorkspaceFileValidator(
      SqliteConnectionFactory? connectionFactory = null,
      Func<string, Stream>? openHeaderStream = null)
  {
    this.connectionFactory = connectionFactory ?? new();
    this.openHeaderStream = openHeaderStream ?? (path => new FileStream(
        path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
  }

  public async Task<OperationResult<Workspace>> OpenAsync(string path, CancellationToken token)
  {
    try
    {
      if (!File.Exists(path)) return Failure("workspace.file.invalid", "The Workspace file does not exist.");
      var hasHeader = await HasSqliteHeaderAsync(path, token);
      if (!hasHeader) return Failure("workspace.file.invalid", "The selected file is not a Workspace.");
      await using var connection = await connectionFactory.OpenAsync(path, token);
      await using var metadata = connection.CreateCommand();
      metadata.CommandText = """
                SELECT singleton, application_id, schema_version FROM application_metadata;
                SELECT singleton, id, display_name, created_utc, modified_utc FROM workspace;
                """;
      await using var reader = await metadata.ExecuteReaderAsync(token);
      if (!await reader.ReadAsync(token) || reader.GetInt64(0) != 1 || reader.GetString(1) != SchemaV1.ApplicationId)
        return Failure("workspace.file.invalid", "The selected SQLite file is not a TWW3 Companion Workspace.");
      var version = reader.GetInt32(2);
      if (version > SchemaVersion.Current) return Failure("workspace.schema.newer", "This Workspace was created by a newer application version.");
      if (version < 1 || await reader.ReadAsync(token)) return Failure("workspace.file.invalid", "Workspace metadata is invalid.");
      if (!await ValidateMigrationMetadataAsync(connection, version, token))
        return Failure("workspace.file.invalid", "Workspace migration metadata is invalid.");
      if (!await reader.NextResultAsync(token) || !await reader.ReadAsync(token) || reader.GetInt64(0) != 1)
        return Failure("workspace.identity.invalid", "Workspace identity is missing or invalid.");
      var idText = reader.GetString(1); var nameText = reader.GetString(2); var createdText = reader.GetString(3); var modifiedText = reader.GetString(4);
      if (await reader.ReadAsync(token)) return Failure("workspace.identity.invalid", "Workspace identity is duplicated.");
      if (WorkspaceId.Parse(idText) is not ValidationResult<WorkspaceId>.Success id ||
          idText != id.Value.ToString() ||
          WorkspaceName.Create(nameText) is not ValidationResult<WorkspaceName>.Success name ||
          nameText != name.Value.ToString() ||
          !TryUtc(createdText, out var created) || !TryUtc(modifiedText, out var modified) ||
          Workspace.Create(id.Value, name.Value, created, modified) is not ValidationResult<Workspace>.Success workspace)
        return Failure("workspace.identity.invalid", "Workspace identity is invalid.");
      try
      {
        await WorkspaceSchemaInspector.ValidateAsync(connection, null, version, token);
      }
      catch (WorkspaceSchemaStructureException)
      {
        return Failure("workspace.file.invalid", "Workspace structure is invalid.");
      }
      catch (WorkspaceSchemaIntegrityException exception)
      {
        return Failure("workspace.file.corrupt", exception.Message);
      }
      return new OperationResult<Workspace>.Success(workspace.Value);
    }
    catch (UnauthorizedAccessException) { return Failure("workspace.access.denied", "Access to the Workspace was denied."); }
    catch (IOException) { return Failure("workspace.file.locked", "The Workspace file is locked."); }
    catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6) { return Failure("workspace.file.locked", "The Workspace database is locked."); }
    catch (SqliteException exception) when (exception.SqliteErrorCode is 11 or 26) { return Failure("workspace.file.corrupt", "The Workspace database is corrupt."); }
    catch (SqliteException) { return Failure("workspace.file.invalid", "The selected file is not a valid Workspace."); }
  }

  internal async Task<int?> ReadSchemaVersionAsync(string path, CancellationToken token)
  {
    if (!File.Exists(path)) return null;
    if (!await HasSqliteHeaderAsync(path, token)) return null;
    try
    {
      await using var connection = await connectionFactory.OpenAsync(path, token);
      await using var command = connection.CreateCommand();
      command.CommandText = "SELECT schema_version FROM application_metadata WHERE singleton=1;";
      var result = await command.ExecuteScalarAsync(token);
      return result is long value ? (int)value : null;
    }
    catch (SqliteException)
    {
      return null;
    }
  }

  private static async Task<bool> ValidateMigrationMetadataAsync(
      SqliteConnection connection,
      int schemaVersion,
      CancellationToken token)
  {
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT version, applied_utc FROM schema_migrations ORDER BY version;";
    await using var reader = await command.ExecuteReaderAsync(token);
    var seen = 0;
    while (await reader.ReadAsync(token))
    {
      seen++;
      if (reader.GetInt32(0) != seen || !TryUtc(reader.GetString(1), out _))
        return false;
    }

    return seen == schemaVersion;
  }

  private static bool TryUtc(string text, out DateTimeOffset value) =>
      DateTimeOffset.TryParseExact(text, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out value) && value.Offset == TimeSpan.Zero;

  private async Task<bool> HasSqliteHeaderAsync(string path, CancellationToken token)
  {
    var buffer = new byte[16];
    await using var stream = openHeaderStream(path);
    return await stream.ReadAsync(buffer, token) == buffer.Length && buffer.SequenceEqual("SQLite format 3\0"u8.ToArray());
  }

  internal static OperationResult<Workspace>.Failure Failure(string code, string message) =>
      new(new OperationError(code, message, false, "Return Home and choose another Workspace file."));
}
