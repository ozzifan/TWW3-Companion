# Local Workspace Catalog Persistence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Persist imported Mods, Collections, canonical Source References, and Collection Memberships in schema-v2 Workspace databases so the library overlay returns the same data immediately after import and after reopening the application.

**Architecture:** Extend the Application import contracts with explicit Collection destinations and structured source identity, then add a normalized schema-v2 migration. One Infrastructure SQLite catalog adapter implements both the import-write port and a narrow catalog-read port; `ApplicationComposition` supplies that adapter to `ImportEngine` and `WorkspaceLibraryQuery`. Confirmed imports are additive and atomic, while preview creation remains in memory.

**Tech Stack:** C# / .NET 10, Avalonia 12.1.0, Microsoft.Data.Sqlite.Core 10.0.10, SQLitePCLRaw.bundle_winsqlite3 2.1.11, xUnit 3.2.2, existing Application / Infrastructure / Desktop projects.

## Global Constraints

- Every implementation task must enter through AI Dev Orchestrator and use the rigid `IMP` implementation role followed by the `REV` review role defined in `AGENTS.md`.
- The Product Owner's approval of this implementation plan is the only approval that may not be delegated.
- Start the managed checkout from the exact approved plan commit on current `origin/main`.
- Use `C:\Users\steve\.dotnet\dotnet.exe` for restore, format, build, and test commands.
- Keep SQL, `Microsoft.Data.Sqlite`, filesystem mutation, and Serilog out of Domain and Application.
- Use Microsoft logging abstractions above Infrastructure; do not log imported descriptions, names, notes, or other user-authored content.
- Schema v2 is forward-only. Opening a valid schema-v1 Workspace for editing must create a SQLite-safe pre-migration backup and migrate transactionally.
- Every confirmed import targets exactly one Collection.
- Mods are shared inside one Workspace; this slice does not create a machine-global or cross-Workspace catalog.
- Membership position records source-order documentation only. It must not enforce or alter game load order.
- Imports remain additive-only. Re-import never deletes or reorders an existing Membership.
- Preview construction must not create, open, or mutate a Workspace database.
- New-Workspace import uses an application-owned temporary file beside the destination and a final non-overwriting move on the same filesystem.
- Infrastructure failure tests use constructor-injected seams. Do not add an executable test hook or gate ordinary unit tests on `TWW3_COMPANION_TEST_MODE`.
- Do not add import forms, preview screens, Collection-management UI, JSON export/restore, notes, tags, categories, relationships, compatibility observations, replace behavior, or synchronization.
- Keep `.superpowers/sdd/` reports and `.orchestrator-work-packet.json` out of implementation commits.

---

### Task 1: Make import identity and Collection destinations explicit

**Files:**
- Create: `src/Tww3Companion.Application/Importing/ImportSourceReference.cs`
- Create: `src/Tww3Companion.Application/Importing/ImportValidationIssue.cs`
- Create: `src/Tww3Companion.Application/Importing/ImportPersistenceException.cs`
- Create: `src/Tww3Companion.Application/Workspaces/IWorkspaceCatalogReader.cs`
- Modify: `src/Tww3Companion.Application/Importing/ImportCandidate.cs`
- Modify: `src/Tww3Companion.Application/Importing/ImportPreview.cs`
- Modify: `src/Tww3Companion.Application/Importing/ImportTargetContext.cs`
- Modify: `src/Tww3Companion.Application/Importing/ImportEngine.cs`
- Modify: `src/Tww3Companion.Application/Importing/SteamImportAdapter.cs`
- Modify: `src/Tww3Companion.Application/Importing/CurrentWorkspaceImportSession.cs`
- Modify: `src/Tww3Companion.Application/Workspaces/WorkspaceLibraryQuery.cs`
- Modify: `src/Tww3Companion.Desktop/ViewModels/ShellViewModel.cs`
- Modify: `tests/Tww3Companion.Application.Tests/Importing/ImportEngineTests.cs`
- Modify: `tests/Tww3Companion.Application.Tests/Workspaces/WorkspaceQueryTests.cs`
- Modify: `tests/Tww3Companion.Desktop.Tests/ViewModels/ShellViewModelTests.cs`

**Interfaces:**
- Consumes: existing Markdown and Steam candidate adapters, `IWorkspaceImportStore`, `WorkspaceLibrarySnapshot`
- Produces:

```csharp
public enum ImportSourceType
{
  SteamWorkshop
}

public sealed record ImportSourceReference(
    ImportSourceType SourceType,
    string ExternalId);

public sealed record ImportCandidate(
    string CandidateId,
    ImportSourceReference? SourceReference,
    string? LinkedModId,
    string? DisplayName,
    bool IsSkipped,
    string? SuggestedModId = null);

public sealed record ImportValidationIssue(
    string CandidateId,
    string Code,
    string Message);

public sealed record ImportPreview(
    ImportTargetContext TargetContext,
    IReadOnlyList<object> Candidates,
    bool Applied,
    IReadOnlyList<ImportResolution>? Resolutions = null,
    IReadOnlyList<ImportValidationIssue>? ValidationIssues = null);

public interface IWorkspaceCatalogReader
{
  Task<WorkspaceLibrarySnapshot> ReadLibrarySnapshotAsync(
      string workspacePath,
      CancellationToken cancellationToken = default);
}
```

- Extends the sealed target hierarchy to:

```csharp
public sealed record NewWorkspace(
    string DisplayName,
    string DestinationPath,
    string CollectionDisplayName) : ImportTargetContext;

public sealed record CurrentWorkspace(
    string WorkspaceId,
    string WorkspacePath,
    string CollectionId) : ImportTargetContext;
```

- The old two-argument `ForNewWorkspace` and one-argument `ForCurrentWorkspace` factories are removed. Every compile failure must be updated to the new Collection-specific contract.

- [ ] **Step 1: Add failing tests for structured identity and target requirements**

Add focused tests that prove:

