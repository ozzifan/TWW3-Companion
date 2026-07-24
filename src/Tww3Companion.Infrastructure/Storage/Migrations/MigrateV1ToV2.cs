using Microsoft.Data.Sqlite;
using Tww3Companion.Infrastructure.Storage.Schema;

namespace Tww3Companion.Infrastructure.Storage.Migrations;

public sealed class MigrateV1ToV2 : IMigration
{
  public int FromVersion => 1;
  public int ToVersion => 2;

  public async Task ApplyAsync(
      SqliteConnection connection,
      SqliteTransaction transaction,
      CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = SchemaV2.CatalogTablesSql;
    await command.ExecuteNonQueryAsync(cancellationToken);
  }
}
