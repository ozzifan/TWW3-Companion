using Microsoft.Data.Sqlite;
using Tww3Companion.Application.Abstractions;
using Tww3Companion.Application.Common;
using Tww3Companion.Application.Importing;
using Tww3Companion.Domain.Validation;
using Tww3Companion.Domain.Workspaces;
using Tww3Companion.Infrastructure.Settings;
using Tww3Companion.Infrastructure.Storage;
using Xunit;

namespace Tww3Companion.Infrastructure.Tests.Storage;

public sealed class SqliteWorkspaceCatalogStoreTests
{
  [Fact]
  public async Task CurrentImport_PersistsAndReloadsCatalog()
  {
    await using var fixture = await CatalogWorkspaceFixture.CreateAsync();
    var store = fixture.Store;
    var target = ImportTargetContext.ForCurrentWorkspace(
        fixture.WorkspaceId,
        fixture.WorkspacePath,
        fixture.FirstCollectionId);
    var preview = await store.SavePreviewAsync(
        target,
        [
          ImportCandidate.CreateWithDisplayName(
              "candidate:111",
              "First Mod",
              ImportSourceReference.SteamWorkshop("111")),
          ImportCandidate.CreateWithDisplayName(
              "candidate:local",
              "Local Mod")
        ],
        [],
        TestContext.Current.CancellationToken);

    var outcome = await store.CommitAtomicallyAsync(
        preview,
        confirm: true,
        TestContext.Current.CancellationToken);
    var reopened = await new SqliteWorkspaceCatalogStore(
        fixture.ConnectionFactory,
        fixture.UuidGenerator,
        fixture.Clock)
        .ReadLibrarySnapshotAsync(
            fixture.WorkspacePath,
            TestContext.Current.CancellationToken);

    Assert.True(outcome.Applied);
    Assert.Equal(2, reopened.Mods.Count);
    Assert.Equal(
        new[] { 0, 1 },
        await fixture.ReadMembershipPositionsAsync(
            fixture.FirstCollectionId));
  }

  [Fact]
  public async Task SavePreviewAsync_DoesNotCreateWorkspaceFileForNewTarget()
  {
    using var directory = new TemporaryDirectory();
    var destination = Path.Combine(directory.Path, "nonexistent.tww3c");
    var store = CreateStore();

    var preview = await store.SavePreviewAsync(
        ImportTargetContext.ForNewWorkspace("New Workspace", destination, "Imported Collection"),
        [ImportCandidate.CreateWithDisplayName("candidate:1", "Mod")],
        [],
        TestContext.Current.CancellationToken);

    Assert.False(preview.Applied);
    Assert.Empty(preview.ValidationIssues!);
    Assert.False(File.Exists(destination));
  }

  [Fact]
  public async Task CommitAtomicallyAsync_WrongWorkspaceId_ThrowsImportPersistenceException()
  {
    await using var fixture = await CatalogWorkspaceFixture.CreateAsync();
    var preview = await fixture.Store.SavePreviewAsync(
        ImportTargetContext.ForCurrentWorkspace(
            "87654321-4321-4abc-8def-1234567890ab",
            fixture.WorkspacePath,
            fixture.FirstCollectionId),
        [ImportCandidate.CreateWithDisplayName("candidate:1", "Mod")],
        [],
        TestContext.Current.CancellationToken);

    var exception = await Assert.ThrowsAsync<ImportPersistenceException>(() =>
        fixture.Store.CommitAtomicallyAsync(
            preview,
            confirm: true,
            TestContext.Current.CancellationToken));

    Assert.Equal("import.workspace.mismatch", exception.Error.Code);
    Assert.False(exception.Error.PersistentChangeCommitted);
  }