```csharp
[Fact]
public async Task Steam_url_normalizes_to_canonical_workshop_identity()
{
  var preview = await new ImportEngine(new FakeWorkspaceImportStore())
      .BuildPreviewAsync(
          ImportTargetContext.ForNewWorkspace(
              "Workspace",
              "C:\\Workspaces\\workspace.tww3c",
              "Imported Collection"),
          [new SteamImportCandidate(
              "https://steamcommunity.com/sharedfiles/filedetails/?id=123456789",
              "Example Mod")],
          TestContext.Current.CancellationToken);

  var candidate = Assert.IsType<ImportCandidate>(Assert.Single(preview.Candidates));
  Assert.Equal("123456789", candidate.SourceReference!.ExternalId);
  Assert.Equal(ImportSourceType.SteamWorkshop, candidate.SourceReference.SourceType);
}

[Fact]
public async Task Source_neutral_markdown_has_preview_identity_but_no_source_reference()
{
  var preview = await new ImportEngine(new FakeWorkspaceImportStore())
      .BuildPreviewAsync(
          ImportTargetContext.ForNewWorkspace(
              "Workspace",
              "C:\\Workspaces\\workspace.tww3c",
              "Imported Collection"),
          [new MarkdownImportCandidate(
              ImportCandidateKind.Candidate,
              "Local Mod",
              "Local Mod",
              SourceLine: 7)],
          TestContext.Current.CancellationToken);

  var candidate = Assert.IsType<ImportCandidate>(Assert.Single(preview.Candidates));
  Assert.Equal("markdown:7", candidate.CandidateId);
  Assert.Null(candidate.SourceReference);
}

[Fact]
public async Task Conflicting_source_owner_blocks_apply_in_preview()
{
  var store = new FakeWorkspaceImportStore
  {
    ExistingCandidates =
    [
      ImportCandidate.Linked(
          "existing:123",
          "existing-mod",
          ImportSourceReference.SteamWorkshop("123"))
    ]
  };
  var engine = new ImportEngine(store);

  var preview = await engine.BuildPreviewAsync(
      ImportTargetContext.ForCurrentWorkspace(
          "12345678-1234-4abc-8def-1234567890ab",
          "C:\\Workspaces\\workspace.tww3c",
          "22345678-1234-4abc-8def-1234567890ab"),
      [ImportCandidate.Linked(
          "incoming:123",
          "different-mod",
          ImportSourceReference.SteamWorkshop("123"))],
      TestContext.Current.CancellationToken);

  Assert.Contains(
      preview.ValidationIssues!,
      issue => issue.Code == "import.source.owner.conflict");
  await Assert.ThrowsAsync<InvalidOperationException>(
      () => engine.ApplyAsync(
          preview,
          confirm: true,
          TestContext.Current.CancellationToken));
}
```

Update every existing import-engine and shell test to supply:

```csharp
ImportTargetContext.ForNewWorkspace(
    "My New Workspace",
    "C:\\Workspaces\\my-new.tww3c",
    "Imported Collection")

ImportTargetContext.ForCurrentWorkspace(
    "workspace-id-123",
    "C:\\Workspaces\\current.tww3c",
    "collection-id-123")
```

- [ ] **Step 2: Run the focused tests and record the red state**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj --filter "ImportEngineTests|WorkspaceQueryTests" -v minimal
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj --filter ShellViewModelTests -v minimal
```

Expected: compilation fails because the structured source types, target fields, catalog-read port, and updated factories do not exist.

- [ ] **Step 3: Add the structured import contracts**

Implement these factories without additional source kinds:

```csharp
public sealed record ImportSourceReference(
    ImportSourceType SourceType,
    string ExternalId)
{
  public static ImportSourceReference SteamWorkshop(string workshopItemId)
  {
    if (string.IsNullOrWhiteSpace(workshopItemId) ||
        !workshopItemId.All(char.IsDigit))
    {
      throw new ArgumentException(
          "A Steam Workshop source reference requires a numeric item ID.",
          nameof(workshopItemId));
    }

    return new(
        ImportSourceType.SteamWorkshop,
        workshopItemId.Trim());
  }
}
```

Retain the convenient candidate factories with explicit preview and source identity:

```csharp
public static ImportCandidate Linked(
    string candidateId,
    string linkedModId,
    ImportSourceReference? sourceReference = null) =>
    new(
        candidateId,
        sourceReference,
        linkedModId,
        DisplayName: null,
        IsSkipped: false);

public static ImportCandidate CreateWithDisplayName(
    string candidateId,
    string displayName,
    ImportSourceReference? sourceReference = null) =>
    new(
        candidateId,
        sourceReference,
        LinkedModId: null,
        displayName,
        IsSkipped: false);

public static ImportCandidate Skipped(string candidateId) =>
    new(
        candidateId,
        SourceReference: null,
        LinkedModId: null,
        DisplayName: null,
        IsSkipped: true);
```

Add an `ImportPersistenceException` that preserves the repository error model:

```csharp
public sealed class ImportPersistenceException(OperationError error)
    : Exception(error.Message)
{
  public OperationError Error { get; } = error;
}
```

- [ ] **Step 4: Normalize Workshop identity and preview conflicts**

Change `SteamImportAdapter.TryGetWorkshopItemId` from `private` to `internal` so normalization uses the already-tested parser rather than copying URL logic.

In `ImportEngine.NormalizeCandidates`, use the candidate index to keep preview-row identity distinct from persistent source identity:

```csharp
private static IReadOnlyList<ImportCandidate> NormalizeCandidates(
    IReadOnlyList<object> candidates) =>
    candidates.Select((candidate, index) => candidate switch
    {
SteamImportCandidate steamCandidate =>
    CreateSteamCandidate(steamCandidate, index),

MarkdownImportCandidate
{
  Kind: ImportCandidateKind.Candidate,
  SourceReference: { } source
} markdownCandidate =>
    ImportCandidate.CreateWithDisplayName(
        $"markdown:{markdownCandidate.SourceLine}",
        markdownCandidate.Value,
        ImportSourceReference.SteamWorkshop(source.WorkshopId)),

MarkdownImportCandidate
{
  Kind: ImportCandidateKind.Candidate
} markdownCandidate =>
    ImportCandidate.CreateWithDisplayName(
        $"markdown:{markdownCandidate.SourceLine}",
        markdownCandidate.Value),

ImportCandidate importCandidate => importCandidate,

MarkdownImportCandidate =>
    throw new ArgumentException(
        "Only Markdown candidate entries can enter the import pipeline.",
        nameof(candidates)),

_ => throw new ArgumentException(
    $"Unsupported import candidate type: {candidate?.GetType().FullName ?? "<null>"}.",
    nameof(candidates))
    }).ToArray();
