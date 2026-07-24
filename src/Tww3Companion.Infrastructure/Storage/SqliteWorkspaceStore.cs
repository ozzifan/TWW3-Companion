using Microsoft.Data.Sqlite;
using Tww3Companion.Application.Common;
using Tww3Companion.Application.Workspaces;
using Tww3Companion.Domain.Workspaces;
using Tww3Companion.Infrastructure.Settings;
using Tww3Companion.Infrastructure.Storage.Migrations;
using Tww3Companion.Infrastructure.Storage.Schema;

namespace Tww3Companion.Infrastructure.Storage;

public sealed class SqliteWorkspaceStore : IWorkspaceStore
{
  private readonly SqliteConnectionFactory connectionFactory;
  private readonly WorkspaceFileValidator validator;
  private readonly IAtomicFileSystem fileSystem;
  private readonly Action<string> deleteOwnedFile;
  private readonly MigrationRunner? migrationRunner;

  public SqliteWorkspaceStore(
      SqliteConnectionFactory? connectionFactory = null,
      IAtomicFileSystem? fileSystem = null,
      Action<string>? deleteOwnedFile = null,
      MigrationRunner? migrationRunner = null)
  {
    this.connectionFactory = connectionFactory ?? new();
    validator = new(this.connectionFactory);
    this.fileSystem = fileSystem ?? new AtomicFileSystem();
    this.deleteOwnedFile = deleteOwnedFile ?? File.Delete;
    this.migrationRunner = migrationRunner;
  }

  public async Task<OperationResult<Workspace>> CreateAsync(string path, Workspace workspace, CancellationToken token)
  {
    if (File.Exists(path)) return WorkspaceFileValidator.Failure("workspace.file.invalid", "The destination already exists.");
    var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
    try
    {
      await using (var connection = await connectionFactory.OpenAsync(temporaryPath, token))
      {
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(token);
        await SchemaV2.InitializeAsync(
            connection,
            transaction,
            workspace,
            token);
        await WorkspaceSchemaInspector.ValidateAsync(
            connection,
            transaction,
            SchemaVersion.Current,
            CancellationToken.None);
        await transaction.CommitAsync(CancellationToken.None);
      }
      var validation = await validator.OpenAsync(temporaryPath, token);
      if (validation is OperationResult<Workspace>.Failure failure) return failure;
      fileSystem.MoveWithoutOverwrite(temporaryPath, path);
      return await validator.OpenAsync(path, token);
    }
    catch (UnauthorizedAccessException) { return WorkspaceFileValidator.Failure("workspace.access.denied", "Access to the destination was denied."); }
    catch (IOException) { return WorkspaceFileValidator.Failure("workspace.file.invalid", "The destination could not be created without overwriting a file."); }
    catch (WorkspaceSchemaStructureException) { return WorkspaceFileValidator.Failure("workspace.file.invalid", "The Workspace could not be created."); }
    catch (WorkspaceSchemaIntegrityException) { return WorkspaceFileValidator.Failure("workspace.file.corrupt", "The Workspace could not be created."); }
    catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6) { return WorkspaceFileValidator.Failure("workspace.file.locked", "The destination is locked."); }
    catch (SqliteException) { return WorkspaceFileValidator.Failure("workspace.file.invalid", "The Workspace could not be created."); }
    finally
    {
      try { deleteOwnedFile(temporaryPath); }
      catch (IOException) { }
      catch (UnauthorizedAccessException) { }
    }
  }

  public async Task<OperationResult<Workspace>> OpenAsync(string path, CancellationToken token)
  {
    int? version;
    try
    {
      version = await validator.ReadSchemaVersionAsync(path, token);
    }
    catch (IOException)
    {
      return WorkspaceFileValidator.Failure("workspace.file.locked", "The Workspace file is locked.");
    }
    catch (UnauthorizedAccessException)
    {
      return WorkspaceFileValidator.Failure("workspace.access.denied", "Access to the Workspace was denied.");
    }

    if (version is null)
      return await validator.OpenAsync(path, token);

    if (version > SchemaVersion.Current)
      return WorkspaceFileValidator.Failure("workspace.schema.newer", "This Workspace was created by a newer application version.");

    if (version == 1)
    {
      if (migrationRunner is null)
        return WorkspaceFileValidator.Failure("workspace.migration.unsupported", "No complete migration path is available.");
      var migration = await migrationRunner.MigrateAsync(path, SchemaVersion.Current, token);
      if (migration is OperationResult<int>.Failure failure)
        return WorkspaceFileValidator.Failure(failure.Error.Code, failure.Error.Message);
    }

    return await validator.OpenAsync(path, token);
  }
}