  [Fact]
  public async Task CommitAtomicallyAsync_MissingCollection_ThrowsImportPersistenceException()
  {
    await using var fixture = await CatalogWorkspaceFixture.CreateAsync();
    var preview = await fixture.Store.SavePreviewAsync(
        ImportTargetContext.ForCurrentWorkspace(
            fixture.WorkspaceId,
            fixture.WorkspacePath,
            "99999999-9999-4999-8999-999999999999"),
        [ImportCandidate.CreateWithDisplayName("candidate:1", "Mod")],
        [],
        TestContext.Current.CancellationToken);

    var exception = await Assert.ThrowsAsync<ImportPersistenceException>(() =>
        fixture.Store.CommitAtomicallyAsync(
            preview,
            confirm: true,
            TestContext.Current.CancellationToken));

    Assert.Equal("import.collection.missing", exception.Error.Code);
    Assert.False(exception.Error.PersistentChangeCommitted);
  }

  [Fact]
  public async Task CurrentImport_ExactSourceIdentityReusesModInSecondCollection()
  {
    await using var fixture = await CatalogWorkspaceFixture.CreateAsync();
    var firstTarget = ImportTargetContext.ForCurrentWorkspace(
        fixture.WorkspaceId,
        fixture.WorkspacePath,
        fixture.FirstCollectionId);
    var secondTarget = ImportTargetContext.ForCurrentWorkspace(
        fixture.WorkspaceId,
        fixture.WorkspacePath,
        fixture.SecondCollectionId);
    var candidate = ImportCandidate.CreateWithDisplayName(
        "candidate:111",
        "Shared Mod",
        ImportSourceReference.SteamWorkshop("111"));

    var firstPreview = await fixture.Store.SavePreviewAsync(
        firstTarget,
        [candidate],
        [],
        TestContext.Current.CancellationToken);
    Assert.True((await fixture.Store.CommitAtomicallyAsync(
        firstPreview,
        confirm: true,
        TestContext.Current.CancellationToken)).Applied);

    var secondPreview = await fixture.Store.SavePreviewAsync(
        secondTarget,
        [candidate with { CandidateId = "candidate:111-again" }],
        [],
        TestContext.Current.CancellationToken);
    Assert.True((await fixture.Store.CommitAtomicallyAsync(
        secondPreview,
        confirm: true,
        TestContext.Current.CancellationToken)).Applied);

    var snapshot = await fixture.Store.ReadLibrarySnapshotAsync(
        fixture.WorkspacePath,
        TestContext.Current.CancellationToken);

    Assert.Single(snapshot.Mods);
    Assert.Equal(2, snapshot.Memberships.Count);
    Assert.Contains(snapshot.Memberships, membership => membership.CollectionId == fixture.FirstCollectionId);
    Assert.Contains(snapshot.Memberships, membership => membership.CollectionId == fixture.SecondCollectionId);
  }

  [Fact]
  public async Task CurrentImport_ReimportSameCollectionRetainsMembershipAndPosition()
  {
    await using var fixture = await CatalogWorkspaceFixture.CreateAsync();
    var target = ImportTargetContext.ForCurrentWorkspace(
        fixture.WorkspaceId,
        fixture.WorkspacePath,
        fixture.FirstCollectionId);
    var candidate = ImportCandidate.CreateWithDisplayName(
        "candidate:111",
        "Shared Mod",
        ImportSourceReference.SteamWorkshop("111"));

    var firstPreview = await fixture.Store.SavePreviewAsync(
        target,
        [candidate],
        [],
        TestContext.Current.CancellationToken);
    Assert.True((await fixture.Store.CommitAtomicallyAsync(
        firstPreview,
        confirm: true,
        TestContext.Current.CancellationToken)).Applied);

    var secondPreview = await fixture.Store.SavePreviewAsync(
        target,
        [candidate with { CandidateId = "candidate:111-again" }],
        [],
        TestContext.Current.CancellationToken);
    Assert.True((await fixture.Store.CommitAtomicallyAsync(
        secondPreview,
        confirm: true,
        TestContext.Current.CancellationToken)).Applied);

    Assert.Equal(new[] { 0 }, await fixture.ReadMembershipPositionsAsync(fixture.FirstCollectionId));
    Assert.Equal(1L, await fixture.CountRowsAsync("mods"));
    Assert.Equal(1L, await fixture.CountRowsAsync("collection_memberships"));
  }