```

Implement:

```csharp
private static ImportCandidate CreateSteamCandidate(
    SteamImportCandidate candidate,
    int index)
{
  if (!SteamImportAdapter.TryGetWorkshopItemId(
          candidate.SourceReference,
          out var workshopItemId))
  {
    throw new ArgumentException(
        "Steam candidates require a numeric Workshop ID or supported Workshop URL.",
        nameof(candidate));
  }

  return ImportCandidate.CreateWithDisplayName(
      $"steam:{workshopItemId}:{index}",
      candidate.DisplayName ?? candidate.SourceReference,
      ImportSourceReference.SteamWorkshop(workshopItemId));
}
```

Do not add new Steam URL forms in this slice.

Match exact identities by `SourceType` and `ExternalId`, not `CandidateId`. If an incoming candidate already links a different Mod than the exact source owner, add:

```csharp
new ImportValidationIssue(
    candidate.CandidateId,
    "import.source.owner.conflict",
    $"The source identity is already owned by Mod {existing.LinkedModId}.")
```

`ImportPreviewValidation.Validate` must reject a preview containing any validation issue before calling a commit method.

- [ ] **Step 5: Add the Collection-specific target factories**

Implement validation in the factories:

```csharp
public static ImportTargetContext ForNewWorkspace(
    string displayName,
    string destinationPath,
    string collectionDisplayName) =>
    new NewWorkspace(displayName, destinationPath, collectionDisplayName);

public static ImportTargetContext ForCurrentWorkspace(
    string workspaceId,
    string workspacePath,
    string collectionId) =>
    new CurrentWorkspace(workspaceId, workspacePath, collectionId);
```

Extend `NewWorkspaceImportSession.ValidateDestination` to reject a blank Collection display name. Current-Workspace persistence validation of UUIDs and the target Collection remains the Infrastructure adapter's responsibility.

Update the existing shell test helpers with deterministic values only. Do not present these calls as completed import UI:

```csharp
ImportTargetContext.ForNewWorkspace(
    "My New Workspace",
    "C:\\Workspaces\\my-new.tww3c",
    "Imported Collection")
```

The current-Workspace hook receives path and Collection ID as test parameters. Do not invent a production default Collection.

- [ ] **Step 6: Introduce the catalog-read port**

Create `IWorkspaceCatalogReader` with the signature in this task's Interfaces block. Change `WorkspaceLibraryQuery` to depend on that port:

```csharp
public sealed class WorkspaceLibraryQuery(
    IWorkspaceCatalogReader catalogReader) : IWorkspaceQuery
{
  private static readonly WorkspaceLibrarySnapshot EmptySnapshot =
      new([], [], []);

  private string? activeWorkspacePath;

  public void SetActiveWorkspacePath(string? path) =>
      activeWorkspacePath =
          string.IsNullOrWhiteSpace(path) ? null : path;

  public Task<WorkspaceLibrarySnapshot> GetLibrarySnapshotAsync(
      CancellationToken cancellationToken) =>
      activeWorkspacePath is null
          ? Task.FromResult(EmptySnapshot)
          : catalogReader.ReadLibrarySnapshotAsync(
              activeWorkspacePath,
              cancellationToken);
}
```

Update `WorkspaceQueryTests` to use a recording catalog reader and assert the active path is passed exactly.

- [ ] **Step 7: Run focused and layer-boundary tests**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj --filter "ImportEngineTests|WorkspaceQueryTests|DependencyRulesTests" -v minimal
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj --filter ShellViewModelTests -v minimal
git diff --check
```

Expected: all selected tests pass and the dependency tests confirm no SQLite or Serilog reference entered Application.

- [ ] **Step 8: Inspect scope and commit**

Run:

```powershell
git status --short
git diff --stat
git diff --name-only
```

Expected: only Task 1 Application contracts, the narrow shell test hook, and their tests changed.

Commit:

```powershell
git add src/Tww3Companion.Application src/Tww3Companion.Desktop/ViewModels/ShellViewModel.cs tests/Tww3Companion.Application.Tests tests/Tww3Companion.Desktop.Tests/ViewModels/ShellViewModelTests.cs
git commit -m "feat: make import collection targets explicit"
```

---

### Task 2: Add schema v2 and safe v1 migration

**Files:**
- Create: `src/Tww3Companion.Infrastructure/Storage/Schema/SchemaV2.cs`
- Create: `src/Tww3Companion.Infrastructure/Storage/Schema/WorkspaceSchemaInspector.cs`
- Create: `src/Tww3Companion.Infrastructure/Storage/Migrations/MigrateV1ToV2.cs`
- Modify: `src/Tww3Companion.Infrastructure/Storage/Schema/SchemaVersion.cs`
- Modify: `src/Tww3Companion.Infrastructure/Storage/Migrations/MigrationRunner.cs`
- Modify: `src/Tww3Companion.Infrastructure/Storage/WorkspaceFileValidator.cs`
- Modify: `src/Tww3Companion.Infrastructure/Storage/SqliteWorkspaceStore.cs`
- Modify: `tests/Tww3Companion.Infrastructure.Tests/Storage/MigrationRunnerTests.cs`
- Modify: `tests/Tww3Companion.Infrastructure.Tests/Storage/SqliteWorkspaceStoreTests.cs`
- Modify: `tests/Tww3Companion.Infrastructure.Tests/Storage/WorkspaceFileValidatorTests.cs`
- Create: `tests/Tww3Companion.Infrastructure.Tests/Storage/Fixtures/SchemaVersionOneFixture.cs`

**Interfaces:**
- Consumes: `IMigration`, `MigrationRunner`, `WorkspaceBackupService`, `IAtomicFileSystem`, `SqliteConnectionFactory`
- Produces:

```csharp
public static class SchemaVersion
{
  public const int Current = 2;
}

public sealed class MigrateV1ToV2 : IMigration
{
  public int FromVersion => 1;
  public int ToVersion => 2;

  public Task ApplyAsync(
      SqliteConnection connection,
      SqliteTransaction transaction,
      CancellationToken cancellationToken);
}
```

- Produces these exact schema-v2 tables in addition to the three v1 tables:

```sql
CREATE TABLE mods (
    id TEXT PRIMARY KEY,
    display_name TEXT NOT NULL CHECK (length(trim(display_name)) > 0)
);

CREATE TABLE collections (
    id TEXT PRIMARY KEY,
    display_name TEXT NOT NULL CHECK (length(trim(display_name)) > 0)
);

CREATE TABLE source_references (
    source_type TEXT NOT NULL,
    external_id TEXT NOT NULL CHECK (length(trim(external_id)) > 0),
    mod_id TEXT NOT NULL,
    PRIMARY KEY (source_type, external_id),
    FOREIGN KEY (mod_id) REFERENCES mods(id) ON DELETE RESTRICT
);

CREATE TABLE collection_memberships (
    collection_id TEXT NOT NULL,
    mod_id TEXT NOT NULL,
    position INTEGER NOT NULL CHECK (position >= 0),
    PRIMARY KEY (collection_id, mod_id),
    UNIQUE (collection_id, position),
    FOREIGN KEY (collection_id) REFERENCES collections(id) ON DELETE CASCADE,
    FOREIGN KEY (mod_id) REFERENCES mods(id) ON DELETE RESTRICT
);
```

