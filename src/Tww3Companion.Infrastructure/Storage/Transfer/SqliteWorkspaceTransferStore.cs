using System.Globalization;
using Microsoft.Data.Sqlite;
using Tww3Companion.Application.Common;
using Tww3Companion.Application.Workspaces.Transfer;
using Tww3Companion.Domain.Validation;
using Tww3Companion.Domain.Workspaces;
using Tww3Companion.Infrastructure.Settings;
using Tww3Companion.Infrastructure.Storage.Backups;
using Tww3Companion.Infrastructure.Storage.Schema;

namespace Tww3Companion.Infrastructure.Storage.Transfer;

public sealed class SqliteWorkspaceTransferStore : IWorkspaceTransferStore
{
  private readonly SqliteConnectionFactory connectionFactory;
  private readonly WorkspaceFileValidator validator;
  private readonly IAtomicFileSystem fileSystem;
  private readonly WorkspaceBackupService? backupService;
  private readonly Action<string> deleteOwnedFile;
  private readonly Action<string>? afterPersistStage;
  private readonly Func<string, CancellationToken, Task<OperationResult<Workspace>>> openWorkspaceAsync;

  public SqliteWorkspaceTransferStore(
      SqliteConnectionFactory? connectionFactory = null,
      IAtomicFileSystem? fileSystem = null,
      Action<string>? deleteOwnedFile = null,
      WorkspaceBackupService? backupService = null,
      WorkspaceFileValidator? validator = null,
      Action<string>? afterPersistStage = null,
      Func<string, CancellationToken, Task<OperationResult<Workspace>>>? openWorkspaceAsync = null)
  {
    this.connectionFactory = connectionFactory ?? new();
    this.validator = validator ?? new WorkspaceFileValidator(this.connectionFactory);
    this.fileSystem = fileSystem ?? new AtomicFileSystem();
    this.deleteOwnedFile = deleteOwnedFile ?? File.Delete;
    this.backupService = backupService;
    this.afterPersistStage = afterPersistStage;
    this.openWorkspaceAsync = openWorkspaceAsync ?? this.validator.OpenAsync;
  }