  [Fact]
  public async Task CurrentImport_NewMembershipsAppendAfterHighestExistingPosition()
  {
    await using var fixture = await CatalogWorkspaceFixture.CreateAsync();
    await fixture.InsertModWithMembershipAsync(
        "existing-mod",
        "Existing Mod",
        fixture.FirstCollectionId,
        position: 0);

    var target = ImportTargetContext.ForCurrentWorkspace(
        fixture.WorkspaceId,
        fixture.WorkspacePath,
        fixture.FirstCollectionId);
    var preview = await fixture.Store.SavePreviewAsync(
        target,
        [
          ImportCandidate.CreateWithDisplayName("candidate:1", "Second Mod"),
          ImportCandidate.CreateWithDisplayName("candidate:2", "Third Mod")
        ],
        [],
        TestContext.Current.CancellationToken);

    Assert.True((await fixture.Store.CommitAtomicallyAsync(
        preview,
        confirm: true,
        TestContext.Current.CancellationToken)).Applied);

    Assert.Equal(new[] { 0, 1, 2 }, await fixture.ReadMembershipPositionsAsync(fixture.FirstCollectionId));
  }

  [Fact]
  public async Task CurrentImport_SourceNeutralModHasNoSourceReferenceRow()
  {
    await using var fixture = await CatalogWorkspaceFixture.CreateAsync();
    var target = ImportTargetContext.ForCurrentWorkspace(
        fixture.WorkspaceId,
        fixture.WorkspacePath,
        fixture.FirstCollectionId);
    var preview = await fixture.Store.SavePreviewAsync(
        target,
        [ImportCandidate.CreateWithDisplayName("candidate:local", "Local Mod")],
        [],
        TestContext.Current.CancellationToken);

    Assert.True((await fixture.Store.CommitAtomicallyAsync(
        preview,
        confirm: true,
        TestContext.Current.CancellationToken)).Applied);

    Assert.Equal(0L, await fixture.CountRowsAsync("source_references"));
    Assert.Equal(1L, await fixture.CountRowsAsync("mods"));
  }

  [Fact]
  public async Task CommitAtomicallyAsync_ConflictingSourceOwner_ThrowsImportPersistenceException()
  {
    await using var fixture = await CatalogWorkspaceFixture.CreateAsync();
    await fixture.InsertModWithSourceReferenceAsync(
        "owned-mod",
        "Owned Mod",
        ImportSourceReference.SteamWorkshop("111"));
    await fixture.InsertModAsync("other-mod", "Other Mod");

    var preview = await fixture.Store.SavePreviewAsync(
        ImportTargetContext.ForCurrentWorkspace(
            fixture.WorkspaceId,
            fixture.WorkspacePath,
            fixture.FirstCollectionId),
        [ImportCandidate.Linked(
            "candidate:111",
            "other-mod",
            ImportSourceReference.SteamWorkshop("111"))],
        [],
        TestContext.Current.CancellationToken);

    var exception = await Assert.ThrowsAsync<ImportPersistenceException>(() =>
        fixture.Store.CommitAtomicallyAsync(
            preview,
            confirm: true,
            TestContext.Current.CancellationToken));

    Assert.Equal("import.source.owner.conflict", exception.Error.Code);
    Assert.False(exception.Error.PersistentChangeCommitted);
    Assert.Equal(0L, await fixture.CountRowsAsync("collection_memberships"));
  }

