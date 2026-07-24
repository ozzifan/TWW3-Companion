using Microsoft.Data.Sqlite;
using Tww3Companion.Application.Abstractions;
using Tww3Companion.Application.Common;
using Tww3Companion.Application.Importing;
using Tww3Companion.Application.Workspaces;
using Tww3Companion.Domain.Workspaces;
using Tww3Companion.Infrastructure.Settings;
using Tww3Companion.Infrastructure.Storage.Schema;

namespace Tww3Companion.Infrastructure.Storage;

public sealed class SqliteWorkspaceCatalogStore :
    IWorkspaceImportStore,
    IWorkspaceCatalogReader
{
  private const string SteamWorkshopSourceType = "steam-workshop";

  private readonly SqliteConnectionFactory connectionFactory;
  private readonly IUuidGenerator uuidGenerator;
  private readonly IClock clock;
  private readonly WorkspaceFileValidator validator;
  private readonly IAtomicFileSystem fileSystem;
  private readonly Action<string> deleteOwnedFile;
  private readonly Action<int>? afterCandidatePersisted;

  public SqliteWorkspaceCatalogStore(
      SqliteConnectionFactory connectionFactory,
      IUuidGenerator uuidGenerator,
      IClock clock,
      WorkspaceFileValidator? validator = null,
      IAtomicFileSystem? fileSystem = null,
      Action<string>? deleteOwnedFile = null,
      Action<int>? afterCandidatePersisted = null)
  {
    this.connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    this.uuidGenerator = uuidGenerator ?? throw new ArgumentNullException(nameof(uuidGenerator));
    this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    this.validator = validator ?? new WorkspaceFileValidator(this.connectionFactory);
    this.fileSystem = fileSystem ?? new AtomicFileSystem();
    this.deleteOwnedFile = deleteOwnedFile ?? File.Delete;
    this.afterCandidatePersisted = afterCandidatePersisted;
  }

  public Task<ImportPreview> SavePreviewAsync(
      ImportTargetContext targetContext,
      IReadOnlyList<ImportCandidate> candidates,
      IReadOnlyList<ImportResolution> resolutions,
      CancellationToken cancellationToken = default) =>
      Task.FromResult(new ImportPreview(
          targetContext,
          candidates,
          Applied: false,
          Resolutions: resolutions,
          ValidationIssues: []));

  public async Task<IReadOnlyList<ImportCandidate>> ReadCandidatesAsync(
      ImportTargetContext targetContext,
      CancellationToken cancellationToken = default)
  {
    if (targetContext is not ImportTargetContext.CurrentWorkspace current)
    {
      throw new ArgumentException("The import target must be the current Workspace.", nameof(targetContext));
    }

    await using var connection = await OpenValidatedConnectionAsync(
        current.WorkspacePath,
        current.WorkspaceId,
        requireCollection: false,
        cancellationToken);
    var sourceReferences = await ReadSourceReferencesByModIdAsync(connection, cancellationToken);
    var candidates = new List<ImportCandidate>();
    await using var command = connection.CreateCommand();
    command.CommandText = """
        SELECT id, display_name
        FROM mods;
        """;
    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    while (await reader.ReadAsync(cancellationToken))
    {
      var modId = reader.GetString(0);
      var displayName = reader.GetString(1);
      sourceReferences.TryGetValue(modId, out var sourceReference);
      candidates.Add(new ImportCandidate(
          $"catalog:{modId}",
          sourceReference,
          modId,
          displayName,
          IsSkipped: false));
    }

    return candidates;
  }

  public async Task<bool> ModExistsAsync(
      ImportTargetContext.CurrentWorkspace targetContext,
      string modId,
      CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(modId);

    await using var connection = await OpenValidatedConnectionAsync(
        targetContext.WorkspacePath,
        targetContext.WorkspaceId,
        requireCollection: false,
        cancellationToken);
    await using var command = connection.CreateCommand();
    command.CommandText = "SELECT EXISTS(SELECT 1 FROM mods WHERE id = $modId);";
    command.Parameters.AddWithValue("$modId", modId);
    return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture) == 1L;
  }

  public async Task<ImportOutcome> CommitAtomicallyAsync(
      ImportPreview preview,
      bool confirm,
      CancellationToken cancellationToken = default)
  {
    if (!confirm)
    {
      return new ImportOutcome(
          preview.TargetContext,
          preview.Candidates,
          Applied: false);
    }

    if (preview.TargetContext is not ImportTargetContext.CurrentWorkspace current)
    {
      throw new ArgumentException("The preview must target the current Workspace.", nameof(preview));
    }

    ValidatePreview(preview);

    try
    {
      cancellationToken.ThrowIfCancellationRequested();

      await using var connection = await OpenValidatedConnectionAsync(
          current.WorkspacePath,
          current.WorkspaceId,
          requireCollection: true,
          cancellationToken,
          current.CollectionId);
      await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
      var persistedCandidateCount = 0;

      try
      {
        await VerifyWorkspaceIdAsync(connection, transaction, current.WorkspaceId, CancellationToken.None);
        await VerifyCollectionExistsAsync(connection, transaction, current.CollectionId, CancellationToken.None);

        var nextPosition = await ReadNextMembershipPositionAsync(
            connection,
            transaction,
            current.CollectionId,
            CancellationToken.None);

        foreach (var candidate in preview.Candidates.OfType<ImportCandidate>())
        {
          cancellationToken.ThrowIfCancellationRequested();
          if (candidate.IsSkipped)
          {
            continue;
          }

          var modId = await ResolveOrCreateModAsync(
              connection,
              transaction,
              candidate,
              CancellationToken.None);
          nextPosition = await EnsureMembershipAsync(
              connection,
              transaction,
              current.CollectionId,
              modId,
              nextPosition,
              CancellationToken.None);

          persistedCandidateCount++;
          afterCandidatePersisted?.Invoke(persistedCandidateCount);
        }

        await transaction.CommitAsync(CancellationToken.None);
        return new ImportOutcome(preview.TargetContext, preview.Candidates, Applied: true);
      }
      catch
      {
        await transaction.RollbackAsync(CancellationToken.None);
        throw;
      }
    }
    catch (Exception exception)
    {
      throw MapPersistenceFailure(exception);
    }
  }

  public Task<ImportOutcome> CommitNewWorkspaceAtomicallyAsync(
      ImportPreview preview,
      CancellationToken cancellationToken = default) =>
      throw new NotImplementedException("New-Workspace atomic import is implemented in Task 4.");

  public async Task<WorkspaceLibrarySnapshot> ReadLibrarySnapshotAsync(
      string workspacePath,
      CancellationToken cancellationToken = default)
  {
    try
    {
      await EnsureReadableWorkspaceAsync(workspacePath, cancellationToken);
      await using var connection = await connectionFactory.OpenAsync(workspacePath, cancellationToken);

      var mods = new List<WorkspaceLibraryMod>();
      await using (var command = connection.CreateCommand())
      {
        command.CommandText = """
            SELECT id, display_name
            FROM mods
            ORDER BY display_name COLLATE NOCASE, id;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
          mods.Add(new WorkspaceLibraryMod(reader.GetString(0), reader.GetString(1)));
        }
      }

      var collections = new List<WorkspaceCollection>();
      await using (var command = connection.CreateCommand())
      {
        command.CommandText = """
            SELECT id, display_name
            FROM collections
            ORDER BY display_name COLLATE NOCASE, id;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
          collections.Add(new WorkspaceCollection(reader.GetString(0), reader.GetString(1)));
        }
      }

      var memberships = new List<WorkspaceCollectionMembership>();
      await using (var command = connection.CreateCommand())
      {
        command.CommandText = """
            SELECT collection_id, mod_id
            FROM collection_memberships
            ORDER BY collection_id, position, mod_id;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
          memberships.Add(new WorkspaceCollectionMembership(reader.GetString(0), reader.GetString(1)));
        }
      }

      return new WorkspaceLibrarySnapshot(mods, collections, memberships);
    }
    catch (Exception exception)
    {
      throw MapPersistenceFailure(exception);
    }
  }

  private async Task<SqliteConnection> OpenValidatedConnectionAsync(
      string workspacePath,
      string expectedWorkspaceId,
      bool requireCollection,
      CancellationToken cancellationToken,
      string? collectionId = null)
  {
    try
    {
      await EnsureReadableWorkspaceAsync(workspacePath, cancellationToken);
      var connection = await connectionFactory.OpenAsync(workspacePath, cancellationToken);
      try
      {
        await VerifyWorkspaceIdAsync(connection, null, expectedWorkspaceId, cancellationToken);
        if (requireCollection)
        {
          await VerifyCollectionExistsAsync(connection, null, collectionId!, cancellationToken);
        }

        return connection;
      }
      catch
      {
        await connection.DisposeAsync();
        throw;
      }
    }
    catch (Exception exception)
    {
      throw MapPersistenceFailure(exception);
    }
  }

  private async Task EnsureReadableWorkspaceAsync(string workspacePath, CancellationToken cancellationToken)
  {
    var validation = await validator.OpenAsync(workspacePath, cancellationToken);
    if (validation is OperationResult<Workspace>.Failure failure)
    {
      throw MapWorkspaceFailure(failure.Error);
    }

    var version = await validator.ReadSchemaVersionAsync(workspacePath, cancellationToken);
    if (version != SchemaVersion.Current)
    {
      throw ImportError(
          "import.workspace.schema.unsupported",
          "The Workspace is not ready for catalog imports.",
          "Open the Workspace and try again after migration completes.");
    }
  }

  private static async Task VerifyWorkspaceIdAsync(
      SqliteConnection connection,
      SqliteTransaction? transaction,
      string expectedWorkspaceId,
      CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = "SELECT id FROM workspace WHERE singleton = 1;";
    var storedId = await command.ExecuteScalarAsync(cancellationToken) as string;
    if (storedId is null ||
        !string.Equals(
            CanonicalizeUuid(storedId),
            CanonicalizeUuid(expectedWorkspaceId),
            StringComparison.Ordinal))
    {
      throw ImportError(
          "import.workspace.mismatch",
          "The selected file is not the expected Workspace.",
          "Return to the Workspace and choose the import destination again.");
    }
  }

  private static async Task VerifyCollectionExistsAsync(
      SqliteConnection connection,
      SqliteTransaction? transaction,
      string collectionId,
      CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = """
        SELECT EXISTS(
            SELECT 1
            FROM collections
            WHERE id = $collectionId);
        """;
    command.Parameters.AddWithValue("$collectionId", collectionId);
    var exists = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture) == 1L;
    if (!exists)
    {
      throw ImportError(
          "import.collection.missing",
          "The selected Collection no longer exists in this Workspace.",
          "Return to the Workspace and choose the import destination again.");
    }
  }

  private static async Task<int> ReadNextMembershipPositionAsync(
      SqliteConnection connection,
      SqliteTransaction transaction,
      string collectionId,
      CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = """
        SELECT COALESCE(MAX(position), -1)
        FROM collection_memberships
        WHERE collection_id = $collectionId;
        """;
    command.Parameters.AddWithValue("$collectionId", collectionId);
    var maxPosition = Convert.ToInt32(
        await command.ExecuteScalarAsync(cancellationToken),
        System.Globalization.CultureInfo.InvariantCulture);
    return maxPosition + 1;
  }

  private async Task<string> ResolveOrCreateModAsync(
      SqliteConnection connection,
      SqliteTransaction transaction,
      ImportCandidate candidate,
      CancellationToken cancellationToken)
  {
    if (candidate.SourceReference is not null)
    {
      var existingOwner = await ReadSourceOwnerAsync(
          connection,
          transaction,
          candidate.SourceReference,
          cancellationToken);
      if (existingOwner is not null)
      {
        if (!string.IsNullOrWhiteSpace(candidate.LinkedModId) &&
            !string.Equals(existingOwner, candidate.LinkedModId, StringComparison.Ordinal))
        {
          throw ImportError(
              "import.source.owner.conflict",
              "The source identity is already owned by another Mod.",
              "Resolve the conflicting candidate in the import preview and try again.");
        }

        return existingOwner;
      }
    }

    if (!string.IsNullOrWhiteSpace(candidate.LinkedModId))
    {
      if (!await ModExistsInTransactionAsync(connection, transaction, candidate.LinkedModId, cancellationToken))
      {
        throw new InvalidOperationException("All linked import candidates must resolve existing Mods before applying.");
      }

      if (candidate.SourceReference is not null)
      {
        await InsertSourceReferenceAsync(
            connection,
            transaction,
            candidate.SourceReference,
            candidate.LinkedModId,
            cancellationToken);
      }

      return candidate.LinkedModId;
    }

    if (string.IsNullOrWhiteSpace(candidate.DisplayName))
    {
      throw new InvalidOperationException("All required import candidates must be resolved before applying.");
    }

    var modId = uuidGenerator.NewUuid();
    await using (var command = connection.CreateCommand())
    {
      command.Transaction = transaction;
      command.CommandText = """
          INSERT INTO mods (id, display_name)
          VALUES ($id, $displayName);
          """;
      command.Parameters.AddWithValue("$id", modId);
      command.Parameters.AddWithValue("$displayName", candidate.DisplayName.Trim());
      await command.ExecuteNonQueryAsync(cancellationToken);
    }

    if (candidate.SourceReference is not null)
    {
      await InsertSourceReferenceAsync(
          connection,
          transaction,
          candidate.SourceReference,
          modId,
          cancellationToken);
    }

    return modId;
  }

  private static async Task InsertSourceReferenceAsync(
      SqliteConnection connection,
      SqliteTransaction transaction,
      ImportSourceReference sourceReference,
      string modId,
      CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = """
        INSERT INTO source_references (source_type, external_id, mod_id)
        VALUES ($sourceType, $externalId, $modId);
        """;
    command.Parameters.AddWithValue("$sourceType", ToStoredSourceType(sourceReference.SourceType));
    command.Parameters.AddWithValue("$externalId", sourceReference.ExternalId);
    command.Parameters.AddWithValue("$modId", modId);
    try
    {
      await command.ExecuteNonQueryAsync(cancellationToken);
    }
    catch (SqliteException exception) when (exception.SqliteErrorCode is 19)
    {
      var owner = await ReadSourceOwnerAsync(connection, transaction, sourceReference, cancellationToken);
      if (owner is not null && !string.Equals(owner, modId, StringComparison.Ordinal))
      {
        throw ImportError(
            "import.source.owner.conflict",
            "The source identity is already owned by another Mod.",
            "Resolve the conflicting candidate in the import preview and try again.");
      }

      throw;
    }
  }

  private static async Task<int> EnsureMembershipAsync(
      SqliteConnection connection,
      SqliteTransaction transaction,
      string collectionId,
      string modId,
      int nextPosition,
      CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = """
        INSERT INTO collection_memberships(
            collection_id,
            mod_id,
            position)
        SELECT $collectionId, $modId, $position
        WHERE NOT EXISTS(
            SELECT 1
            FROM collection_memberships
            WHERE collection_id = $collectionId
              AND mod_id = $modId);
        """;
    command.Parameters.AddWithValue("$collectionId", collectionId);
    command.Parameters.AddWithValue("$modId", modId);
    command.Parameters.AddWithValue("$position", nextPosition);
    var inserted = await command.ExecuteNonQueryAsync(cancellationToken);
    return inserted == 1 ? nextPosition + 1 : nextPosition;
  }

  private static async Task<string?> ReadSourceOwnerAsync(
      SqliteConnection connection,
      SqliteTransaction? transaction,
      ImportSourceReference sourceReference,
      CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = """
        SELECT mod_id
        FROM source_references
        WHERE source_type = $sourceType
          AND external_id = $externalId;
        """;
    command.Parameters.AddWithValue("$sourceType", ToStoredSourceType(sourceReference.SourceType));
    command.Parameters.AddWithValue("$externalId", sourceReference.ExternalId);
    return await command.ExecuteScalarAsync(cancellationToken) as string;
  }

  private static async Task<bool> ModExistsInTransactionAsync(
      SqliteConnection connection,
      SqliteTransaction transaction,
      string modId,
      CancellationToken cancellationToken)
  {
    await using var command = connection.CreateCommand();
    command.Transaction = transaction;
    command.CommandText = "SELECT EXISTS(SELECT 1 FROM mods WHERE id = $modId);";
    command.Parameters.AddWithValue("$modId", modId);
    return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture) == 1L;
  }

  private static async Task<Dictionary<string, ImportSourceReference>> ReadSourceReferencesByModIdAsync(
      SqliteConnection connection,
      CancellationToken cancellationToken)
  {
    var sourceReferences = new Dictionary<string, ImportSourceReference>(StringComparer.Ordinal);
    await using var command = connection.CreateCommand();
    command.CommandText = """
        SELECT source_type, external_id, mod_id
        FROM source_references;
        """;
    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
    while (await reader.ReadAsync(cancellationToken))
    {
      var sourceType = reader.GetString(0);
      var externalId = reader.GetString(1);
      var modId = reader.GetString(2);
      sourceReferences[modId] = new ImportSourceReference(
          ParseStoredSourceType(sourceType),
          externalId);
    }

    return sourceReferences;
  }

  private static ImportSourceType ParseStoredSourceType(string storedSourceType) =>
      storedSourceType switch
      {
        SteamWorkshopSourceType => ImportSourceType.SteamWorkshop,
        _ => throw ImportError(
            "workspace.file.invalid",
            "The Workspace contains an unsupported source reference type.",
            "Return Home and choose another Workspace file.")
      };

  private static string ToStoredSourceType(ImportSourceType sourceType) =>
      sourceType switch
      {
        ImportSourceType.SteamWorkshop => SteamWorkshopSourceType,
        _ => throw new ArgumentOutOfRangeException(nameof(sourceType), sourceType, "Unsupported import source type.")
      };

  private static string CanonicalizeUuid(string value) => value.Trim().ToLowerInvariant();

  private static void ValidatePreview(ImportPreview preview)
  {
    if (preview.ValidationIssues?.Count > 0)
    {
      throw new InvalidOperationException("The import preview contains validation issues.");
    }

    if (preview.Candidates.Any(candidate => candidate is not ImportCandidate importCandidate ||
        (importCandidate.IsSkipped && preview.Resolutions?.Any(resolution =>
            resolution.CandidateId == importCandidate.CandidateId && resolution.CanSkip) != true) ||
        (!importCandidate.IsSkipped && string.IsNullOrWhiteSpace(importCandidate.LinkedModId) &&
            string.IsNullOrWhiteSpace(importCandidate.DisplayName))))
    {
      throw new InvalidOperationException("All required import candidates must be resolved before applying.");
    }
  }

  private static ImportPersistenceException MapWorkspaceFailure(OperationError error) =>
      new(new OperationError(
          error.Code,
          error.Message,
          PersistentChangeCommitted: false,
          error.SafeNextAction));

  private static ImportPersistenceException ImportError(
      string code,
      string message,
      string safeNextAction) =>
      new(new OperationError(code, message, PersistentChangeCommitted: false, safeNextAction));

  private static Exception MapPersistenceFailure(Exception exception) =>
      exception switch
      {
        ImportPersistenceException => exception,
        OperationCanceledException => ImportError(
            "import.persistence.cancelled",
            "The import was cancelled before it could finish.",
            "Try the import again."),
        UnauthorizedAccessException => ImportError(
            "workspace.access.denied",
            "Access to the Workspace was denied.",
            "Return Home and choose another Workspace file."),
        IOException => ImportError(
            "workspace.file.locked",
            "The Workspace file is locked.",
            "Close other applications using the file and try again."),
        SqliteException { SqliteErrorCode: 5 or 6 } => ImportError(
            "workspace.file.locked",
            "The Workspace database is locked.",
            "Close other applications using the file and try again."),
        SqliteException { SqliteErrorCode: 11 or 26 } => ImportError(
            "workspace.file.corrupt",
            "The Workspace database is corrupt.",
            "Return Home and choose another Workspace file."),
        SqliteException => ImportError(
            "import.persistence.failed",
            "The import could not be completed.",
            "Review the import preview and try again."),
        _ => exception
      };
}
