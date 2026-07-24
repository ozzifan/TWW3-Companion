using Microsoft.Data.Sqlite;

namespace Tww3Companion.Infrastructure.Storage.Schema;

internal static class WorkspaceSchemaInspector
{
  public static async Task ValidateAsync(
      SqliteConnection connection,
      SqliteTransaction? transaction,
      int schemaVersion,
      CancellationToken cancellationToken)
  {
    switch (schemaVersion)
    {
      case 1:
        await ValidateStructureAsync(
            connection,
            transaction,
            ["application_metadata", "schema_migrations", "workspace"],
            V1Columns,
            HasV1Constraints,
            cancellationToken);
        break;
      case 2:
        await ValidateStructureAsync(
            connection,
            transaction,
            ["application_metadata", "collection_memberships", "collections",
             "mods", "schema_migrations", "source_references", "workspace"],
            V2Columns,
            HasV2Constraints,
            cancellationToken);
        break;
      default:
        throw new InvalidOperationException("Unsupported schema version.");
    }

    await ValidateIntegrityAsync(connection, transaction, cancellationToken);
  }

  private static readonly (string Table, string[] Columns)[] V1Columns =
  [
      ("application_metadata", ["singleton|INTEGER|0|1", "application_id|TEXT|1|0", "schema_version|INTEGER|1|0"]),
      ("schema_migrations", ["version|INTEGER|0|1", "applied_utc|TEXT|1|0"]),
      ("workspace", ["singleton|INTEGER|0|1", "id|TEXT|1|0", "display_name|TEXT|1|0", "created_utc|TEXT|1|0", "modified_utc|TEXT|1|0"])
  ];

  private static readonly (string Table, string[] Columns)[] V2Columns =
  [
      ("application_metadata", ["singleton|INTEGER|0|1", "application_id|TEXT|1|0", "schema_version|INTEGER|1|0"]),
      ("schema_migrations", ["version|INTEGER|0|1", "applied_utc|TEXT|1|0"]),
      ("workspace", ["singleton|INTEGER|0|1", "id|TEXT|1|0", "display_name|TEXT|1|0", "created_utc|TEXT|1|0", "modified_utc|TEXT|1|0"]),
      ("mods", ["id|TEXT|0|1", "display_name|TEXT|1|0"]),
      ("collections", ["id|TEXT|0|1", "display_name|TEXT|1|0"]),
      ("source_references", ["source_type|TEXT|1|1", "external_id|TEXT|1|2", "mod_id|TEXT|1|0"]),
      ("collection_memberships", ["collection_id|TEXT|1|1", "mod_id|TEXT|1|2", "position|INTEGER|1|0"])
  ];

  private static async Task ValidateStructureAsync(
      SqliteConnection connection,
      SqliteTransaction? transaction,
      string[] expectedTables,
      (string Table, string[] Columns)[] expectedColumns,
      Func<SqliteConnection, SqliteTransaction?, CancellationToken, Task<bool>> hasConstraints,
      CancellationToken cancellationToken)
  {
    var tables = new List<string>();
    await using (var structure = connection.CreateCommand())
    {
      structure.Transaction = transaction;
      structure.CommandText =
          "SELECT name FROM sqlite_schema WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
      await using var reader = await structure.ExecuteReaderAsync(cancellationToken);
      while (await reader.ReadAsync(cancellationToken))
        tables.Add(reader.GetString(0));
    }

    if (!tables.SequenceEqual(expectedTables))
      throw new WorkspaceSchemaStructureException("Workspace structure is invalid.");

    foreach (var (table, expected) in expectedColumns)
    {
      if (!await HasColumnsAsync(connection, transaction, table, expected, cancellationToken))
        throw new WorkspaceSchemaStructureException("Workspace structure is invalid.");
    }

    if (!await hasConstraints(connection, transaction, cancellationToken))
      throw new WorkspaceSchemaStructureException("Workspace structure is invalid.");
  }

