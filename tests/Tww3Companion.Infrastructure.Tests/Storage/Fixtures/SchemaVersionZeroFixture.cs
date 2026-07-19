using Microsoft.Data.Sqlite;
using Tww3Companion.Infrastructure.Storage;

namespace Tww3Companion.Infrastructure.Tests.Storage.Fixtures;

internal static class SchemaVersionZeroFixture
{
  public const string WorkspaceId = "12345678-1234-4abc-8def-1234567890ab";

  public static async Task CreateAsync(string path)
  {
    await using var connection = await new SqliteConnectionFactory().OpenAsync(path, CancellationToken.None);
    await using var command = connection.CreateCommand();
    command.CommandText = $"""
            CREATE TABLE application_metadata(singleton INTEGER PRIMARY KEY CHECK(singleton=1), application_id TEXT NOT NULL CHECK(application_id='com.ozzifan.tww3-companion.workspace'), schema_version INTEGER NOT NULL CHECK(schema_version>=0));
            INSERT INTO application_metadata VALUES(1, 'com.ozzifan.tww3-companion.workspace', 0);
            CREATE TABLE schema_migrations(version INTEGER PRIMARY KEY CHECK(version>=1), applied_utc TEXT NOT NULL);
            CREATE TABLE workspace(singleton INTEGER PRIMARY KEY CHECK(singleton=1), id TEXT NOT NULL UNIQUE, display_name TEXT NOT NULL CHECK(length(trim(display_name))>0), created_utc TEXT NOT NULL, modified_utc TEXT NOT NULL, CHECK(modified_utc>=created_utc));
            INSERT INTO workspace VALUES(1, '{WorkspaceId}', 'Fixture', '2026-07-18T00:00:00.0000000+00:00', '2026-07-18T00:00:00.0000000+00:00');
            """;
    await command.ExecuteNonQueryAsync();
  }
}
