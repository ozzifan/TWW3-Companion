using Microsoft.Data.Sqlite;
using Tww3Companion.Application.Common;
using Tww3Companion.Application.Workspaces;
using Tww3Companion.Domain.Workspaces;
using Tww3Companion.Infrastructure.Settings;
using Tww3Companion.Infrastructure.Storage.Schema;

namespace Tww3Companion.Infrastructure.Storage;

public sealed class SqliteWorkspaceStore : IWorkspaceStore
{
    private readonly SqliteConnectionFactory connectionFactory;
    private readonly WorkspaceFileValidator validator;
    private readonly IAtomicFileSystem fileSystem;
    private readonly Action<string> deleteOwnedFile;

    public SqliteWorkspaceStore(
        SqliteConnectionFactory? connectionFactory = null,
        IAtomicFileSystem? fileSystem = null,
        Action<string>? deleteOwnedFile = null)
    {
        this.connectionFactory = connectionFactory ?? new();
        validator = new(this.connectionFactory);
        this.fileSystem = fileSystem ?? new AtomicFileSystem();
        this.deleteOwnedFile = deleteOwnedFile ?? File.Delete;
    }

    public async Task<OperationResult<Workspace>> CreateAsync(string path, Workspace workspace, CancellationToken token)
    {
        if (File.Exists(path)) return WorkspaceFileValidator.Failure("workspace.file.invalid", "The destination already exists.");
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var connection = await connectionFactory.OpenAsync(temporaryPath, token))
                await SchemaV1.CreateAsync(connection, workspace, token);
            var validation = await validator.OpenAsync(temporaryPath, token);
            if (validation is OperationResult<Workspace>.Failure failure) return failure;
            fileSystem.MoveWithoutOverwrite(temporaryPath, path);
            return await validator.OpenAsync(path, token);
        }
        catch (UnauthorizedAccessException) { return WorkspaceFileValidator.Failure("workspace.access.denied", "Access to the destination was denied."); }
        catch (IOException) { return WorkspaceFileValidator.Failure("workspace.file.invalid", "The destination could not be created without overwriting a file."); }
        catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6) { return WorkspaceFileValidator.Failure("workspace.file.locked", "The destination is locked."); }
        catch (SqliteException) { return WorkspaceFileValidator.Failure("workspace.file.invalid", "The Workspace could not be created."); }
        finally
        {
            try { deleteOwnedFile(temporaryPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public Task<OperationResult<Workspace>> OpenAsync(string path, CancellationToken token) => validator.OpenAsync(path, token);
}