- [ ] **Step 1: Create a real schema-v1 fixture and failing migration tests**

`SchemaVersionOneFixture.CreateAsync` must create the exact current schema-v1 structure and one valid Workspace identity. Add tests:

```csharp
[Fact]
public async Task MigrateV1ToV2_AddsNormalizedCatalogTablesAndRetainsWorkspace()
{
  using var directory = new TemporaryDirectory();
  var path = Path.Combine(directory.Path, "v1.tww3c");
  await SchemaVersionOneFixture.CreateAsync(path);

  var result = Assert.IsType<OperationResult<int>.Success>(
      await CreateRunner(directory.Path, new MigrateV1ToV2())
          .MigrateAsync(
              path,
              2,
              TestContext.Current.CancellationToken));

  Assert.Equal(2, result.Value);
  Assert.Equal(
      ["application_metadata", "collection_memberships", "collections",
       "mods", "schema_migrations", "source_references", "workspace"],
      await ReadTablesAsync(path));
  Assert.Equal(
      SchemaVersionOneFixture.WorkspaceId,
      await ScalarTextAsync(path, "SELECT id FROM workspace"));
}

[Fact]
public async Task FailedV1ToV2Migration_RollsBackAndRetainsV1Backup()
{
  using var directory = new TemporaryDirectory();
  var path = Path.Combine(directory.Path, "v1.tww3c");
  await SchemaVersionOneFixture.CreateAsync(path);

  var failure = Assert.IsType<OperationResult<int>.Failure>(
      await CreateRunner(
              directory.Path,
              new FailingV1ToV2Migration())
          .MigrateAsync(path, 2, CancellationToken.None));

  Assert.Equal("workspace.migration.failed", failure.Error.Code);
  Assert.Equal(1L, await ScalarAsync(
      path,
      "SELECT schema_version FROM application_metadata"));
  Assert.DoesNotContain("mods", await ReadTablesAsync(path));

  var backup = Assert.Single(Directory.GetFiles(
      Path.Combine(
          directory.Path,
          "Backups",
          SchemaVersionOneFixture.WorkspaceId)));
  Assert.Equal(1L, await ScalarAsync(
      backup,
      "SELECT schema_version FROM application_metadata"));
}
```

Add these exact helpers to `MigrationRunnerTests`:

```csharp
private static async Task<string[]> ReadTablesAsync(string path)
{
  await using var connection =
      await new SqliteConnectionFactory().OpenAsync(
          path,
          CancellationToken.None);
  await using var command = connection.CreateCommand();
  command.CommandText =
      "SELECT name FROM sqlite_schema " +
      "WHERE type='table' AND name NOT LIKE 'sqlite_%' " +
      "ORDER BY name;";
  await using var reader =
      await command.ExecuteReaderAsync(CancellationToken.None);
  var tables = new List<string>();
  while (await reader.ReadAsync(CancellationToken.None))
  {
    tables.Add(reader.GetString(0));
  }

  return tables.ToArray();
}

private static async Task<string> ScalarTextAsync(
    string path,
    string sql)
{
  await using var connection =
      await new SqliteConnectionFactory().OpenAsync(
          path,
          CancellationToken.None);
  await using var command = connection.CreateCommand();
  command.CommandText = sql;
  return (string)(await command.ExecuteScalarAsync(
      CancellationToken.None))!;
}
```

- [ ] **Step 2: Run migration tests and record the red state**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Infrastructure.Tests/Tww3Companion.Infrastructure.Tests.csproj --filter "MigrationRunnerTests|SqliteWorkspaceStoreTests|WorkspaceFileValidatorTests" -v minimal
```

Expected: compilation fails because schema v2, its migration, and version-aware structure validation do not exist.

- [ ] **Step 3: Implement schema-v2 initialization and migration**

`SchemaV2.InitializeAsync` owns no transaction. Its caller supplies the transaction so Workspace creation and initial catalog insertion can share one atomic boundary:

```csharp
internal static Task InitializeAsync(
    SqliteConnection connection,
    SqliteTransaction transaction,
    Workspace workspace,
    CancellationToken cancellationToken);
```

It creates the seven required tables, inserts:

```sql
INSERT INTO application_metadata VALUES (1, $applicationId, 2);
INSERT INTO schema_migrations VALUES (1, $appliedUtc);
INSERT INTO schema_migrations VALUES (2, $appliedUtc);
INSERT INTO workspace VALUES (
    1, $id, $name, $createdUtc, $modifiedUtc);
```

and leaves commit or rollback to the caller.

`MigrateV1ToV2.ApplyAsync` executes only the four catalog `CREATE TABLE` statements from the Interfaces block. `MigrationRunner` remains responsible for recording version 2 and validating before commit.

- [ ] **Step 4: Make schema inspection version-aware**

Move duplicated table/column/constraint checks from `MigrationRunner` and `WorkspaceFileValidator` into `WorkspaceSchemaInspector`:

```csharp
internal static class WorkspaceSchemaInspector
{
  public static Task ValidateAsync(
      SqliteConnection connection,
      SqliteTransaction? transaction,
      int schemaVersion,
      CancellationToken cancellationToken);
}
```

For version 1 it requires only:

```text
application_metadata
schema_migrations
workspace
```

For version 2 it requires all seven tables and verifies:

- exact required columns;
- both Membership uniqueness constraints;
- Source Reference primary-key uniqueness;
- every foreign key;
- `PRAGMA integrity_check` returns only `ok`;
- `PRAGMA foreign_key_check` returns no rows.

Unknown versions throw `InvalidOperationException`; public callers translate that into their existing typed failure.

- [ ] **Step 5: Create schema v2 for new empty Workspaces**

Change `SqliteWorkspaceStore.CreateAsync` to:

```csharp
await using var connection =
    await connectionFactory.OpenAsync(temporaryPath, token);
