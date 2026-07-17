using Microsoft.Data.Sqlite;

namespace Tww3Companion.Infrastructure.Storage.Migrations;

public interface IMigration
{
    int FromVersion { get; }
    int ToVersion { get; }
    Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken token);
}
