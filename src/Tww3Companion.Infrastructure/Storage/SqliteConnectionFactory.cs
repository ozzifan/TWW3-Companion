using Microsoft.Data.Sqlite;

namespace Tww3Companion.Infrastructure.Storage;

public sealed class SqliteConnectionFactory
{
    private static readonly Lazy<bool> Provider = new(() => { SQLitePCL.Batteries_V2.Init(); return true; });

    public async Task<SqliteConnection> OpenAsync(string path, CancellationToken cancellationToken)
    {
        _ = Provider.Value;
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString());
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
            await command.ExecuteNonQueryAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