await using var transaction =
    (SqliteTransaction)await connection.BeginTransactionAsync(token);
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
```

Preserve sibling temporary-file cleanup and `MoveWithoutOverwrite`.

Change the creation test name and assertions to require the seven schema-v2 tables and version 2.

- [ ] **Step 6: Migrate schema v1 before opening for editing**

Give `SqliteWorkspaceStore` an injected `MigrationRunner`. `OpenAsync` must:

1. validate file identity and read its schema version;
2. reject a newer version without mutation;
3. run the configured v1-to-v2 migration when version is 1;
4. revalidate after migration;
5. return the Workspace only after schema-v2 validation succeeds.

Do not silently instantiate a migration runner with a guessed backup location. Production composition supplies the runner with the mode-specific `ManagedPaths`; tests supply one rooted in their `TemporaryDirectory`.

Add tests proving:

```csharp
Assert.Equal(
    2L,
    await ScalarAsync(
        path,
        "SELECT schema_version FROM application_metadata"));
Assert.Single(Directory.GetFiles(
    Path.Combine(
        managedRoot,
        "Backups",
        SchemaVersionOneFixture.WorkspaceId)));
```

Also retain tests that a newer schema, invalid structure, corruption, and lock failures do not mutate the file.

- [ ] **Step 7: Run the complete Infrastructure storage tests**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Infrastructure.Tests/Tww3Companion.Infrastructure.Tests.csproj --filter "FullyQualifiedName~Storage" -v minimal
git diff --check
```

Expected: all storage tests pass, including existing backup, cancellation, corruption, and atomic-placement tests.

- [ ] **Step 8: Inspect scope and commit**

Run:

```powershell
git status --short
git diff --stat
git diff --name-only
```

Expected: only Task 2 schema, migration, validation, Workspace-store, fixture, and test files changed.

Commit:

```powershell
git add src/Tww3Companion.Infrastructure/Storage tests/Tww3Companion.Infrastructure.Tests/Storage
git commit -m "feat: add workspace catalog schema v2"
```

---

### Task 3: Persist and query current-Workspace catalog imports

**Files:**
- Create: `src/Tww3Companion.Infrastructure/Storage/SqliteWorkspaceCatalogStore.cs`
- Create: `tests/Tww3Companion.Infrastructure.Tests/Storage/SqliteWorkspaceCatalogStoreTests.cs`
- Modify: `src/Tww3Companion.Application/Importing/IWorkspaceImportStore.cs`
- Modify: `tests/Tww3Companion.Application.Tests/Importing/ImportEngineTests.cs`

**Interfaces:**
- Consumes: Task 1's structured candidates and catalog-read port; Task 2's schema v2 and validator
- Produces:

```csharp
public sealed class SqliteWorkspaceCatalogStore :
    IWorkspaceImportStore,
    IWorkspaceCatalogReader
{
  public SqliteWorkspaceCatalogStore(
      SqliteConnectionFactory connectionFactory,
      IUuidGenerator uuidGenerator,
      IClock clock,
      WorkspaceFileValidator? validator = null,
      IAtomicFileSystem? fileSystem = null,
      Action<string>? deleteOwnedFile = null,
      Action<int>? afterCandidatePersisted = null);

  public Task<IReadOnlyList<ImportCandidate>> ReadCandidatesAsync(
      ImportTargetContext targetContext,
      CancellationToken cancellationToken = default);

  public Task<bool> ModExistsAsync(
      ImportTargetContext.CurrentWorkspace targetContext,
      string modId,
      CancellationToken cancellationToken = default);

  public Task<ImportPreview> SavePreviewAsync(
      ImportTargetContext targetContext,
      IReadOnlyList<ImportCandidate> candidates,
      IReadOnlyList<ImportResolution> resolutions,
      CancellationToken cancellationToken = default);

  public Task<ImportOutcome> CommitAtomicallyAsync(
      ImportPreview preview,
      bool confirm,
      CancellationToken cancellationToken = default);

  public Task<WorkspaceLibrarySnapshot> ReadLibrarySnapshotAsync(
      string workspacePath,
      CancellationToken cancellationToken = default);
}
```

- `CommitNewWorkspaceAtomicallyAsync` is implemented in Task 4.

- [ ] **Step 1: Add failing integration tests for current-Workspace persistence**

Create a schema-v2 Workspace with two Collections and test:

```csharp
[Fact]
public async Task CurrentImport_PersistsAndReloadsCatalog()
{
  var fixture = await CatalogWorkspaceFixture.CreateAsync();
  var store = fixture.Store;
  var target = ImportTargetContext.ForCurrentWorkspace(
      fixture.WorkspaceId,
      fixture.Path,
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
          fixture.Path,
          TestContext.Current.CancellationToken);

  Assert.True(outcome.Applied);
  Assert.Equal(2, reopened.Mods.Count);
  Assert.Equal(
      [0, 1],
      await fixture.ReadMembershipPositionsAsync(
          fixture.FirstCollectionId));
}
```

The test file owns a focused fixture with these members:

```csharp
private sealed class CatalogWorkspaceFixture : IAsyncDisposable
{
  public required string Path { get; init; }
  public required string WorkspaceId { get; init; }
  public required string FirstCollectionId { get; init; }
  public required string SecondCollectionId { get; init; }
  public required SqliteConnectionFactory ConnectionFactory { get; init; }
  public required IUuidGenerator UuidGenerator { get; init; }
  public required IClock Clock { get; init; }
  public required SqliteWorkspaceCatalogStore Store { get; init; }

  public static Task<CatalogWorkspaceFixture> CreateAsync();

  public Task<int[]> ReadMembershipPositionsAsync(
      string collectionId);

  public ValueTask DisposeAsync();
}
```

`CreateAsync` uses `SchemaV2.InitializeAsync` and parameterized inserts to create exactly one valid Workspace and two empty Collections in a `TemporaryDirectory`. It exposes deterministic UUID and clock fakes declared in the same test file and deletes only its own temporary directory during `DisposeAsync`.

Add separate tests for:

- wrong Workspace UUID;
- missing Collection UUID;
- exact source identity reusing the same Mod in the second Collection;
- re-import into the same Collection retaining one Membership and its old position;
- new Memberships appending after the highest existing position in source order;
- source-neutral Mod creation without a `source_references` row;
- a conflicting source owner producing `ImportPersistenceException` with:

```csharp
Assert.Equal("import.source.owner.conflict", exception.Error.Code);
Assert.False(exception.Error.PersistentChangeCommitted);
```

- a failure injected after the first insert rolling back every catalog row;
- pre-cancelled apply leaving every catalog table unchanged.

