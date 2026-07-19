using System.Globalization;
using Microsoft.Data.Sqlite;
using Tww3Companion.Domain.Workspaces;

namespace Tww3Companion.Infrastructure.Storage.Schema;

internal static class SchemaV1
{
  public const string ApplicationId = "com.ozzifan.tww3-companion.workspace";

  public static async Task CreateAsync(SqliteConnection connection, Workspace workspace, CancellationToken token)
  {
    await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(token);
    await using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = """
            CREATE TABLE application_metadata (
                singleton INTEGER PRIMARY KEY CHECK (singleton = 1),
                application_id TEXT NOT NULL CHECK (application_id = 'com.ozzifan.tww3-companion.workspace'),
                schema_version INTEGER NOT NULL CHECK (schema_version >= 1)
            );
            CREATE TABLE schema_migrations (
                version INTEGER PRIMARY KEY CHECK (version >= 1),
                applied_utc TEXT NOT NULL
            );
            CREATE TABLE workspace (
                singleton INTEGER PRIMARY KEY CHECK (singleton = 1),
                id TEXT NOT NULL UNIQUE,
                display_name TEXT NOT NULL CHECK (length(trim(display_name)) > 0),
                created_utc TEXT NOT NULL,
                modified_utc TEXT NOT NULL,
                CHECK (modified_utc >= created_utc)
            );
            INSERT INTO application_metadata VALUES (1, $applicationId, 1);
            INSERT INTO schema_migrations VALUES (1, $appliedUtc);
            INSERT INTO workspace VALUES (1, $id, $name, $createdUtc, $modifiedUtc);
            """;
    command.Parameters.AddWithValue("$applicationId", ApplicationId);
    command.Parameters.AddWithValue("$appliedUtc", Format(workspace.CreatedUtc));
    command.Parameters.AddWithValue("$id", workspace.Id.ToString());
    command.Parameters.AddWithValue("$name", workspace.Name.ToString());
    command.Parameters.AddWithValue("$createdUtc", Format(workspace.CreatedUtc));
    command.Parameters.AddWithValue("$modifiedUtc", Format(workspace.ModifiedUtc));
    await command.ExecuteNonQueryAsync(token);
    await transaction.CommitAsync(token);
  }

  private static string Format(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
}