  private static async Task ValidateIntegrityAsync(
      SqliteConnection connection,
      SqliteTransaction? transaction,
      CancellationToken cancellationToken)
  {
    await using var check = connection.CreateCommand();
    check.Transaction = transaction;
    check.CommandText = "PRAGMA integrity_check; PRAGMA foreign_key_check;";
    await using var reader = await check.ExecuteReaderAsync(cancellationToken);
    if (!await reader.ReadAsync(cancellationToken) ||
        reader.GetString(0) != "ok" ||
        await reader.ReadAsync(cancellationToken))
      throw new WorkspaceSchemaIntegrityException("The Workspace database is corrupt.");
    if (!await reader.NextResultAsync(cancellationToken) ||
        await reader.ReadAsync(cancellationToken))
      throw new WorkspaceSchemaIntegrityException("The Workspace database has invalid relationships.");
  }

  private static async Task<bool> HasColumnsAsync(
      SqliteConnection connection,
      SqliteTransaction? transaction,
      string table,
      IReadOnlyList<string> expected,
      CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = $"PRAGMA table_info({table});";
    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    var actual = new List<string>();
    while (await reader.ReadAsync(cancellationToken))
      actual.Add($"{reader.GetString(1)}|{reader.GetString(2)}|{reader.GetInt32(3)}|{reader.GetInt32(5)}");
    return actual.SequenceEqual(expected);
  }

  private static async Task<bool> HasV1Constraints(
      SqliteConnection connection,
      SqliteTransaction? transaction,
      CancellationToken cancellationToken) =>
      HasBaseConstraints(await ReadTableSqlAsync(connection, transaction, cancellationToken));

  private static async Task<bool> HasV2Constraints(
      SqliteConnection connection,
      SqliteTransaction? transaction,
      CancellationToken cancellationToken)
  {
    var sql = await ReadTableSqlAsync(connection, transaction, cancellationToken);
    if (!HasBaseConstraints(sql))
      return false;

    return sql["mods"].Contains("check(length(trim(display_name))>0)", StringComparison.Ordinal) &&
           sql["collections"].Contains("check(length(trim(display_name))>0)", StringComparison.Ordinal) &&
           sql["source_references"].Contains("check(length(trim(external_id))>0)", StringComparison.Ordinal) &&
           sql["source_references"].Contains("primarykey(source_type,external_id)", StringComparison.Ordinal) &&
           sql["source_references"].Contains("foreignkey(mod_id)referencesmods(id)ondeleterestrict", StringComparison.Ordinal) &&
           sql["collection_memberships"].Contains("check(position>=0)", StringComparison.Ordinal) &&
           sql["collection_memberships"].Contains("primarykey(collection_id,mod_id)", StringComparison.Ordinal) &&
           sql["collection_memberships"].Contains("unique(collection_id,position)", StringComparison.Ordinal) &&
           sql["collection_memberships"].Contains("foreignkey(collection_id)referencescollections(id)ondeletecascade", StringComparison.Ordinal) &&
           sql["collection_memberships"].Contains("foreignkey(mod_id)referencesmods(id)ondeleterestrict", StringComparison.Ordinal);
  }

  private static bool HasBaseConstraints(IReadOnlyDictionary<string, string> sql) =>
      sql["application_metadata"].Contains("check(singleton=1)", StringComparison.Ordinal) &&
      sql["application_metadata"].Contains("check(application_id='com.ozzifan.tww3-companion.workspace')", StringComparison.Ordinal) &&
      sql["application_metadata"].Contains("check(schema_version>=1)", StringComparison.Ordinal) &&
      sql["schema_migrations"].Contains("check(version>=1)", StringComparison.Ordinal) &&
      sql["workspace"].Contains("check(singleton=1)", StringComparison.Ordinal) &&
      sql["workspace"].Contains("idtextnotnullunique", StringComparison.Ordinal) &&
      sql["workspace"].Contains("check(length(trim(display_name))>0)", StringComparison.Ordinal) &&
      sql["workspace"].Contains("check(modified_utc>=created_utc)", StringComparison.Ordinal);

  private static async Task<Dictionary<string, string>> ReadTableSqlAsync(
      SqliteConnection connection,
      SqliteTransaction? transaction,
      CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = "SELECT name, sql FROM sqlite_schema WHERE type='table' AND name NOT LIKE 'sqlite_%';";
    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    var sql = new Dictionary<string, string>();
    while (await reader.ReadAsync(cancellationToken))
      sql[reader.GetString(0)] = reader.GetString(1).Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant();
    return sql;
  }
}

internal class WorkspaceSchemaStructureException(string message) : InvalidOperationException(message);

internal class WorkspaceSchemaIntegrityException(string message) : InvalidOperationException(message);