- [ ] **Step 2: Run the catalog-store tests and record the red state**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Infrastructure.Tests/Tww3Companion.Infrastructure.Tests.csproj --filter SqliteWorkspaceCatalogStoreTests -v minimal
```

Expected: compilation fails because `SqliteWorkspaceCatalogStore` does not exist.

- [ ] **Step 3: Implement the non-mutating read and preview methods**

`SavePreviewAsync` returns exactly:

```csharp
return Task.FromResult(new ImportPreview(
    targetContext,
    candidates,
    Applied: false,
    Resolutions: resolutions,
    ValidationIssues: []));
```

It must not test file existence or open SQLite. Prove this with a test targeting a nonexistent new-Workspace path.

`ReadCandidatesAsync` reads all Mods and optional Source References for current-Workspace matching. It validates the Workspace ID before returning. `ModExistsAsync` validates the same target and executes:

```sql
SELECT EXISTS(SELECT 1 FROM mods WHERE id = $modId);
```

Persist `ImportSourceType.SteamWorkshop` as the invariant text `steam-workshop`. Reading any other stored source type is an invalid-Workspace failure; do not silently coerce an unknown type.

`ReadLibrarySnapshotAsync` uses ordered queries:

```sql
SELECT id, display_name FROM mods
ORDER BY display_name COLLATE NOCASE, id;

SELECT id, display_name FROM collections
ORDER BY display_name COLLATE NOCASE, id;

SELECT collection_id, mod_id FROM collection_memberships
ORDER BY collection_id, position, mod_id;
```

Map rows only into `WorkspaceLibrarySnapshot`; do not expose SQLite types.

- [ ] **Step 4: Implement target verification and typed failures**

Before any current-Workspace read or write, call `WorkspaceFileValidator.OpenAsync` first so the read/write connection factory never creates a missing file. Require schema version 2, then query:

```sql
SELECT id FROM workspace WHERE singleton = 1;
SELECT EXISTS(
    SELECT 1 FROM collections WHERE id = $collectionId);
```

Canonicalize and compare UUID text case-insensitively. On mismatch throw:

```csharp
new ImportPersistenceException(new OperationError(
    "import.workspace.mismatch",
    "The selected file is not the expected Workspace.",
    PersistentChangeCommitted: false,
    "Return to the Workspace and choose the import destination again."));
```

Use `import.collection.missing` for a missing target Collection. Translate SQLite lock, access, corrupt, constraint, and cancellation failures into similarly typed errors with `PersistentChangeCommitted: false`.

- [ ] **Step 5: Implement one atomic additive current-Workspace commit**

Reject `confirm == false` without opening SQLite:

```csharp
if (!confirm)
{
  return new ImportOutcome(
      preview.TargetContext,
      preview.Candidates,
      Applied: false);
}
```

For a confirmed preview:

1. validate the preview;
2. open the schema-v2 database;
3. begin one transaction;
4. verify Workspace and Collection;
5. read the current maximum Membership position;
6. process non-skipped candidates in preview order;
7. reuse the exact source owner or linked Mod;
8. otherwise insert a new UUID Mod;
9. insert the Source Reference if present and verify its owner;
10. retain an existing Membership or append a new one;
11. commit with `CancellationToken.None`;
12. roll back with `CancellationToken.None` on failure.

Use parameterized SQL only. The Membership insert must be:

```sql
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
```

Increment `$position` only when the insert changes one row. Existing Memberships retain their prior position.

Inject an optional callback through the constructor shown in this task's Interfaces block:

```csharp
afterCandidatePersisted?.Invoke(persistedCandidateCount);
```

Tests inject a throwing callback to prove rollback. The production default is `null`. This constructor-injected seam is not an executable test hook and must not inspect `TWW3_COMPANION_TEST_MODE`.

- [ ] **Step 6: Run current-import and Application import suites**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Infrastructure.Tests/Tww3Companion.Infrastructure.Tests.csproj --filter SqliteWorkspaceCatalogStoreTests -v minimal
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj --filter ImportEngineTests -v minimal
git diff --check
```

Expected: all selected tests pass; rollback tests show zero partial rows.

- [ ] **Step 7: Inspect scope and commit**

Run:

```powershell
git status --short
git diff --stat
git diff --name-only
```

Expected: only the catalog adapter, its port adjustment, and their focused tests changed.

Commit:

```powershell
git add src/Tww3Companion.Infrastructure/Storage/SqliteWorkspaceCatalogStore.cs src/Tww3Companion.Application/Importing/IWorkspaceImportStore.cs tests/Tww3Companion.Infrastructure.Tests/Storage/SqliteWorkspaceCatalogStoreTests.cs tests/Tww3Companion.Application.Tests/Importing/ImportEngineTests.cs
git commit -m "feat: persist current workspace imports"
```

---

### Task 4: Create a new Workspace and initial Collection atomically

**Files:**
- Modify: `src/Tww3Companion.Infrastructure/Storage/SqliteWorkspaceCatalogStore.cs`
- Modify: `tests/Tww3Companion.Infrastructure.Tests/Storage/SqliteWorkspaceCatalogStoreTests.cs`

**Interfaces:**
- Consumes: Task 2's `SchemaV2.InitializeAsync`; Task 3's catalog insert and read helpers
- Produces a complete implementation of:

```csharp
Task<ImportOutcome> CommitNewWorkspaceAtomicallyAsync(
    ImportPreview preview,
    CancellationToken cancellationToken = default);
```

- A successful new import returns `ImportOutcome.TargetContext` as the generated current target:

```csharp
ImportTargetContext.ForCurrentWorkspace(
    workspaceId,
    destinationPath,
    collectionId)
```

- [ ] **Step 1: Add failing new-Workspace atomicity tests**

Add:

```csharp
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
```

Add these helpers to the same test class:

```csharp
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

private static async Task<string> ReadWorkspaceNameAsync(
    string path)
{
  await using var connection =
      await new SqliteConnectionFactory().OpenAsync(
          path,
          CancellationToken.None);
  await using var command = connection.CreateCommand();
  command.CommandText =
      "SELECT display_name FROM workspace WHERE singleton = 1;";
  return (string)(await command.ExecuteScalarAsync(
      CancellationToken.None))!;
}
```

`SequenceUuidGenerator` returns the supplied canonical UUIDs in order and throws if a test requests more UUIDs than it declared. `FixedClock.UtcNow` returns `2026-07-24T00:00:00Z`.

Add tests proving:

- an existing destination remains byte-for-byte unchanged;
- a pre-cancelled operation creates no destination or sibling temporary file;
- failure after one candidate creates no destination and removes the exact sibling temporary file;
- final move failure returns a typed uncommitted failure and removes the temporary file;
- generated Workspace, Collection, and Mod UUIDs are stable valid canonical UUID strings;
- the complete database passes `WorkspaceFileValidator` after placement.