  public async Task<OperationResult<WorkspaceTransferSnapshot>> ReadSnapshotAsync(
      string workspacePath,
      CancellationToken cancellationToken)
  {
    try
    {
      cancellationToken.ThrowIfCancellationRequested();
      var opened = await validator.OpenAsync(workspacePath, cancellationToken);
      if (opened is OperationResult<Workspace>.Failure failure)
      {
        return MapWorkspaceFailure(failure);
      }

      await using var connection = await connectionFactory.OpenAsync(workspacePath, cancellationToken);
      await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
      var version = await ReadSchemaVersionAsync(connection, transaction, cancellationToken);
      if (version != SchemaVersion.Current)
      {
        return SnapshotFailure("workspace.transfer.snapshot.unsupported", "The Workspace schema is not supported for export.");
      }

      var workspace = await ReadWorkspaceRowAsync(connection, transaction, cancellationToken);
      if (workspace is null)
      {
        return SnapshotFailure("workspace.transfer.snapshot.invalid", "The Workspace snapshot is invalid.");
      }

      var mods = await ReadModsAsync(connection, transaction, cancellationToken);
      var sourceReferences = await ReadSourceReferencesAsync(connection, transaction, cancellationToken);
      var collections = await ReadCollectionsAsync(connection, transaction, cancellationToken);
      var memberships = await ReadMembershipsAsync(connection, transaction, cancellationToken);
      var snapshot = new WorkspaceTransferSnapshot(
          WorkspaceTransferValidation.SupportedFormat,
          workspace,
          mods,
          sourceReferences,
          collections,
          memberships);
      var validationErrors = WorkspaceTransferValidation.Validate(snapshot);
      if (validationErrors.Count > 0)
      {
        return new OperationResult<WorkspaceTransferSnapshot>.Failure(validationErrors[0]);
      }

      return new OperationResult<WorkspaceTransferSnapshot>.Success(snapshot);
    }
    catch (OperationCanceledException)
    {
      return SnapshotFailure("workspace.export.cancelled", "Workspace export was cancelled.");
    }
    catch (IOException)
    {
      return SnapshotFailure("workspace.export.failed", "The Workspace export could not be read.");
    }
    catch (UnauthorizedAccessException)
    {
      return SnapshotFailure("workspace.access.denied", "Access to the Workspace was denied.");
    }
    catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6)
    {
      return SnapshotFailure("workspace.file.locked", "The Workspace file is locked.");
    }
    catch (SqliteException)
    {
      return SnapshotFailure("workspace.export.failed", "The Workspace export could not be read.");
    }
  }

  public async Task<OperationResult<WorkspaceTransferSnapshot>> ReadExportAsync(
      string exportPath,
      CancellationToken cancellationToken)
  {
    try
    {
      cancellationToken.ThrowIfCancellationRequested();
      if (!File.Exists(exportPath))
      {
        return SnapshotFailure("workspace.restore.source.missing", "The selected export file does not exist.");
      }

      var json = await File.ReadAllTextAsync(exportPath, cancellationToken);
      return WorkspaceJsonCodec.Deserialize(json);
    }
    catch (OperationCanceledException)
    {
      return SnapshotFailure("workspace.restore.cancelled", "Workspace restore was cancelled.");
    }
    catch (IOException)
    {
      return SnapshotFailure("workspace.restore.source.invalid", "The selected export could not be read.");
    }
    catch (UnauthorizedAccessException)
    {
      return SnapshotFailure("workspace.access.denied", "Access to the export was denied.");
    }
  }

  public async Task<OperationResult<string>> WriteExportAsync(
      WorkspaceTransferSnapshot snapshot,
      string exportPath,
      CancellationToken cancellationToken)
  {
    var serialized = WorkspaceJsonCodec.Serialize(snapshot);
    if (serialized is OperationResult<string>.Failure failure)
    {
      return failure;
    }

    try
    {
      cancellationToken.ThrowIfCancellationRequested();
      await fileSystem.WriteAllTextAtomicallyAsync(
          exportPath,
          ((OperationResult<string>.Success)serialized).Value,
          cancellationToken);
      return new OperationResult<string>.Success(exportPath);
    }
    catch (OperationCanceledException)
    {
      return ExportFailure("workspace.export.cancelled", "Workspace export was cancelled.");
    }
    catch (IOException)
    {
      return ExportFailure("workspace.export.failed", "The Workspace export could not be written.");
    }
    catch (UnauthorizedAccessException)
    {
      return ExportFailure("workspace.access.denied", "Access to the export destination was denied.");
    }
  }

  public async Task<OperationResult<Workspace>> RestoreNewAsync(
      WorkspaceTransferSnapshot snapshot,
      string destinationPath,
      CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(snapshot);

    var validationErrors = WorkspaceTransferValidation.Validate(snapshot);
    if (validationErrors.Count > 0)
    {
      return new OperationResult<Workspace>.Failure(validationErrors[0]);
    }

    if (File.Exists(destinationPath))
    {
      return RestoreFailure("workspace.file.invalid", "The destination already exists.");
    }

    var destinationDirectory = Path.GetDirectoryName(destinationPath);
    if (string.IsNullOrWhiteSpace(destinationDirectory))
    {
      return RestoreFailure("workspace.file.invalid", "The destination path is invalid.");
    }

    var temporaryPath = $"{destinationPath}.{Guid.NewGuid():N}.restore.tmp";
    try
    {
      cancellationToken.ThrowIfCancellationRequested();
      using (fileSystem.CreateWriteProbe(destinationDirectory))
      {
      }

      var build = await BuildTemporaryDatabaseAsync(snapshot, temporaryPath, cancellationToken);
      if (build is OperationResult<Workspace>.Failure buildFailure)
      {
        return buildFailure;
      }

      var validation = await openWorkspaceAsync(temporaryPath, cancellationToken);
      if (validation is OperationResult<Workspace>.Failure validationFailure)
      {
        return validationFailure;
      }

      fileSystem.MoveWithoutOverwrite(temporaryPath, destinationPath);
      return await openWorkspaceAsync(destinationPath, cancellationToken);
    }
    catch (OperationCanceledException)
    {
      return RestoreFailure("workspace.restore.cancelled", "Workspace restore was cancelled.");
    }
    catch (IOException)
    {
      return RestoreFailure("workspace.restore.failed", "The Workspace could not be restored.");
    }
    catch (UnauthorizedAccessException)
    {
      return RestoreFailure("workspace.access.denied", "Access to the destination was denied.");
    }
    catch (WorkspaceSchemaStructureException)
    {
      return RestoreFailure("workspace.restore.failed", "The Workspace could not be restored.");
    }
    catch (WorkspaceSchemaIntegrityException)
    {
      return RestoreFailure("workspace.file.corrupt", "The restored Workspace is invalid.");
    }
    catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6)
    {
      return RestoreFailure("workspace.file.locked", "The destination is locked.");
    }
    catch (SqliteException)
    {
      return RestoreFailure("workspace.restore.failed", "The Workspace could not be restored.");
    }
    finally
    {
      try
      {
        deleteOwnedFile(temporaryPath);
      }
      catch (IOException)
      {
      }
      catch (UnauthorizedAccessException)
      {
      }
    }
  }

  public async Task<OperationResult<Workspace>> ReplaceAsync(
      WorkspaceTransferSnapshot snapshot,
      string destinationPath,
      CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(snapshot);

    if (backupService is null)
    {
      throw new InvalidOperationException("Workspace replacement requires a backup service.");
    }

    var validationErrors = WorkspaceTransferValidation.Validate(snapshot);
    if (validationErrors.Count > 0)
    {
      return new OperationResult<Workspace>.Failure(validationErrors[0]);
    }

    if (!File.Exists(destinationPath))
    {
      return RestoreFailure("workspace.file.invalid", "The destination Workspace does not exist.");
    }

    var destinationDirectory = Path.GetDirectoryName(destinationPath);
    if (string.IsNullOrWhiteSpace(destinationDirectory))
    {
      return RestoreFailure("workspace.file.invalid", "The destination path is invalid.");
    }

    var temporaryPath = $"{destinationPath}.{Guid.NewGuid():N}.restore.tmp";
    var recoveryPath = $"{destinationPath}.{Guid.NewGuid():N}.replace.recovery";
    string? managedBackupPath = null;
    var replacementBlocked = false;
    try
    {
      cancellationToken.ThrowIfCancellationRequested();

      var backup = await backupService.CreateAsync(
          destinationPath,
          snapshot.Workspace.Id,
          BackupReason.PreRestore,
          cancellationToken);
      if (backup is OperationResult<string>.Failure backupFailure)
      {
        return new OperationResult<Workspace>.Failure(backupFailure.Error);
      }

      managedBackupPath = ((OperationResult<string>.Success)backup).Value;

      var build = await BuildTemporaryDatabaseAsync(snapshot, temporaryPath, cancellationToken);
      if (build is OperationResult<Workspace>.Failure buildFailure)
      {
        return buildFailure;
      }

      cancellationToken.ThrowIfCancellationRequested();

      try
      {
        fileSystem.ReplaceWithRecovery(temporaryPath, destinationPath, recoveryPath);
      }
      catch (WorkspaceReplacementException exception)
      {
        replacementBlocked = true;
        return ReplacementBlockedFailure(exception.RecoveryPath);
      }
      catch (IOException)
      {
        return RestoreFailure("workspace.restore.failed", "The Workspace could not be replaced.");
      }

      var opened = await openWorkspaceAsync(destinationPath, CancellationToken.None);
      if (opened is OperationResult<Workspace>.Failure openFailure)
      {
        await TryRestoreFromRecoveryAsync(destinationPath, recoveryPath);
        return openFailure;
      }

      try
      {
        deleteOwnedFile(recoveryPath);
      }
      catch (IOException)
      {
      }
      catch (UnauthorizedAccessException)
      {
      }

      try
      {
        await backupService.CleanupAsync(snapshot.Workspace.Id, CancellationToken.None);
      }
      catch
      {
      }

      return opened;
    }
    catch (OperationCanceledException)
    {
      return RestoreFailure("workspace.restore.cancelled", "Workspace restore was cancelled.");
    }
    catch (IOException)
    {
      return RestoreFailure("workspace.restore.failed", "The Workspace could not be replaced.");
    }
    catch (UnauthorizedAccessException)
    {
      return RestoreFailure("workspace.access.denied", "Access to the destination was denied.");
    }
    catch (WorkspaceSchemaStructureException)
    {
      return RestoreFailure("workspace.restore.failed", "The Workspace could not be replaced.");
    }
    catch (WorkspaceSchemaIntegrityException)
    {
      return RestoreFailure("workspace.file.corrupt", "The restored Workspace is invalid.");
    }
    catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6)
    {
      return RestoreFailure("workspace.file.locked", "The destination is locked.");
    }
    catch (SqliteException)
    {
      return RestoreFailure("workspace.restore.failed", "The Workspace could not be replaced.");
    }
    finally
    {
      try
      {
        deleteOwnedFile(temporaryPath);
      }
      catch (IOException)
      {
      }
      catch (UnauthorizedAccessException)
      {
      }

      if (!replacementBlocked
          && managedBackupPath is not null
          && !File.Exists(destinationPath)
          && File.Exists(managedBackupPath))
      {
        try
        {
          File.Copy(managedBackupPath, destinationPath, overwrite: false);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
      }
    }
  }

  private async Task<OperationResult<Workspace>> BuildTemporaryDatabaseAsync(
      WorkspaceTransferSnapshot snapshot,
      string temporaryPath,
      CancellationToken cancellationToken)
  {
    var workspace = CreateWorkspace(snapshot.Workspace);
    if (workspace is OperationResult<Workspace>.Failure workspaceFailure)
    {
      return workspaceFailure;
    }

    await using (var connection = await connectionFactory.OpenAsync(temporaryPath, cancellationToken))
    {
      await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
      try
      {
        await SchemaV2.InitializeAsync(
            connection,
            transaction,
            ((OperationResult<Workspace>.Success)workspace).Value,
            CancellationToken.None);

        await InsertModsAsync(connection, transaction, snapshot.Mods, cancellationToken);
        afterPersistStage?.Invoke("mods");

        await InsertSourceReferencesAsync(connection, transaction, snapshot.SourceReferences, cancellationToken);
        afterPersistStage?.Invoke("sourceReferences");

        await InsertCollectionsAsync(connection, transaction, snapshot.Collections, cancellationToken);
        afterPersistStage?.Invoke("collections");

        await InsertMembershipsAsync(connection, transaction, snapshot.Memberships, cancellationToken);
        afterPersistStage?.Invoke("memberships");

        await WorkspaceSchemaInspector.ValidateAsync(
            connection,
            transaction,
            SchemaVersion.Current,
            CancellationToken.None);

        await transaction.CommitAsync(CancellationToken.None);
      }
      catch
      {
        await transaction.RollbackAsync(CancellationToken.None);
        throw;
      }
    }

    return workspace;
  }

  private static OperationResult<Workspace> CreateWorkspace(WorkspaceTransferWorkspace workspaceRow)
  {
    if (WorkspaceId.Parse(workspaceRow.Id) is not ValidationResult<WorkspaceId>.Success id ||
        WorkspaceName.Create(workspaceRow.DisplayName) is not ValidationResult<WorkspaceName>.Success name ||
        Workspace.Create(id.Value, name.Value, workspaceRow.CreatedUtc, workspaceRow.ModifiedUtc)
            is not ValidationResult<Workspace>.Success workspace)
    {
      return new OperationResult<Workspace>.Failure(new OperationError(
          "workspace.transfer.identity.invalid",
          "The Workspace identity is invalid.",
          false,
          "Choose a different export and try again."));
    }

    return new OperationResult<Workspace>.Success(workspace.Value);
  }

  private static async Task InsertModsAsync(
      SqliteConnection connection,
      SqliteTransaction transaction,
      IReadOnlyList<WorkspaceTransferMod> mods,
      CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = "INSERT INTO mods (id, display_name) VALUES ($id, $displayName);";
    var id = command.CreateParameter();
    id.ParameterName = "$id";
    command.Parameters.Add(id);
    var displayName = command.CreateParameter();
    displayName.ParameterName = "$displayName";
    command.Parameters.Add(displayName);
    foreach (var mod in mods)
    {
      id.Value = mod.Id;
      displayName.Value = mod.DisplayName;
      await command.ExecuteNonQueryAsync(cancellationToken);
    }
  }

  private static async Task InsertSourceReferencesAsync(
      SqliteConnection connection,
      SqliteTransaction transaction,
      IReadOnlyList<WorkspaceTransferSourceReference> sourceReferences,
      CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = """
        INSERT INTO source_references (source_type, external_id, mod_id)
        VALUES ($sourceType, $externalId, $modId);
        """;
    var sourceType = command.CreateParameter();
    sourceType.ParameterName = "$sourceType";
    command.Parameters.Add(sourceType);
    var externalId = command.CreateParameter();
    externalId.ParameterName = "$externalId";
    command.Parameters.Add(externalId);
    var modId = command.CreateParameter();
    modId.ParameterName = "$modId";
    command.Parameters.Add(modId);
    foreach (var reference in sourceReferences)
    {
      sourceType.Value = reference.SourceType;
      externalId.Value = reference.ExternalId;
      modId.Value = reference.ModId;
      await command.ExecuteNonQueryAsync(cancellationToken);
    }
  }

  private static async Task InsertCollectionsAsync(
      SqliteConnection connection,
      SqliteTransaction transaction,
      IReadOnlyList<WorkspaceTransferCollection> collections,
      CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = "INSERT INTO collections (id, display_name) VALUES ($id, $displayName);";
    var id = command.CreateParameter();
    id.ParameterName = "$id";
    command.Parameters.Add(id);
    var displayName = command.CreateParameter();
    displayName.ParameterName = "$displayName";
    command.Parameters.Add(displayName);
    foreach (var collection in collections)
    {
      id.Value = collection.Id;
      displayName.Value = collection.DisplayName;
      await command.ExecuteNonQueryAsync(cancellationToken);
    }
  }

  private static async Task InsertMembershipsAsync(
      SqliteConnection connection,
      SqliteTransaction transaction,
      IReadOnlyList<WorkspaceTransferMembership> memberships,
      CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = """
        INSERT INTO collection_memberships (collection_id, mod_id, position)
        VALUES ($collectionId, $modId, $position);
        """;
    var collectionId = command.CreateParameter();
    collectionId.ParameterName = "$collectionId";
    command.Parameters.Add(collectionId);
    var modId = command.CreateParameter();
    modId.ParameterName = "$modId";
    command.Parameters.Add(modId);
    var position = command.CreateParameter();
    position.ParameterName = "$position";
    command.Parameters.Add(position);
    foreach (var membership in memberships)
    {
      collectionId.Value = membership.CollectionId;
      modId.Value = membership.ModId;
      position.Value = membership.Position;
      await command.ExecuteNonQueryAsync(cancellationToken);
    }
  }

  private async Task TryRestoreFromRecoveryAsync(string destinationPath, string recoveryPath)
  {
    if (!File.Exists(recoveryPath))
    {
      return;
    }

    try
    {
      if (File.Exists(destinationPath))
      {
        File.Delete(destinationPath);
      }

      fileSystem.MoveWithoutOverwrite(recoveryPath, destinationPath);
    }
    catch (IOException)
    {
    }
    catch (UnauthorizedAccessException)
    {
    }
  }

  private static async Task<int> ReadSchemaVersionAsync(
      SqliteConnection connection,
      SqliteTransaction transaction,
      CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = "SELECT schema_version FROM application_metadata WHERE singleton = 1;";
    var value = await command.ExecuteScalarAsync(cancellationToken);
    return Convert.ToInt32(value, CultureInfo.InvariantCulture);
  }

  private static async Task<WorkspaceTransferWorkspace?> ReadWorkspaceRowAsync(
      SqliteConnection connection,
      SqliteTransaction transaction,
      CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = """
        SELECT id, display_name, created_utc, modified_utc
        FROM workspace WHERE singleton = 1;
        """;
    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    if (!await reader.ReadAsync(cancellationToken))
    {
      return null;
    }

    if (!DateTimeOffset.TryParse(reader.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var created) ||
        !DateTimeOffset.TryParse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var modified))
    {
      return null;
    }

    return new WorkspaceTransferWorkspace(
        reader.GetString(0),
        reader.GetString(1),
        created,
        modified);
  }

  private static async Task<IReadOnlyList<WorkspaceTransferMod>> ReadModsAsync(
      SqliteConnection connection,
      SqliteTransaction transaction,
      CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = "SELECT id, display_name FROM mods ORDER BY id;";
    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    var mods = new List<WorkspaceTransferMod>();
    while (await reader.ReadAsync(cancellationToken))
    {
      mods.Add(new WorkspaceTransferMod(reader.GetString(0), reader.GetString(1)));
    }

    return mods;
  }

  private static async Task<IReadOnlyList<WorkspaceTransferSourceReference>> ReadSourceReferencesAsync(
      SqliteConnection connection,
      SqliteTransaction transaction,
      CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = """
        SELECT source_type, external_id, mod_id
        FROM source_references ORDER BY source_type, external_id;
        """;
    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    var references = new List<WorkspaceTransferSourceReference>();
    while (await reader.ReadAsync(cancellationToken))
    {
      references.Add(new WorkspaceTransferSourceReference(
          reader.GetString(0),
          reader.GetString(1),
          reader.GetString(2)));
    }

    return references;
  }

  private static async Task<IReadOnlyList<WorkspaceTransferCollection>> ReadCollectionsAsync(
      SqliteConnection connection,
      SqliteTransaction transaction,
      CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = "SELECT id, display_name FROM collections ORDER BY id;";
    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    var collections = new List<WorkspaceTransferCollection>();
    while (await reader.ReadAsync(cancellationToken))
    {
      collections.Add(new WorkspaceTransferCollection(reader.GetString(0), reader.GetString(1)));
    }

    return collections;
  }

  private static async Task<IReadOnlyList<WorkspaceTransferMembership>> ReadMembershipsAsync(
      SqliteConnection connection,
      SqliteTransaction transaction,
      CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = """
        SELECT collection_id, mod_id, position
        FROM collection_memberships ORDER BY collection_id, position, mod_id;
        """;
    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    var memberships = new List<WorkspaceTransferMembership>();
    while (await reader.ReadAsync(cancellationToken))
    {
      memberships.Add(new WorkspaceTransferMembership(
          reader.GetString(0),
          reader.GetString(1),
          reader.GetInt32(2)));
    }

    return memberships;
  }

  private static OperationResult<WorkspaceTransferSnapshot>.Failure MapWorkspaceFailure(
      OperationResult<Workspace>.Failure failure) =>
      new(failure.Error);

  private static OperationResult<WorkspaceTransferSnapshot>.Failure SnapshotFailure(string code, string message) =>
      new(new OperationError(code, message, false, "Return Home and retry the operation."));

  private static OperationResult<string>.Failure ExportFailure(string code, string message) =>
      new(new OperationError(code, message, false, "Return Home and retry the operation."));

  private static OperationResult<Workspace>.Failure RestoreFailure(string code, string message) =>
      new(new OperationError(code, message, false, "Return Home and retry the operation."));

  private static OperationResult<Workspace>.Failure ReplacementBlockedFailure(string recoveryPath) =>
      new(new OperationError(
          "workspace.restore.replacement.blocked",
          "The Workspace could not be restored safely.",
          true,
          $"Do not overwrite the retained recovery file at {recoveryPath}. Use that copy to recover the Workspace."));
}
