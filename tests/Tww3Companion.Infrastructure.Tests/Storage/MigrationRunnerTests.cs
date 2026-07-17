using Microsoft.Data.Sqlite;
using Tww3Companion.Application.Abstractions;
using Tww3Companion.Application.Common;
using Tww3Companion.Infrastructure.Paths;
using Tww3Companion.Infrastructure.Storage.Backups;
using Tww3Companion.Infrastructure.Storage.Migrations;
using Tww3Companion.Infrastructure.Tests.Storage.Fixtures;
using Xunit;

namespace Tww3Companion.Infrastructure.Tests.Storage;

public sealed class MigrationRunnerTests
{
    [Fact]
    public async Task Migrate_BacksUpBeforeMutationAndRecordsCommittedVersion()
    {
        using var directory = new TemporaryDirectory(); var path = Path.Combine(directory.Path, "old.tww3c");
        await SchemaVersionZeroFixture.CreateAsync(path);
        var runner = CreateRunner(directory.Path, new TestMigration(false));

        var result = Assert.IsType<OperationResult<int>.Success>(await runner.MigrateAsync(path, 1, CancellationToken.None));

        Assert.Equal(1, result.Value);
        Assert.Equal(1L, await ScalarAsync(path, "SELECT schema_version FROM application_metadata"));
        Assert.Equal(1L, await ScalarAsync(path, "SELECT COUNT(*) FROM schema_migrations WHERE version=1"));
        var backup = Assert.Single(Directory.GetFiles(Path.Combine(directory.Path, "Backups", SchemaVersionZeroFixture.WorkspaceId)));
        Assert.Equal(0L, await ScalarAsync(backup, "SELECT schema_version FROM application_metadata"));
    }

    [Fact]
    public async Task Migrate_WhenMigrationFails_RollsBackOriginalAndRetainsUsableBackup()
    {
        using var directory = new TemporaryDirectory(); var path = Path.Combine(directory.Path, "old.tww3c");
        await SchemaVersionZeroFixture.CreateAsync(path);

        var failure = Assert.IsType<OperationResult<int>.Failure>(await CreateRunner(directory.Path, new TestMigration(true)).MigrateAsync(path, 1, CancellationToken.None));

        Assert.Equal("workspace.migration.failed", failure.Error.Code); Assert.False(failure.Error.PersistentChangeCommitted);
        Assert.Equal(0L, await ScalarAsync(path, "SELECT schema_version FROM application_metadata"));
        var backup = Assert.Single(Directory.GetFiles(Path.Combine(directory.Path, "Backups", SchemaVersionZeroFixture.WorkspaceId)));
        Assert.Equal(0L, await ScalarAsync(backup, "SELECT schema_version FROM application_metadata"));
    }

    [Fact]
    public async Task Migrate_WhenResultingStructureIsInvalid_RollsBackOriginalAndRetainsUsableBackup()
    {
        using var directory = new TemporaryDirectory(); var path = Path.Combine(directory.Path, "old.tww3c");
        await SchemaVersionZeroFixture.CreateAsync(path);

        var failure = Assert.IsType<OperationResult<int>.Failure>(await CreateRunner(directory.Path, new InvalidStructureMigration()).MigrateAsync(path, 1, CancellationToken.None));

        Assert.Equal("workspace.migration.failed", failure.Error.Code);
        Assert.Equal(0L, await ScalarAsync(path, "SELECT schema_version FROM application_metadata"));
        Assert.Equal(0L, await ScalarAsync(path, "SELECT COUNT(*) FROM sqlite_schema WHERE type='table' AND name='unexpected'"));
        var backup = Assert.Single(Directory.GetFiles(Path.Combine(directory.Path, "Backups", SchemaVersionZeroFixture.WorkspaceId)));
        Assert.Equal(0L, await ScalarAsync(backup, "SELECT schema_version FROM application_metadata"));
    }

    [Fact]
    public async Task Migrate_RejectsGapWithoutCreatingBackup()
    {
        using var directory = new TemporaryDirectory(); var path = Path.Combine(directory.Path, "old.tww3c"); await SchemaVersionZeroFixture.CreateAsync(path);
        var failure = Assert.IsType<OperationResult<int>.Failure>(await CreateRunner(directory.Path).MigrateAsync(path, 1, CancellationToken.None));
        Assert.Equal("workspace.migration.unsupported", failure.Error.Code); Assert.False(Directory.Exists(Path.Combine(directory.Path, "Backups")));
    }

    private static MigrationRunner CreateRunner(string root, params IMigration[] migrations)
    {
        var clock = new FixedClock(); var paths = ManagedPaths.ForRoot(ApplicationMode.Portable, root);
        return new MigrationRunner(new(), new WorkspaceBackupService(new(), paths, clock), clock, migrations);
    }
    private static async Task<long> ScalarAsync(string path, string sql) { await using var c = await new Tww3Companion.Infrastructure.Storage.SqliteConnectionFactory().OpenAsync(path, CancellationToken.None); await using var cmd = c.CreateCommand(); cmd.CommandText = sql; return (long)(await cmd.ExecuteScalarAsync())!; }
    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow => new(2026, 7, 18, 1, 2, 3, 456, TimeSpan.Zero); }
    private sealed class TestMigration(bool fail) : IMigration
    {
        public int FromVersion => 0; public int ToVersion => 1;
        public async Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken token)
        { await ApplyValidStructureAsync(connection, transaction, token); if (fail) throw new InvalidOperationException("expected"); }
    }
    private sealed class InvalidStructureMigration : IMigration
    {
        public int FromVersion => 0; public int ToVersion => 1;
        public async Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken token)
        { await ApplyValidStructureAsync(connection, transaction, token); await using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "CREATE TABLE unexpected(value TEXT);"; await command.ExecuteNonQueryAsync(token); }
    }

    private static async Task ApplyValidStructureAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken token)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            ALTER TABLE application_metadata RENAME TO old_application_metadata;
            CREATE TABLE application_metadata(singleton INTEGER PRIMARY KEY CHECK(singleton=1), application_id TEXT NOT NULL CHECK(application_id='com.ozzifan.tww3-companion.workspace'), schema_version INTEGER NOT NULL CHECK(schema_version>=1));
            INSERT INTO application_metadata SELECT singleton, application_id, 1 FROM old_application_metadata;
            DROP TABLE old_application_metadata;
            """;
        await command.ExecuteNonQueryAsync(token);
    }
}