- [ ] **Step 2: Run the new-import tests and record the red state**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Infrastructure.Tests/Tww3Companion.Infrastructure.Tests.csproj --filter "FullyQualifiedName~SqliteWorkspaceCatalogStoreTests&FullyQualifiedName~NewImport" -v minimal
```

Expected: new-Workspace cases fail because the method is not implemented.

- [ ] **Step 3: Implement sibling temporary-file ownership**

Use:

```csharp
var destination = target.DestinationPath;
var temporaryPath =
    $"{destination}.{uuidGenerator.NewUuid().Replace("-", "", StringComparison.Ordinal)}.tmp";
```

Before creating it:

```csharp
if (File.Exists(destination))
{
  throw Failure(
      "import.destination.exists",
      "The destination already exists.",
      "Choose a different Workspace destination.");
}

using (fileSystem.CreateWriteProbe(
    Path.GetDirectoryName(destination)!))
{
}
```

Only `temporaryPath` is operation-owned. Cleanup uses the injected exact-path delete delegate and ignores only cleanup failure after preserving the primary error.

- [ ] **Step 4: Build the entire schema-v2 database in one transaction**

Create and validate Domain Workspace identity using the injected UUID generator and clock. Begin a transaction in the temporary database, then:

```csharp
await SchemaV2.InitializeAsync(
    connection,
    transaction,
    workspace,
    CancellationToken.None);

await InsertCollectionAsync(
    connection,
    transaction,
    collectionId,
    target.CollectionDisplayName,
    CancellationToken.None);

await PersistCandidatesAsync(
    connection,
    transaction,
    collectionId,
    preview.Candidates.OfType<ImportCandidate>(),
    initialPosition: 0,
    CancellationToken.None);

await WorkspaceSchemaInspector.ValidateAsync(
    connection,
    transaction,
    SchemaVersion.Current,
    CancellationToken.None);

await transaction.CommitAsync(CancellationToken.None);
```

Close the SQLite connection before:

```csharp
fileSystem.MoveWithoutOverwrite(temporaryPath, destination);
```

Do not copy across filesystems and do not use overwrite mode.

- [ ] **Step 5: Return the generated current target only after placement**

Return:

```csharp
new ImportOutcome(
    ImportTargetContext.ForCurrentWorkspace(
        workspace.Id.ToString(),
        destination,
        collectionId),
    preview.Candidates,
    Applied: true);
```

No success result may be created before `MoveWithoutOverwrite` completes.

- [ ] **Step 6: Run catalog, Workspace-store, and backup tests**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Infrastructure.Tests/Tww3Companion.Infrastructure.Tests.csproj --filter "SqliteWorkspaceCatalogStoreTests|SqliteWorkspaceStoreTests|WorkspaceBackupServiceTests" -v minimal
git diff --check
```

Expected: all selected tests pass; every failure case leaves no partial destination or orphan sibling temporary file.

- [ ] **Step 7: Inspect scope and commit**

Run:

```powershell
git status --short
git diff --stat
git diff --name-only
```

Expected: only the Task 4 catalog adapter and focused tests changed.

Commit:

```powershell
git add src/Tww3Companion.Infrastructure/Storage/SqliteWorkspaceCatalogStore.cs tests/Tww3Companion.Infrastructure.Tests/Storage/SqliteWorkspaceCatalogStoreTests.cs
git commit -m "feat: persist new workspace imports atomically"
```

---

### Task 5: Wire production composition, migration, and reload

**Files:**
- Modify: `src/Tww3Companion.Desktop/Composition/ApplicationComposition.cs`
- Modify: `src/Tww3Companion.Desktop/ViewModels/ShellViewModel.cs`
- Modify: `tests/Tww3Companion.Desktop.Tests/Composition/ApplicationCompositionTests.cs`
- Modify: `tests/Tww3Companion.Desktop.Tests/ViewModels/ShellViewModelTests.cs`
- Modify: `tests/Tww3Companion.Desktop.Tests/ViewModels/ModLibraryViewModelTests.cs`
- Modify: `CHANGELOG.md`
- Modify: `ROADMAP.md`
- Modify: `docs/project-history.md`

**Interfaces:**
- Consumes: Tasks 1–4's `SqliteWorkspaceCatalogStore`, `IWorkspaceCatalogReader`, `MigrateV1ToV2`, Collection-specific targets
- Produces: one production composition root with no `CompositionWorkspaceImportStore`; persisted overlay refresh after successful current-Workspace import

- [ ] **Step 1: Add failing composition and reload tests**

Add a source-level composition assertion:

```csharp
[Fact]
public void ProductionComposition_UsesSqliteCatalogStoreAndHasNoStub()
{
  var desktopDirectory = Path.GetFullPath(Path.Combine(
      AppContext.BaseDirectory,
      "..", "..", "..", "..", "..",
      "src", "Tww3Companion.Desktop"));
  var source = File.ReadAllText(Path.Combine(
      desktopDirectory,
      "Composition",
      "ApplicationComposition.cs"));

  Assert.Contains("new SqliteWorkspaceCatalogStore(", source);
  Assert.DoesNotContain("CompositionWorkspaceImportStore", source);
  Assert.Contains("new MigrateV1ToV2()", source);
}
```

Add a shell test with a recording import service and catalog reader. Build a resolved preview for an explicit current target, configure the service to return an applied outcome, and prove:

```csharp
var target = ImportTargetContext.ForCurrentWorkspace(
    workspaceId,
    workspacePath,
    collectionId);
var preview = new ImportPreview(
    target,
    [ImportCandidate.Linked("candidate:1", "mod-1")],
    Applied: false,
    Resolutions: []);

await shell.ApplyImportPreviewAsync(
    preview,
    confirm: true,
    TestContext.Current.CancellationToken);

Assert.Equal(workspacePath, reader.LastPath);
Assert.Contains(
    shell.ModLibrary.Mods,
    mod => mod.DisplayName == "Persisted Mod");
```

The shell reloads only after applied success. Add a second test in which the service returns `Applied: false` and assert that the catalog reader is not called. Add a third test in which the service throws `ImportPersistenceException` and assert that the existing Workspace error surface shows its message without replacing the prior overlay.

