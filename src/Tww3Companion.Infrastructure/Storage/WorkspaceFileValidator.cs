using System.Globalization;
using Microsoft.Data.Sqlite;
using Tww3Companion.Application.Common;
using Tww3Companion.Domain.Validation;
using Tww3Companion.Domain.Workspaces;
using Tww3Companion.Infrastructure.Storage.Schema;

namespace Tww3Companion.Infrastructure.Storage;

public sealed class WorkspaceFileValidator(SqliteConnectionFactory? connectionFactory = null)
{
    private readonly SqliteConnectionFactory connectionFactory = connectionFactory ?? new();

    public async Task<OperationResult<Workspace>> OpenAsync(string path, CancellationToken token)
    {
        try
        {
            if (!File.Exists(path)) return Failure("workspace.file.invalid", "The Workspace file does not exist.");
            var hasHeader = await HasSqliteHeaderAsync(path, token);
            if (!hasHeader) return Failure("workspace.file.invalid", "The selected file is not a Workspace.");
            await using var connection = await connectionFactory.OpenAsync(path, token);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT application_id, schema_version FROM application_metadata;
                SELECT version, applied_utc FROM schema_migrations;
                SELECT id, display_name, created_utc, modified_utc FROM workspace;
                SELECT name FROM sqlite_schema WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;
                """;
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token) || reader.GetString(0) != SchemaV1.ApplicationId)
                return Failure("workspace.file.invalid", "The selected SQLite file is not a TWW3 Companion Workspace.");
            var version = reader.GetInt32(1);
            if (version > SchemaVersion.Current) return Failure("workspace.schema.newer", "This Workspace was created by a newer application version.");
            if (version < 1 || await reader.ReadAsync(token)) return Failure("workspace.file.invalid", "Workspace metadata is invalid.");
            if (!await reader.NextResultAsync(token) || !await reader.ReadAsync(token) || reader.GetInt32(0) != 1 ||
                !TryUtc(reader.GetString(1), out _) || await reader.ReadAsync(token))
                return Failure("workspace.file.invalid", "Workspace migration metadata is invalid.");
            if (!await reader.NextResultAsync(token) || !await reader.ReadAsync(token))
                return Failure("workspace.identity.invalid", "Workspace identity is missing or invalid.");
            var idText = reader.GetString(0); var nameText = reader.GetString(1); var createdText = reader.GetString(2); var modifiedText = reader.GetString(3);
            if (await reader.ReadAsync(token)) return Failure("workspace.identity.invalid", "Workspace identity is duplicated.");
            if (WorkspaceId.Parse(idText) is not ValidationResult<WorkspaceId>.Success id ||
                idText != id.Value.ToString() ||
                WorkspaceName.Create(nameText) is not ValidationResult<WorkspaceName>.Success name ||
                nameText != name.Value.ToString() ||
                !TryUtc(createdText, out var created) || !TryUtc(modifiedText, out var modified) ||
                Workspace.Create(id.Value, name.Value, created, modified) is not ValidationResult<Workspace>.Success workspace)
                return Failure("workspace.identity.invalid", "Workspace identity is invalid.");
            if (!await reader.NextResultAsync(token)) return Failure("workspace.file.invalid", "Workspace structure is invalid.");
            var tables = new List<string>();
            while (await reader.ReadAsync(token)) tables.Add(reader.GetString(0));
            if (!tables.SequenceEqual(["application_metadata", "schema_migrations", "workspace"]))
                return Failure("workspace.file.invalid", "Workspace structure is invalid.");
            await reader.DisposeAsync();
            if (!await HasColumnsAsync(connection, "application_metadata",
                    ["singleton|INTEGER|0|1", "application_id|TEXT|1|0", "schema_version|INTEGER|1|0"], token) ||
                !await HasColumnsAsync(connection, "schema_migrations",
                    ["version|INTEGER|0|1", "applied_utc|TEXT|1|0"], token) ||
                !await HasColumnsAsync(connection, "workspace",
                    ["singleton|INTEGER|0|1", "id|TEXT|1|0", "display_name|TEXT|1|0", "created_utc|TEXT|1|0", "modified_utc|TEXT|1|0"], token))
                return Failure("workspace.file.invalid", "Workspace structure is invalid.");
            await using var check = connection.CreateCommand();
            check.CommandText = "PRAGMA integrity_check; PRAGMA foreign_key_check;";
            await using var checkReader = await check.ExecuteReaderAsync(token);
            if (!await checkReader.ReadAsync(token) || checkReader.GetString(0) != "ok" || await checkReader.ReadAsync(token))
                return Failure("workspace.file.corrupt", "The Workspace database is corrupt.");
            if (!await checkReader.NextResultAsync(token) || await checkReader.ReadAsync(token))
                return Failure("workspace.file.corrupt", "The Workspace database has invalid relationships.");
            return new OperationResult<Workspace>.Success(workspace.Value);
        }
        catch (UnauthorizedAccessException) { return Failure("workspace.access.denied", "Access to the Workspace was denied."); }
        catch (IOException) { return Failure("workspace.file.locked", "The Workspace file is locked."); }
        catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6) { return Failure("workspace.file.locked", "The Workspace database is locked."); }
        catch (SqliteException exception) when (exception.SqliteErrorCode is 11 or 26) { return Failure("workspace.file.corrupt", "The Workspace database is corrupt."); }
        catch (SqliteException) { return Failure("workspace.file.invalid", "The selected file is not a valid Workspace."); }
    }

    private static bool TryUtc(string text, out DateTimeOffset value) =>
        DateTimeOffset.TryParseExact(text, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out value) && value.Offset == TimeSpan.Zero;

    private static async Task<bool> HasColumnsAsync(
        SqliteConnection connection,
        string table,
        IReadOnlyList<string> expected,
        CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table});";
        await using var reader = await command.ExecuteReaderAsync(token);
        var actual = new List<string>();
        while (await reader.ReadAsync(token))
            actual.Add($"{reader.GetString(1)}|{reader.GetString(2)}|{reader.GetInt32(3)}|{reader.GetInt32(5)}");
        return actual.SequenceEqual(expected);
    }

    private static async Task<bool> HasSqliteHeaderAsync(string path, CancellationToken token)
    {
        var buffer = new byte[16];
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return await stream.ReadAsync(buffer, token) == buffer.Length && buffer.SequenceEqual("SQLite format 3\0"u8.ToArray());
    }

    internal static OperationResult<Workspace>.Failure Failure(string code, string message) =>
        new(new OperationError(code, message, false, "Return Home and choose another Workspace file."));
}
