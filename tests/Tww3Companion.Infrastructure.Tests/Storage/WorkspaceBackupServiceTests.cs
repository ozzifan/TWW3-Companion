using Tww3Companion.Application.Abstractions;
using Tww3Companion.Application.Common;
using Tww3Companion.Infrastructure.Paths;
using Tww3Companion.Infrastructure.Storage.Backups;
using Tww3Companion.Infrastructure.Tests.Storage.Fixtures;
using Xunit;

namespace Tww3Companion.Infrastructure.Tests.Storage;

public sealed class WorkspaceBackupServiceTests
{
    [Fact]
    public async Task Create_UsesExactManagedNameAndProducesUsableSqliteBackup()
    {
        using var directory = new TemporaryDirectory();
        var source = Path.Combine(directory.Path, "source.tww3c");
        await SchemaVersionZeroFixture.CreateAsync(source);
        var service = new WorkspaceBackupService(new(), ManagedPaths.ForRoot(ApplicationMode.Portable, directory.Path), new FixedClock(new DateTimeOffset(2026, 7, 18, 1, 2, 3, 456, TimeSpan.Zero)));

        var result = Assert.IsType<OperationResult<string>.Success>(await service.CreateAsync(source, SchemaVersionZeroFixture.WorkspaceId, BackupReason.PreMigration, TestContext.Current.CancellationToken));

        Assert.Equal(Path.Combine(directory.Path, "Backups", SchemaVersionZeroFixture.WorkspaceId, "20260718T010203456Z.pre-migration.tww3c"), result.Value);
        await using var backup = await new Tww3Companion.Infrastructure.Storage.SqliteConnectionFactory().OpenAsync(result.Value, TestContext.Current.CancellationToken);
        await using var command = backup.CreateCommand(); command.CommandText = "SELECT schema_version FROM application_metadata";
        Assert.Equal(0L, await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Cleanup_KeepsFiveManagedBackupsAndNeverDeletesUnrelatedFiles()
    {
        using var directory = new TemporaryDirectory();
        var paths = ManagedPaths.ForRoot(ApplicationMode.Portable, directory.Path);
        var folder = Path.Combine(paths.BackupsDirectory, SchemaVersionZeroFixture.WorkspaceId);
        Directory.CreateDirectory(folder);
        for (var i = 0; i < 7; i++) await File.WriteAllTextAsync(Path.Combine(folder, $"20260718T01020{i}000Z.pre-migration.tww3c"), "x", TestContext.Current.CancellationToken);
        var unrelated = Path.Combine(folder, "notes.tww3c"); await File.WriteAllTextAsync(unrelated, "keep", TestContext.Current.CancellationToken);

        await new WorkspaceBackupService(new(), paths, new FixedClock(DateTimeOffset.UtcNow)).CleanupAsync(SchemaVersionZeroFixture.WorkspaceId, TestContext.Current.CancellationToken);

        Assert.Equal(5, Directory.GetFiles(folder, "*.pre-migration.tww3c").Length);
        Assert.True(File.Exists(unrelated));
    }

    private sealed class FixedClock(DateTimeOffset value) : IClock { public DateTimeOffset UtcNow => value; }
}
