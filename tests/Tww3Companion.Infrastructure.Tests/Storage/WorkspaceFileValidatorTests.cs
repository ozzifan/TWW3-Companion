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
    [InlineData("UPDATE workspace SET id='invalid'", "workspace.identity.invalid")]
    [InlineData("PRAGMA ignore_check_constraints=ON; UPDATE workspace SET display_name='   '", "workspace.identity.invalid")]
    public async Task Open_RejectsInvalidMetadata(string mutation, string code)
    {
        using var directory = new TemporaryDirectory();
        var path = await CreateValidAsync(directory.Path);
        await ExecuteAsync(path, mutation);
        await AssertCodeAsync(path, code);
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
