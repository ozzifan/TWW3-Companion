using Microsoft.Data.Sqlite;
using Tww3Companion.Application.Common;
using Tww3Companion.Domain.Workspaces;
using Tww3Companion.Infrastructure.Storage;
using Tww3Companion.Domain.Validation;
using Xunit;

namespace Tww3Companion.Infrastructure.Tests.Storage;

public sealed class WorkspaceFileValidatorTests
{
    [Theory]
    [InlineData("not sqlite", "workspace.file.invalid")]
    [InlineData("SQLite format 3\0broken", "workspace.file.corrupt")]
    public async Task Open_RejectsInvalidFiles(string content, string code)
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "bad.tww3c");
        await File.WriteAllTextAsync(path, content, TestContext.Current.CancellationToken);
        var failure = Assert.IsType<OperationResult<Workspace>.Failure>(
            await new SqliteWorkspaceStore().OpenAsync(path, CancellationToken.None));
        Assert.Equal(code, failure.Error.Code);
    }

    [Fact]
    public async Task Open_RejectsUnrelatedSqliteFile()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "other.tww3c");
        await ExecuteAsync(path, "CREATE TABLE other(value TEXT);");
        await AssertCodeAsync(path, "workspace.file.invalid");
    }

    [Theory]
    [InlineData("UPDATE application_metadata SET schema_version=2", "workspace.schema.newer")]
    [InlineData("PRAGMA ignore_check_constraints=ON; UPDATE application_metadata SET schema_version=0", "workspace.file.invalid")]
    [InlineData("PRAGMA ignore_check_constraints=ON; UPDATE application_metadata SET application_id='forged'", "workspace.file.invalid")]
    [InlineData("PRAGMA ignore_check_constraints=ON; UPDATE application_metadata SET singleton=2", "workspace.file.invalid")]
    [InlineData("UPDATE workspace SET id='invalid'", "workspace.identity.invalid")]
    [InlineData("UPDATE workspace SET id='12345678-1234-4ABC-8DEF-1234567890AB'", "workspace.identity.invalid")]
    [InlineData("PRAGMA ignore_check_constraints=ON; UPDATE workspace SET display_name='   '", "workspace.identity.invalid")]
    [InlineData("UPDATE workspace SET created_utc='2026-07-18T01:02:03.0000000+01:00'", "workspace.identity.invalid")]
    [InlineData("PRAGMA ignore_check_constraints=ON; UPDATE workspace SET singleton=2", "workspace.identity.invalid")]
    public async Task Open_RejectsInvalidMetadata(string mutation, string code)
    {
        using var directory = new TemporaryDirectory();
        var path = await CreateValidAsync(directory.Path);
        await ExecuteAsync(path, mutation);
        await AssertCodeAsync(path, code);
    }

    [Theory]
    [InlineData("DELETE FROM schema_migrations", "workspace.file.invalid")]
    [InlineData("UPDATE schema_migrations SET applied_utc='not-a-timestamp'", "workspace.file.invalid")]
    [InlineData("UPDATE schema_migrations SET version=2", "workspace.file.invalid")]
    [InlineData("CREATE TABLE unexpected(value TEXT)", "workspace.file.invalid")]
    [InlineData("ALTER TABLE workspace ADD COLUMN forged TEXT", "workspace.file.invalid")]
    public async Task Open_RejectsInvalidMigrationOrStructure(string mutation, string code)
    {
        using var directory = new TemporaryDirectory();
        var path = await CreateValidAsync(directory.Path);
        await ExecuteAsync(path, mutation);
        await AssertCodeAsync(path, code);
    }

    [Fact]
    public async Task Open_RejectsForeignKeyViolations()
    {
        using var directory = new TemporaryDirectory();
        var path = await CreateValidAsync(directory.Path);
        await ExecuteAsync(path, """
            PRAGMA foreign_keys=OFF;
            ALTER TABLE application_metadata RENAME TO original_metadata;
            CREATE TABLE application_metadata (
                singleton INTEGER PRIMARY KEY,
                application_id TEXT NOT NULL UNIQUE,
                schema_version INTEGER NOT NULL);
            INSERT INTO application_metadata SELECT * FROM original_metadata;
            DROP TABLE original_metadata;
            ALTER TABLE workspace RENAME TO original_workspace;
            CREATE TABLE workspace (
                singleton INTEGER PRIMARY KEY,
                id TEXT NOT NULL UNIQUE,
                display_name TEXT NOT NULL,
                created_utc TEXT NOT NULL,
                modified_utc TEXT NOT NULL,
                FOREIGN KEY (id) REFERENCES application_metadata(application_id));
            INSERT INTO workspace SELECT singleton,id,display_name,created_utc,modified_utc FROM original_workspace;
            DROP TABLE original_workspace;
            """);

        await AssertCodeAsync(path, "workspace.file.corrupt");
    }

    [Fact]
    public async Task Open_WhenFileIsExclusivelyLocked_ReturnsLockedCode()
    {
        using var directory = new TemporaryDirectory();
        var path = await CreateValidAsync(directory.Path);
        await using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        await AssertCodeAsync(path, "workspace.file.locked");
    }

    [Fact]
    public async Task Open_WhenHeaderAccessIsDenied_ReturnsAccessDeniedCode()
    {
        using var directory = new TemporaryDirectory();
        var path = await CreateValidAsync(directory.Path);
        var validator = new WorkspaceFileValidator(
            openHeaderStream: _ => throw new UnauthorizedAccessException("denied"));

        var failure = Assert.IsType<OperationResult<Workspace>.Failure>(
            await validator.OpenAsync(path, TestContext.Current.CancellationToken));

        Assert.Equal("workspace.access.denied", failure.Error.Code);
        Assert.False(failure.Error.PersistentChangeCommitted);
    }

    [Fact]
    public async Task Open_RejectsDuplicateWorkspaceRows()
    {
        using var directory = new TemporaryDirectory();
        var path = await CreateValidAsync(directory.Path);
        await ExecuteAsync(path, "PRAGMA ignore_check_constraints=ON; UPDATE workspace SET singleton=2; INSERT INTO workspace VALUES(1,'87654321-4321-4abc-8def-1234567890ab','Other','2026-07-18T01:02:03.0000000+00:00','2026-07-18T04:05:06.0000000+00:00')");
        await AssertCodeAsync(path, "workspace.identity.invalid");
    }

    private static async Task<string> CreateValidAsync(string directory)
    {
        var path = Path.Combine(directory, "valid.tww3c");
        var id = ((ValidationResult<WorkspaceId>.Success)WorkspaceId.Parse("12345678-1234-4abc-8def-1234567890ab")).Value;
        var name = ((ValidationResult<WorkspaceName>.Success)WorkspaceName.Create("Name")).Value;
        var workspace = ((ValidationResult<Workspace>.Success)Workspace.Create(id, name, DateTimeOffset.Parse("2026-07-18T01:02:03Z"), DateTimeOffset.Parse("2026-07-18T04:05:06Z"))).Value;
        Assert.IsType<OperationResult<Workspace>.Success>(await new SqliteWorkspaceStore().CreateAsync(path, workspace, CancellationToken.None));
        return path;
    }

    private static async Task ExecuteAsync(string path, string sql)
    {
        await using var connection = await new SqliteConnectionFactory().OpenAsync(path, CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task AssertCodeAsync(string path, string code)
    {
        var failure = Assert.IsType<OperationResult<Workspace>.Failure>(await new SqliteWorkspaceStore().OpenAsync(path, CancellationToken.None));
        Assert.Equal(code, failure.Error.Code);
    }
}
