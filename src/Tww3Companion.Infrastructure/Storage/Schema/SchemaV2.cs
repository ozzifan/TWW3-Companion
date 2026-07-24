using System.Globalization;
using Microsoft.Data.Sqlite;
using Tww3Companion.Domain.Workspaces;

namespace Tww3Companion.Infrastructure.Storage.Schema;

internal static class SchemaV2
{
  internal const string CatalogTablesSql = """
        CREATE TABLE mods (
            id TEXT PRIMARY KEY,
            display_name TEXT NOT NULL CHECK (length(trim(display_name)) > 0)
        );

        CREATE TABLE collections (
            id TEXT PRIMARY KEY,
            display_name TEXT NOT NULL CHECK (length(trim(display_name)) > 0)
        );

        CREATE TABLE source_references (
            source_type TEXT NOT NULL,
            external_id TEXT NOT NULL CHECK (length(trim(external_id)) > 0),
            mod_id TEXT NOT NULL,
            PRIMARY KEY (source_type, external_id),
            FOREIGN KEY (mod_id) REFERENCES mods(id) ON DELETE RESTRICT
        );

        CREATE TABLE collection_memberships (
            collection_id TEXT NOT NULL,
            mod_id TEXT NOT NULL,
            position INTEGER NOT NULL CHECK (position >= 0),
            PRIMARY KEY (collection_id, mod_id),
            UNIQUE (collection_id, position),
            FOREIGN KEY (collection_id) REFERENCES collections(id) ON DELETE CASCADE,
            FOREIGN KEY (mod_id) REFERENCES mods(id) ON DELETE RESTRICT
        );
        """;

  internal static async Task InitializeAsync(
      SqliteConnection connection,
      SqliteTransaction transaction,
      Workspace workspace,
      CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = $"""
            CREATE TABLE application_metadata (
                singleton INTEGER PRIMARY KEY CHECK (singleton = 1),
                application_id TEXT NOT NULL CHECK (application_id = '{SchemaV1.ApplicationId}'),
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
            {CatalogTablesSql}
            INSERT INTO application_metadata VALUES (1, $applicationId, 2);
            INSERT INTO schema_migrations VALUES (1, $appliedUtc);
            INSERT INTO schema_migrations VALUES (2, $appliedUtc);
            INSERT INTO workspace VALUES (1, $id, $name, $createdUtc, $modifiedUtc);
            """;
    command.Parameters.AddWithValue("$applicationId", SchemaV1.ApplicationId);
    command.Parameters.AddWithValue("$appliedUtc", Format(workspace.CreatedUtc));
    command.Parameters.AddWithValue("$id", workspace.Id.ToString());
    command.Parameters.AddWithValue("$name", workspace.Name.ToString());
    command.Parameters.AddWithValue("$createdUtc", Format(workspace.CreatedUtc));
    command.Parameters.AddWithValue("$modifiedUtc", Format(workspace.ModifiedUtc));
    await command.ExecuteNonQueryAsync(cancellationToken);
  }

  private static string Format(DateTimeOffset value) =>
      value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
}
