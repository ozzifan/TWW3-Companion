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
            command.CommandText = "SELECT application_id, schema_version FROM application_metadata WHERE singleton=1; SELECT id, display_name, created_utc, modified_utc FROM workspace;";
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token) || reader.GetString(0) != SchemaV1.ApplicationId)
                return Failure("workspace.file.invalid", "The selected SQLite file is not a TWW3 Companion Workspace.");
            var version = reader.GetInt32(1);
            if (version > SchemaVersion.Current) return Failure("workspace.schema.newer", "This Workspace was created by a newer application version.");
            if (version < 1 || await reader.ReadAsync(token)) return Failure("workspace.file.invalid", "Workspace metadata is invalid.");
            if (!await reader.NextResultAsync(token) || !await reader.ReadAsync(token)) return Failure("workspace.identity.invalid", "Workspace identity is missing or invalid.");
            var idText = reader.GetString(0); var nameText = reader.GetString(1); var createdText = reader.GetString(2); var modifiedText = reader.GetString(3);
            if (await reader.ReadAsync(token)) return Failure("workspace.identity.invalid", "Workspace identity is duplicated.");
            if (WorkspaceId.Parse(idText) is not ValidationResult<WorkspaceId>.Success id ||
                WorkspaceName.Create(nameText) is not ValidationResult<WorkspaceName>.Success name ||
                !TryUtc(createdText, out var created) || !TryUtc(modifiedText, out var modified) ||
                Workspace.Create(id.Value, name.Value, created, modified) is not ValidationResult<Workspace>.Success workspace)
                return Failure("workspace.identity.invalid", "Workspace identity is invalid.");
            await reader.DisposeAsync();
            await using var check = connection.CreateCommand();
            check.CommandText = "PRAGMA integrity_check;";
            if ((string?)await check.ExecuteScalarAsync(token) != "ok")
                return Failure("workspace.file.corrupt", "The Workspace database is corrupt.");
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

    private static async Task<bool> HasSqliteHeaderAsync(string path, CancellationToken token)
    {
        var buffer = new byte[16];
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return await stream.ReadAsync(buffer, token) == buffer.Length && buffer.SequenceEqual("SQLite format 3\0"u8.ToArray());
    }

    internal static OperationResult<Workspace>.Failure Failure(string code, string message) =>
        new(new OperationError(code, message, false, "Return Home and choose another Workspace file."));
}