  [Fact]
  public async Task CommitAtomicallyAsync_FailureAfterFirstCandidateRollsBackAllRows()
  {
    await using var fixture = await CatalogWorkspaceFixture.CreateAsync();
    var store = new SqliteWorkspaceCatalogStore(
        fixture.ConnectionFactory,
        fixture.UuidGenerator,
        fixture.Clock,
        afterCandidatePersisted: count =>
        {
          if (count == 1)
          {
            throw new InvalidOperationException("Injected failure after first candidate.");
          }
        });
    var preview = await store.SavePreviewAsync(
        ImportTargetContext.ForCurrentWorkspace(
            fixture.WorkspaceId,
            fixture.WorkspacePath,
            fixture.FirstCollectionId),
        [
          ImportCandidate.CreateWithDisplayName("candidate:1", "First Mod"),
          ImportCandidate.CreateWithDisplayName("candidate:2", "Second Mod")
        ],
        [],
        TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<InvalidOperationException>(() =>
        store.CommitAtomicallyAsync(
            preview,
            confirm: true,
            TestContext.Current.CancellationToken));

    Assert.Equal(0L, await fixture.CountRowsAsync("mods"));
    Assert.Equal(0L, await fixture.CountRowsAsync("source_references"));
    Assert.Equal(0L, await fixture.CountRowsAsync("collection_memberships"));
  }

  [Fact]
  public async Task CommitAtomicallyAsync_PreCancelledApplyLeavesCatalogUnchanged()
  {
    await using var fixture = await CatalogWorkspaceFixture.CreateAsync();
    var preview = await fixture.Store.SavePreviewAsync(
        ImportTargetContext.ForCurrentWorkspace(
            fixture.WorkspaceId,
            fixture.WorkspacePath,
            fixture.FirstCollectionId),
        [ImportCandidate.CreateWithDisplayName("candidate:1", "Mod")],
        [],
        TestContext.Current.CancellationToken);
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    await Assert.ThrowsAsync<ImportPersistenceException>(() =>
        fixture.Store.CommitAtomicallyAsync(
            preview,
            confirm: true,
            cancellation.Token));

    Assert.Equal(0L, await fixture.CountRowsAsync("mods"));
    Assert.Equal(0L, await fixture.CountRowsAsync("source_references"));
    Assert.Equal(0L, await fixture.CountRowsAsync("collection_memberships"));
  }

  [Fact]
  public async Task NewImport_CreatesWorkspaceInitialCollectionAndReloadableCatalog()
  {
    using var directory = new TemporaryDirectory();
    var destination = Path.Combine(directory.Path, "new.tww3c");
    var store = CreateDeterministicStore();
    var preview = await store.SavePreviewAsync(
        ImportTargetContext.ForNewWorkspace(
            "New Workspace",
            destination,
            "Imported Collection"),
        [
          ImportCandidate.CreateWithDisplayName(
              "candidate:111",
              "First Mod",
              ImportSourceReference.SteamWorkshop("111"))
        ],
        [],
        TestContext.Current.CancellationToken);

    var outcome = await store.CommitNewWorkspaceAtomicallyAsync(
        preview,
        TestContext.Current.CancellationToken);
    var current = Assert.IsType<ImportTargetContext.CurrentWorkspace>(
        outcome.TargetContext);
    var snapshot = await store.ReadLibrarySnapshotAsync(
        destination,
        TestContext.Current.CancellationToken);

    Assert.True(outcome.Applied);
    Assert.Equal("New Workspace", await ReadWorkspaceNameAsync(destination));
    Assert.Equal("Imported Collection", Assert.Single(snapshot.Collections).DisplayName);
    Assert.Equal("First Mod", Assert.Single(snapshot.Mods).DisplayName);
    Assert.Single(snapshot.Memberships);
    Assert.Equal(current.CollectionId, snapshot.Memberships[0].CollectionId);
  }

  [Fact]
  public async Task NewImport_WhenDestinationExists_LeavesItUntouched()
  {
    using var directory = new TemporaryDirectory();
    var destination = Path.Combine(directory.Path, "existing.tww3c");
    await File.WriteAllTextAsync(destination, "original", TestContext.Current.CancellationToken);
    var store = CreateDeterministicStore();
    var preview = await store.SavePreviewAsync(
        ImportTargetContext.ForNewWorkspace("New Workspace", destination, "Imported Collection"),
        [ImportCandidate.CreateWithDisplayName("candidate:1", "Mod")],
        [],
        TestContext.Current.CancellationToken);

    var exception = await Assert.ThrowsAsync<ImportPersistenceException>(() =>
        store.CommitNewWorkspaceAtomicallyAsync(
            preview,
            TestContext.Current.CancellationToken));

    Assert.Equal("import.destination.exists", exception.Error.Code);
    Assert.False(exception.Error.PersistentChangeCommitted);
    Assert.Equal("original", await File.ReadAllTextAsync(destination, TestContext.Current.CancellationToken));
    Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
  }

  [Fact]
  public async Task NewImport_WhenPreCancelled_CreatesNoDestinationOrTemporaryFile()
  {
    using var directory = new TemporaryDirectory();
    var destination = Path.Combine(directory.Path, "cancelled.tww3c");
    var store = CreateDeterministicStore();
    var preview = await store.SavePreviewAsync(
        ImportTargetContext.ForNewWorkspace("New Workspace", destination, "Imported Collection"),
        [ImportCandidate.CreateWithDisplayName("candidate:1", "Mod")],
        [],
        TestContext.Current.CancellationToken);
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    await Assert.ThrowsAsync<ImportPersistenceException>(() =>
        store.CommitNewWorkspaceAtomicallyAsync(preview, cancellation.Token));

    Assert.False(File.Exists(destination));
    Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
  }

  [Fact]
  public async Task NewImport_WhenFailureAfterFirstCandidate_RemovesTemporaryFileAndLeavesNoDestination()
  {
    using var directory = new TemporaryDirectory();
    var destination = Path.Combine(directory.Path, "failed.tww3c");
    var expectedTemporaryPath =
        $"{destination}.1234567812344abc8def1234567890ab.tmp";
    string? deletedPath = null;
    var store = CreateDeterministicStore(
        deleteOwnedFile: path =>
        {
          deletedPath = path;
          File.Delete(path);
        },
        afterCandidatePersisted: count =>
        {
          if (count == 1)
          {
            throw new InvalidOperationException("Injected failure after first candidate.");
          }
        });
    var preview = await store.SavePreviewAsync(
        ImportTargetContext.ForNewWorkspace("New Workspace", destination, "Imported Collection"),
        [
          ImportCandidate.CreateWithDisplayName("candidate:1", "First Mod"),
          ImportCandidate.CreateWithDisplayName("candidate:2", "Second Mod")
        ],
        [],
        TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<InvalidOperationException>(() =>
        store.CommitNewWorkspaceAtomicallyAsync(
            preview,
            TestContext.Current.CancellationToken));

    Assert.False(File.Exists(destination));
    Assert.False(File.Exists(expectedTemporaryPath));
    Assert.Equal(expectedTemporaryPath, deletedPath);
  }

  [Fact]
  public async Task NewImport_WhenMoveFails_ReturnsTypedUncommittedFailureAndRemovesTemporaryFile()
  {
    using var directory = new TemporaryDirectory();
    var destination = Path.Combine(directory.Path, "move-failed.tww3c");
    var expectedTemporaryPath =
        $"{destination}.1234567812344abc8def1234567890ab.tmp";
    string? deletedPath = null;
    var store = CreateDeterministicStore(
        fileSystem: new MoveFailingAtomicFileSystem(),
        deleteOwnedFile: path =>
        {
          deletedPath = path;
          File.Delete(path);
        });

    var preview = await store.SavePreviewAsync(
        ImportTargetContext.ForNewWorkspace("New Workspace", destination, "Imported Collection"),
        [ImportCandidate.CreateWithDisplayName("candidate:1", "Mod")],
        [],
        TestContext.Current.CancellationToken);

    var exception = await Assert.ThrowsAsync<ImportPersistenceException>(() =>
        store.CommitNewWorkspaceAtomicallyAsync(
            preview,
            TestContext.Current.CancellationToken));

    Assert.False(exception.Error.PersistentChangeCommitted);
    Assert.False(File.Exists(destination));
    Assert.False(File.Exists(expectedTemporaryPath));
    Assert.Equal(expectedTemporaryPath, deletedPath);
  }

  [Fact]
  public async Task NewImport_GeneratesStableCanonicalUuids()
  {
    using var directory = new TemporaryDirectory();
    var destination = Path.Combine(directory.Path, "uuids.tww3c");
    var store = CreateDeterministicStore();
    var preview = await store.SavePreviewAsync(
        ImportTargetContext.ForNewWorkspace("New Workspace", destination, "Imported Collection"),
        [ImportCandidate.CreateWithDisplayName("candidate:1", "Mod")],
        [],
        TestContext.Current.CancellationToken);

    var outcome = await store.CommitNewWorkspaceAtomicallyAsync(
        preview,
        TestContext.Current.CancellationToken);
    var current = Assert.IsType<ImportTargetContext.CurrentWorkspace>(outcome.TargetContext);
    var snapshot = await store.ReadLibrarySnapshotAsync(
        destination,
        TestContext.Current.CancellationToken);

    Assert.Equal("22345678-1234-4abc-8def-1234567890ab", current.WorkspaceId);
    Assert.Equal("32345678-1234-4abc-8def-1234567890ab", current.CollectionId);
    Assert.Equal("42345678-1234-4abc-8def-1234567890ab", Assert.Single(snapshot.Mods).ModId);
    Assert.IsType<ValidationResult<WorkspaceId>.Success>(WorkspaceId.Parse(current.WorkspaceId));
    Assert.IsType<ValidationResult<WorkspaceId>.Success>(WorkspaceId.Parse(current.CollectionId));
    Assert.IsType<ValidationResult<WorkspaceId>.Success>(WorkspaceId.Parse(snapshot.Mods[0].ModId));
  }

  [Fact]
  public async Task NewImport_PlacedDatabasePassesWorkspaceFileValidator()
  {
    using var directory = new TemporaryDirectory();
    var destination = Path.Combine(directory.Path, "validated.tww3c");
    var store = CreateDeterministicStore();
    var preview = await store.SavePreviewAsync(
        ImportTargetContext.ForNewWorkspace("New Workspace", destination, "Imported Collection"),
        [ImportCandidate.CreateWithDisplayName("candidate:1", "Mod")],
        [],
        TestContext.Current.CancellationToken);

    await store.CommitNewWorkspaceAtomicallyAsync(
        preview,
        TestContext.Current.CancellationToken);

    var result = await new WorkspaceFileValidator().OpenAsync(
        destination,
        TestContext.Current.CancellationToken);

    Assert.IsType<OperationResult<Workspace>.Success>(result);
  }

  [Fact]
  public async Task CommitAtomicallyAsync_WithConfirmFalse_DoesNotOpenDatabase()
  {
    await using var fixture = await CatalogWorkspaceFixture.CreateAsync();
    var preview = await fixture.Store.SavePreviewAsync(
        ImportTargetContext.ForCurrentWorkspace(
            fixture.WorkspaceId,
            fixture.WorkspacePath,
            fixture.FirstCollectionId),
        [ImportCandidate.CreateWithDisplayName("candidate:1", "Mod")],
        [],
        TestContext.Current.CancellationToken);

    var outcome = await fixture.Store.CommitAtomicallyAsync(
        preview,
        confirm: false,
        TestContext.Current.CancellationToken);

    Assert.False(outcome.Applied);
    Assert.Equal(0L, await fixture.CountRowsAsync("mods"));
  }

  private static SqliteWorkspaceCatalogStore CreateDeterministicStore(
      IAtomicFileSystem? fileSystem = null,
      Action<string>? deleteOwnedFile = null,
      Action<int>? afterCandidatePersisted = null) =>
      new(
          new SqliteConnectionFactory(),
          new SequenceUuidGenerator(
              "12345678-1234-4abc-8def-1234567890ab",
              "22345678-1234-4abc-8def-1234567890ab",
              "32345678-1234-4abc-8def-1234567890ab",
              "42345678-1234-4abc-8def-1234567890ab"),
          new FixedClock(),
          fileSystem: fileSystem,
          deleteOwnedFile: deleteOwnedFile,
          afterCandidatePersisted: afterCandidatePersisted);

  private static async Task<string> ReadWorkspaceNameAsync(string path)
  {
    await using var connection =
        await new SqliteConnectionFactory().OpenAsync(path, CancellationToken.None);
    await using var command = connection.CreateCommand();
    command.CommandText =
        "SELECT display_name FROM workspace WHERE singleton = 1;";
    return (string)(await command.ExecuteScalarAsync(CancellationToken.None))!;
  }

  private static SqliteWorkspaceCatalogStore CreateStore() =>
      new(
          new SqliteConnectionFactory(),
          new SequenceUuidGenerator("42345678-1234-4abc-8def-1234567890ab"),
          new FixedCatalogClock());

  private sealed class CatalogWorkspaceFixture : IAsyncDisposable
  {
    private readonly TemporaryDirectory directory;

    private CatalogWorkspaceFixture(TemporaryDirectory directory)
    {
      this.directory = directory;
    }

    public required string WorkspacePath { get; init; }
    public required string WorkspaceId { get; init; }
    public required string FirstCollectionId { get; init; }
    public required string SecondCollectionId { get; init; }
    public required SqliteConnectionFactory ConnectionFactory { get; init; }
    public required IUuidGenerator UuidGenerator { get; init; }
    public required IClock Clock { get; init; }
    public required SqliteWorkspaceCatalogStore Store { get; init; }

    public static async Task<CatalogWorkspaceFixture> CreateAsync()
    {
      var directory = new TemporaryDirectory();
      var path = Path.Combine(directory.Path, "catalog.tww3c");
      const string workspaceId = "12345678-1234-4abc-8def-1234567890ab";
      const string firstCollectionId = "22345678-1234-4abc-8def-1234567890ab";
      const string secondCollectionId = "32345678-1234-4abc-8def-1234567890ab";
      var workspace = CreateWorkspace(workspaceId, "Catalog Workspace");
      var connectionFactory = new SqliteConnectionFactory();
      Assert.IsType<OperationResult<Workspace>.Success>(
          await new SqliteWorkspaceStore(connectionFactory).CreateAsync(
              path,
              workspace,
              CancellationToken.None));
      await InsertCollectionAsync(connectionFactory, path, firstCollectionId, "First Collection");
      await InsertCollectionAsync(connectionFactory, path, secondCollectionId, "Second Collection");

      var uuidGenerator = new SequenceUuidGenerator(
          "42345678-1234-4abc-8def-1234567890ab",
          "52345678-1234-4abc-8def-1234567890ab",
          "62345678-1234-4abc-8def-1234567890ab");
      var clock = new FixedCatalogClock();
      var store = new SqliteWorkspaceCatalogStore(connectionFactory, uuidGenerator, clock);

      return new CatalogWorkspaceFixture(directory)
      {
        WorkspacePath = path,
        WorkspaceId = workspaceId,
        FirstCollectionId = firstCollectionId,
        SecondCollectionId = secondCollectionId,
        ConnectionFactory = connectionFactory,
        UuidGenerator = uuidGenerator,
        Clock = clock,
        Store = store
      };
    }

    public async Task<int[]> ReadMembershipPositionsAsync(string collectionId)
    {
      await using var connection = await ConnectionFactory.OpenAsync(WorkspacePath, CancellationToken.None);
      await using var command = connection.CreateCommand();
      command.CommandText = """
          SELECT position
          FROM collection_memberships
          WHERE collection_id = $collectionId
          ORDER BY position;
          """;
      command.Parameters.AddWithValue("$collectionId", collectionId);
      await using var reader = await command.ExecuteReaderAsync(CancellationToken.None);
      var positions = new List<int>();
      while (await reader.ReadAsync(CancellationToken.None))
      {
        positions.Add(reader.GetInt32(0));
      }

      return positions.ToArray();
    }

    public Task<long> CountRowsAsync(string tableName) =>
        ScalarAsync(WorkspacePath, $"SELECT COUNT(*) FROM {tableName};");

    public async Task InsertModAsync(string modId, string displayName)
    {
      await using var connection = await ConnectionFactory.OpenAsync(WorkspacePath, CancellationToken.None);
      await using var command = connection.CreateCommand();
      command.CommandText = """
          INSERT INTO mods (id, display_name)
          VALUES ($id, $displayName);
          """;
      command.Parameters.AddWithValue("$id", modId);
      command.Parameters.AddWithValue("$displayName", displayName);
      await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    public async Task InsertModWithSourceReferenceAsync(
        string modId,
        string displayName,
        ImportSourceReference sourceReference)
    {
      await InsertModAsync(modId, displayName);
      await using var connection = await ConnectionFactory.OpenAsync(WorkspacePath, CancellationToken.None);
      await using var command = connection.CreateCommand();
      command.CommandText = """
          INSERT INTO source_references (source_type, external_id, mod_id)
          VALUES ($sourceType, $externalId, $modId);
          """;
      command.Parameters.AddWithValue("$sourceType", "steam-workshop");
      command.Parameters.AddWithValue("$externalId", sourceReference.ExternalId);
      command.Parameters.AddWithValue("$modId", modId);
      await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    public async Task InsertModWithMembershipAsync(
        string modId,
        string displayName,
        string collectionId,
        int position)
    {
      await InsertModAsync(modId, displayName);
      await using var connection = await ConnectionFactory.OpenAsync(WorkspacePath, CancellationToken.None);
      await using var command = connection.CreateCommand();
      command.CommandText = """
          INSERT INTO collection_memberships (collection_id, mod_id, position)
          VALUES ($collectionId, $modId, $position);
          """;
      command.Parameters.AddWithValue("$collectionId", collectionId);
      command.Parameters.AddWithValue("$modId", modId);
      command.Parameters.AddWithValue("$position", position);
      await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    public ValueTask DisposeAsync()
    {
      directory.Dispose();
      return ValueTask.CompletedTask;
    }

    private static async Task InsertCollectionAsync(
        SqliteConnectionFactory connectionFactory,
        string path,
        string collectionId,
        string displayName)
    {
      await using var connection = await connectionFactory.OpenAsync(path, CancellationToken.None);
      await using var command = connection.CreateCommand();
      command.CommandText = """
          INSERT INTO collections (id, display_name)
          VALUES ($id, $displayName);
          """;
      command.Parameters.AddWithValue("$id", collectionId);
      command.Parameters.AddWithValue("$displayName", displayName);
      await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    private static Workspace CreateWorkspace(string workspaceId, string name)
    {
      var id = Assert.IsType<ValidationResult<Domain.Workspaces.WorkspaceId>.Success>(
          Domain.Workspaces.WorkspaceId.Parse(workspaceId)).Value;
      var workspaceName = Assert.IsType<ValidationResult<WorkspaceName>.Success>(WorkspaceName.Create(name)).Value;
      return Assert.IsType<ValidationResult<Workspace>.Success>(Workspace.Create(
          id,
          workspaceName,
          new DateTimeOffset(2026, 7, 18, 1, 2, 3, TimeSpan.Zero),
          new DateTimeOffset(2026, 7, 18, 4, 5, 6, TimeSpan.Zero))).Value;
    }
  }

  private sealed class SequenceUuidGenerator(params string[] values) : IUuidGenerator
  {
    private int index;

    public string NewUuid() =>
        index >= values.Length
            ? throw new InvalidOperationException("No more deterministic UUID values are available.")
            : values[index++];
  }

  private sealed class FixedCatalogClock : IClock
  {
    public DateTimeOffset UtcNow => new(2026, 7, 18, 1, 2, 3, 456, TimeSpan.Zero);
  }

  private sealed class FixedClock : IClock
  {
    public DateTimeOffset UtcNow => new(2026, 7, 24, 0, 0, 0, TimeSpan.Zero);
  }

  private sealed class MoveFailingAtomicFileSystem : IAtomicFileSystem
  {
    public Stream CreateWriteProbe(string directory) => Stream.Null;

    public void MoveWithoutOverwrite(string source, string destination) =>
        throw new IOException("move failed");

    public Task WriteAllTextAtomicallyAsync(string path, string content, CancellationToken token) =>
        throw new NotSupportedException();
  }

  private static async Task<long> ScalarAsync(string path, string sql)
  {
    await using var connection = await new SqliteConnectionFactory().OpenAsync(path, CancellationToken.None);
    await using var command = connection.CreateCommand();
    command.CommandText = sql;
    return (long)(await command.ExecuteScalarAsync(CancellationToken.None))!;
  }
}