- [ ] **Step 2: Run focused Desktop tests and record the red state**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj --filter "ApplicationCompositionTests|ShellViewModelTests|ModLibraryViewModelTests" -v minimal
```

Expected: failures show the composition stub remains and successful import does not reload the overlay.

- [ ] **Step 3: Build production storage services once**

In `ApplicationComposition.CreateRuntime`, construct:

```csharp
var clock = new SystemClock();
var uuidGenerator = new GuidUuidGenerator();
var connectionFactory = new SqliteConnectionFactory();
var backupService = new WorkspaceBackupService(
    connectionFactory,
    paths,
    clock);
var migrationRunner = new MigrationRunner(
    connectionFactory,
    backupService,
    clock,
    [new MigrateV1ToV2()]);
var workspaceStore = new SqliteWorkspaceStore(
    connectionFactory,
    migrationRunner: migrationRunner);
var catalogStore = new SqliteWorkspaceCatalogStore(
    connectionFactory,
    uuidGenerator,
    clock);
var workspaceLibraryQuery =
    new WorkspaceLibraryQuery(catalogStore);
```

Pass the same `catalogStore` to:

```csharp
new ImportEngine(catalogStore)
```

and remove the complete nested `CompositionWorkspaceImportStore` class.

Refactor `CreateWorkspaceLifecycle` only enough to accept the already-constructed `workspaceStore`, `clock`, and `uuidGenerator`. Do not introduce a general-purpose service container.

- [ ] **Step 4: Carry the active path and Collection target through the shell**

The shell stores the active Workspace path alongside its UUID. `SelectCollection` uses `CollectionDetail.SelectedCollection?.CollectionId` as the current import destination and raises command state. A current-Workspace import is enabled only when UUID, path, and selected Collection ID are all available.

The test hook becomes:

```csharp
public Task RunImportIntoCurrentWorkspaceForTestAsync(
    string workspaceId,
    string workspacePath,
    string collectionId) =>
    RunImportIntoCurrentWorkspaceAsync(
        workspaceId,
        workspacePath,
        collectionId);
```

Do not choose an arbitrary Collection in production. The current import button remains unavailable until a Collection is selected. The later import-UI slice owns user input and preview presentation.

Add the production coordination method that the later preview screen will call:

```csharp
public async Task<ImportOutcome> ApplyImportPreviewAsync(
    ImportPreview preview,
    bool confirm,
    CancellationToken cancellationToken = default)
{
  try
  {
    var outcome = await importService.ApplyAsync(
        preview,
        confirm,
        cancellationToken);
    if (outcome.Applied &&
        outcome.TargetContext is
            ImportTargetContext.CurrentWorkspace current)
    {
      await LoadWorkspaceLibraryAsync(current.WorkspacePath);
    }

    return outcome;
  }
  catch (ImportPersistenceException exception)
  {
    UpdateWorkspaceError(exception.Error.Message);
    throw;
  }
}
```

Do not auto-confirm or call this method from the current import command. Confirmation remains owned by the later preview UI.

Returning Home clears UUID, path, Collection target, and overlay state together.

- [ ] **Step 5: Update milestone documentation**

In `CHANGELOG.md` under `[Unreleased] / Added`, add:

```markdown
- Local Workspace catalog persistence with schema-v2 migration, atomic Collection imports, and library reload
```

In `ROADMAP.md`, add to completed v0.1 slices:

```markdown
- Local Workspace catalog persistence
```

Remove the pending `- Local Workspace persistence` bullet so the milestone does not list the slice as both pending and complete. Do not change the status of unrelated remaining work.

Keep v0.1 `In Progress` because import input/preview UI, JSON backup/restore, and packaging completion remain.

In `docs/project-history.md`, add a short dated persistence milestone recording:

- schema v2;
- first persisted Collection import;
- reload through the library overlay;
- v1 migration with pre-migration backup.

Do not claim v0.1 or the first public release complete.

- [ ] **Step 6: Run focused Desktop and cross-layer tests**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj --filter "ApplicationCompositionTests|ShellViewModelTests|ModLibraryViewModelTests|HomeCompositionTests|MainWindowLayoutTests" -v minimal
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj --filter "ImportEngineTests|WorkspaceQueryTests|DependencyRulesTests" -v minimal
git diff --check
```

Expected: all selected tests pass; the source assertion confirms no production import stub remains.

- [ ] **Step 7: Run complete repository verification separately**

Run each command separately and record every exit code:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' format Tww3Companion.sln --verify-no-changes
& 'C:\Users\steve\.dotnet\dotnet.exe' build Tww3Companion.sln -c Release --no-restore
& 'C:\Users\steve\.dotnet\dotnet.exe' test Tww3Companion.sln -c Release --no-build
git diff --check
```

Expected:

- formatter exits `0`;
- Release build exits `0` with zero errors;
- every Domain, Application, Infrastructure, and Desktop test passes;
- whitespace verification prints nothing.

Do not run build and test concurrently; both use the same Release output directories.

- [ ] **Step 8: Inspect scope and commit**

Run:

```powershell
git status --short
git diff --stat
git diff --name-only
```

Expected:

- only Task 5 composition, shell, tests, and milestone documents changed;
- no `.superpowers/sdd/`, work-packet, scratch, managed Workspace, or SQLite journal file is present.

Commit:

```powershell
git add src/Tww3Companion.Desktop tests/Tww3Companion.Desktop.Tests CHANGELOG.md ROADMAP.md docs/project-history.md
git commit -m "feat: wire persisted workspace catalog"
```

---

## Final Orchestrator and Review Gate

The approved plan is dispatched once through AI Dev Orchestrator as one implementation work item. The `IMP` agent performs the five task commits in order. The orchestrator then sends the complete PR diff, test evidence, and approved plan to `REV`.

`REV` acceptance must explicitly confirm:

- all production `ImportTargetContext` callers name one Collection destination;
- preview construction performs no persistence;
- schema-v1 migration is backed up and transactional;
- schema-v2 creation and migration produce the same required catalog constraints;
- current-Workspace import verifies both Workspace and Collection identity;
- new-Workspace import uses a destination-sibling temporary database and non-overwriting move;
- existing Mods and Membership positions are retained across additive re-import;
- source identity ownership cannot silently change;
- `ApplicationComposition` has no non-persistent import-store stub;
- closing and reopening returns the persisted library snapshot;
- no import UI or later roadmap feature entered the slice;
- complete formatter, Release build, test, and whitespace gates passed.

If `REV` requests changes, the orchestrator returns the same work item to `IMP`, then reruns CI and `REV`. No additional Product Owner approval is required unless the requested change expands or contradicts this approved plan.

The task is complete only after CI passes, `REV` accepts, the orchestrator merges the PR, and the task record plus IMP/REV attempt evidence are committed and pushed in AI Dev Orchestrator.
