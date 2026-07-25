# Workspace JSON Backup and Restore Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver deterministic, lossless full-Workspace JSON backup and safe restore into a new or open Workspace while preserving stable identities and existing data on failure.

**Architecture:** Application defines a storage-neutral transfer snapshot and typed backup/restore operations. Infrastructure maps schema-v2 SQLite to a strict `workspace-export-v1` JSON document and reconstructs a validated temporary SQLite database before atomic placement. Desktop adds Home and open-Workspace entry points through one coordinator; JSON parsing and SQL remain outside Views and ViewModels.

**Tech Stack:** .NET 10, C# 14, System.Text.Json, JsonSchema.Net 9.3.0 (tests only), Microsoft.Data.Sqlite, Avalonia 12.1.0, MVVM, xUnit, JSON Schema draft 2020-12.

## Global Constraints

- Implementation tasks must run through AI Dev Orchestrator with Cursor in the rigid `IMP` role and Claude in the independent `REV` role defined by `AGENTS.md`.
- SQLite remains the canonical live Workspace store; JSON is a versioned portable representation.
- The first format identifier is exactly `workspace-export-v1`.
- Restore preserves the Workspace UUID and every record UUID; it never merges or regenerates identities.
- Export and pre-commit restore work is cancellable; final atomic replacement is not.
- User-selected JSON exports are never subject to automatic cleanup.
- Retain the five newest attributable managed automatic backups total per Workspace UUID across `pre-migration` and `pre-restore`.
- No View or ViewModel parses JSON, executes SQL, or manipulates Workspace files directly.
- Domain and Application may depend only on Microsoft logging abstractions; they must not reference Serilog. Concrete Serilog APIs, sinks, and configuration remain confined to Infrastructure and Desktop composition.
- Logs must not contain user-authored notes, imported descriptions, JSON payloads, or unnecessary full local paths.
- The Windows target remains x64 Windows 10 or later, with self-contained installer and portable distributions.
- Backup and restore must remain keyboard-completable and usable at the 1024 × 640 logical minimum.

---

### Task 1: Define the v1 transfer contract and public JSON Schema

**Files:**
- Create: `src/Tww3Companion.Application/Workspaces/Transfer/WorkspaceTransferSnapshot.cs`
- Create: `src/Tww3Companion.Application/Workspaces/Transfer/WorkspaceTransferValidation.cs`
- Create: `schemas/workspace-export-v1.schema.json`
- Create: `tests/Tww3Companion.Application.Tests/Workspaces/WorkspaceTransferValidationTests.cs`
- Modify: `schemas/README.md`

**Interfaces:**
- Consumes: schema-v2 authoritative fields from `workspace`, `mods`, `source_references`, `collections`, and `collection_memberships`.
- Produces: `WorkspaceTransferSnapshot`, `WorkspaceTransferMod`, `WorkspaceTransferSourceReference`, `WorkspaceTransferCollection`, `WorkspaceTransferMembership`, `WorkspaceTransferValidation.Validate`, and `WorkspaceTransferValidation.ContentEquals`.

- [ ] **Step 1: Write failing transfer-validation tests**

Cover one valid populated snapshot and individual failures for non-canonical UUIDs, duplicate Mod IDs, duplicate Collection IDs, duplicate Source Reference `(SourceType, ExternalId)` pairs, missing referenced records, duplicate Collection/Mod memberships, negative positions, and duplicate positions within one Collection. Add a valid case with Membership positions `0`, `2`, and `4` to prove gaps are preserved.

Use this valid fixture shape:

```csharp
private static WorkspaceTransferSnapshot ValidSnapshot() => new(
    Format: "workspace-export-v1",
    Workspace: new WorkspaceTransferWorkspace(
        "11111111-1111-1111-1111-111111111111",
        "My Workspace",
        DateTimeOffset.Parse("2026-07-25T10:00:00Z"),
        DateTimeOffset.Parse("2026-07-25T11:00:00Z")),
    Mods:
    [
      new("22222222-2222-2222-2222-222222222222", "Mod A")
    ],
    SourceReferences:
    [
      new("steam-workshop", "1234567890",
          "22222222-2222-2222-2222-222222222222")
    ],
    Collections:
    [
      new("33333333-3333-3333-3333-333333333333", "Collection A")
    ],
    Memberships:
    [
      new("33333333-3333-3333-3333-333333333333",
          "22222222-2222-2222-2222-222222222222", 0)
    ]);
```

Assert failures use stable codes such as `workspace.transfer.format.unsupported`, `workspace.transfer.identity.invalid`, `workspace.transfer.identity.duplicate`, `workspace.transfer.reference.missing`, and `workspace.transfer.position.invalid`.

- [ ] **Step 2: Run the focused tests and verify RED**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj --filter WorkspaceTransferValidationTests
```

Expected: compilation fails because the transfer types do not exist.

- [ ] **Step 3: Add immutable transfer records**

Create these exact public records:

```csharp
public sealed record WorkspaceTransferSnapshot(
    string Format,
    WorkspaceTransferWorkspace Workspace,
    IReadOnlyList<WorkspaceTransferMod> Mods,
    IReadOnlyList<WorkspaceTransferSourceReference> SourceReferences,
    IReadOnlyList<WorkspaceTransferCollection> Collections,
    IReadOnlyList<WorkspaceTransferMembership> Memberships);

public sealed record WorkspaceTransferWorkspace(
    string Id,
    string DisplayName,
    DateTimeOffset CreatedUtc,
    DateTimeOffset ModifiedUtc);

public sealed record WorkspaceTransferMod(string Id, string DisplayName);

public sealed record WorkspaceTransferSourceReference(
    string SourceType,
    string ExternalId,
    string ModId);

public sealed record WorkspaceTransferCollection(string Id, string DisplayName);

public sealed record WorkspaceTransferMembership(
    string CollectionId,
    string ModId,
    int Position);
```

Implement `WorkspaceTransferValidation.Validate(WorkspaceTransferSnapshot)` as a pure function returning `IReadOnlyList<OperationError>`. Require exact format text, canonical lowercase `D` UUIDs, nonblank trimmed display names, unique keys, complete references, non-negative Membership positions, and unique positions within each Collection. Gaps are valid and must round-trip unchanged because later deletion can leave positions non-contiguous.

Implement `ContentEquals(left, right)` as an explicit scalar comparison plus ordered `SequenceEqual` comparison of every record list. Do not rely on record equality for `IReadOnlyList<T>` properties because interface-list equality is reference equality.

- [ ] **Step 4: Add the strict public schema**

Write `schemas/workspace-export-v1.schema.json` using draft 2020-12. Set `additionalProperties: false` on every object; require every property; use `format: uuid` plus lowercase canonical UUID patterns; use RFC 3339 date-time strings; constrain display names and external IDs to non-empty strings; and constrain Membership positions to integers with minimum `0`.

The root property order must be:

```text
$schema, $id, title, type, additionalProperties, required, properties, $defs
```

The exported document property order must be:

```text
format, workspace, mods, sourceReferences, collections, memberships
```

Update `schemas/README.md` to name this schema as the shipped complete v0.1 Workspace format and explicitly defer single-Collection snapshots.

- [ ] **Step 5: Run focused and Application tests**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj --filter WorkspaceTransferValidationTests
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj
```

Expected: all tests pass.

- [ ] **Step 6: Commit the transfer contract**

```powershell
git add schemas src/Tww3Companion.Application/Workspaces/Transfer tests/Tww3Companion.Application.Tests/Workspaces/WorkspaceTransferValidationTests.cs
git commit -m "feat: define workspace transfer format"
```

---

### Task 2: Read a consistent SQLite snapshot and serialize deterministic JSON

**Files:**
- Create: `src/Tww3Companion.Application/Workspaces/Transfer/IWorkspaceTransferStore.cs`
- Create: `src/Tww3Companion.Application/Workspaces/Transfer/ExportWorkspace.cs`
- Create: `src/Tww3Companion.Infrastructure/Storage/Transfer/WorkspaceJsonCodec.cs`
- Create: `src/Tww3Companion.Infrastructure/Storage/Transfer/SqliteWorkspaceTransferStore.cs`
- Create: `tests/Tww3Companion.Application.Tests/Workspaces/ExportWorkspaceTests.cs`
- Create: `tests/Tww3Companion.Infrastructure.Tests/Storage/WorkspaceJsonCodecTests.cs`
- Create: `tests/Tww3Companion.Infrastructure.Tests/Storage/SqliteWorkspaceTransferStoreTests.cs`
- Modify: `Directory.Packages.props`
- Modify: `tests/Tww3Companion.Infrastructure.Tests/Tww3Companion.Infrastructure.Tests.csproj`

**Interfaces:**
- Consumes: Task 1 transfer records and `OperationResult<T>`.
- Produces:

```csharp
public interface IWorkspaceTransferStore
{
  Task<OperationResult<WorkspaceTransferSnapshot>> ReadSnapshotAsync(
      string workspacePath, CancellationToken cancellationToken);

  Task<OperationResult<WorkspaceTransferSnapshot>> ReadExportAsync(
      string exportPath, CancellationToken cancellationToken);

  Task<OperationResult<string>> WriteExportAsync(
      WorkspaceTransferSnapshot snapshot,
      string exportPath,
      CancellationToken cancellationToken);

  Task<OperationResult<Workspace>> RestoreNewAsync(
      WorkspaceTransferSnapshot snapshot,
      string destinationPath,
      CancellationToken cancellationToken);

  Task<OperationResult<Workspace>> ReplaceAsync(
      WorkspaceTransferSnapshot snapshot,
      string destinationPath,
      CancellationToken cancellationToken);
}
```

- [ ] **Step 1: Write failing codec and snapshot tests**

Prove:

- database row insertion order does not affect snapshot order;
- Mods order by ID, Source References by source type then external ID, Collections by ID, and Memberships by Collection ID then position then Mod ID;
- serialization writes UTF-8 without a BOM, uses two-space indentation, writes camel-case property names in contract order, and ends with one newline;
- serializing the same snapshot twice produces identical bytes;
- a serialized valid snapshot evaluates as valid against `schemas/workspace-export-v1.schema.json`;
- representative invalid documents are rejected by both JsonSchema.Net and the runtime C# validator;
- parsing rejects malformed JSON, a UTF-8 BOM followed by invalid text, unknown properties, missing properties, trailing content, and unsupported format versions;
- paths, application settings, schema migration rows, and backup history do not appear in JSON.

- [ ] **Step 2: Run the focused Infrastructure tests and verify RED**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Infrastructure.Tests/Tww3Companion.Infrastructure.Tests.csproj --filter "WorkspaceJsonCodecTests|SqliteWorkspaceTransferStoreTests"
```

Expected: compilation fails because the codec and transfer store do not exist.

- [ ] **Step 3: Implement the strict JSON codec**

Add the test-only package through central package management:

```xml
<!-- Directory.Packages.props -->
<PackageVersion Include="JsonSchema.Net" Version="9.3.0" />

<!-- Tww3Companion.Infrastructure.Tests.csproj -->
<PackageReference Include="JsonSchema.Net" />
```

Load `schemas/workspace-export-v1.schema.json` in the conformance test and evaluate the serialized document:

```csharp
var schema = Json.Schema.JsonSchema.FromText(await File.ReadAllTextAsync(schemaPath));
var instance = JsonNode.Parse(serializedJson)!;
var result = schema.Evaluate(
    instance,
    new EvaluationOptions
    {
      OutputFormat = OutputFormat.List,
      RequireFormatValidation = true
    });
Assert.True(result.IsValid);
```

Assert `false` for each representative schema violation. The runtime remains the pure C# validator; this test makes divergence from the public schema fail CI.

Use `System.Text.Json` with:

```csharp
private static readonly JsonSerializerOptions Options = new()
{
  PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
  PropertyNameCaseInsensitive = false,
  WriteIndented = true,
  UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
};
```

Serialize a validation-passing, canonically sorted snapshot. Append exactly `"\n"` after serialization. Deserialize from UTF-8 text with strict unmapped-member handling, reject a null root, then call `WorkspaceTransferValidation.Validate`. Map expected failures to bounded `OperationError` results without including JSON content.

- [ ] **Step 4: Implement the SQLite snapshot read**

Open the Workspace through `WorkspaceFileValidator`, require `SchemaVersion.Current`, begin a read transaction, and execute five explicit queries:

```sql
SELECT id, display_name, created_utc, modified_utc
FROM workspace WHERE singleton = 1;

SELECT id, display_name FROM mods ORDER BY id;

SELECT source_type, external_id, mod_id
FROM source_references ORDER BY source_type, external_id;

SELECT id, display_name FROM collections ORDER BY id;

SELECT collection_id, mod_id, position
FROM collection_memberships ORDER BY collection_id, position, mod_id;
```

Build and validate one `WorkspaceTransferSnapshot` before returning it. Do not reuse the UI-oriented `WorkspaceLibrarySnapshot`, because it omits source identity, timestamps, and Membership positions.

- [ ] **Step 5: Implement atomic export**

`ExportWorkspace.ExecuteAsync(workspacePath, exportPath, token)` calls `ReadSnapshotAsync`, then `WriteExportAsync`. `WriteExportAsync` serializes before touching the destination and calls `IAtomicFileSystem.WriteAllTextAtomicallyAsync`. Map cancellation distinctly and leave an existing destination unchanged on serialization or pre-write failure.

- [ ] **Step 6: Run focused, Application, and Infrastructure tests**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj --filter ExportWorkspaceTests
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Infrastructure.Tests/Tww3Companion.Infrastructure.Tests.csproj --filter "WorkspaceJsonCodecTests|SqliteWorkspaceTransferStoreTests"
```

Expected: all focused tests pass.

- [ ] **Step 7: Commit deterministic export**

```powershell
git add Directory.Packages.props src/Tww3Companion.Application/Workspaces/Transfer src/Tww3Companion.Infrastructure/Storage/Transfer tests/Tww3Companion.Application.Tests/Workspaces/ExportWorkspaceTests.cs tests/Tww3Companion.Infrastructure.Tests/Tww3Companion.Infrastructure.Tests.csproj tests/Tww3Companion.Infrastructure.Tests/Storage
git commit -m "feat: export lossless workspace JSON"
```

---

### Task 3: Reconstruct and validate a new Workspace atomically

**Files:**
- Create: `src/Tww3Companion.Application/Workspaces/Transfer/InspectWorkspaceRestore.cs`
- Create: `src/Tww3Companion.Application/Workspaces/Transfer/RestoreWorkspace.cs`
- Create: `src/Tww3Companion.Application/Workspaces/Transfer/WorkspaceRestoreSummary.cs`
- Modify: `src/Tww3Companion.Infrastructure/Storage/Transfer/SqliteWorkspaceTransferStore.cs`
- Create: `tests/Tww3Companion.Application.Tests/Workspaces/RestoreWorkspaceTests.cs`
- Modify: `tests/Tww3Companion.Infrastructure.Tests/Storage/SqliteWorkspaceTransferStoreTests.cs`

**Interfaces:**
- Consumes: `IWorkspaceTransferStore.ReadExportAsync` and Task 1 validation.
- Produces:

```csharp
public sealed record WorkspaceRestoreSummary(
    string WorkspaceId,
    string DisplayName,
    string Format,
    int ModCount,
    int CollectionCount,
    int MembershipCount);

public sealed record InspectedWorkspaceRestore(
    string ExportPath,
    WorkspaceTransferSnapshot Snapshot,
    WorkspaceRestoreSummary Summary);
```

`InspectWorkspaceRestore.ExecuteAsync` returns `OperationResult<InspectedWorkspaceRestore>`. `RestoreWorkspace.RestoreNewAsync` accepts an inspected restore plus destination path.

- [ ] **Step 1: Write failing inspection and new-restore tests**

Prove:

- inspection returns exact counts and performs no destination writes;
- unsupported or invalid exports return failure before destination selection;
- restore re-reads and revalidates the export immediately before construction;
- an export changed after inspection is rejected;
- a pre-existing destination is never overwritten by the new-Workspace path;
- cancellation or injected row-write failure removes the owned temporary database;
- successful restore preserves Workspace, Mod, and Collection IDs, timestamps, Source References, and Membership positions;
- the restored database passes `WorkspaceSchemaInspector.ValidateAsync` and opens normally.

- [ ] **Step 2: Run focused tests and verify RED**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj --filter RestoreWorkspaceTests
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Infrastructure.Tests/Tww3Companion.Infrastructure.Tests.csproj --filter SqliteWorkspaceTransferStoreTests
```

Expected: compilation fails for the restore services.

- [ ] **Step 3: Implement inspection and change detection**

`InspectWorkspaceRestore` reads and validates the export, then creates the summary. `RestoreWorkspace.RestoreNewAsync` calls `ReadExportAsync` again and uses `WorkspaceTransferValidation.ContentEquals` against the inspected snapshot before calling `RestoreNewAsync`. Return `workspace.restore.source.changed` when they differ.

- [ ] **Step 4: Implement temporary schema-v2 reconstruction**

Create an operation-owned path beside the destination:

```csharp
var temporaryPath = $"{destinationPath}.{Guid.NewGuid():N}.restore.tmp";
```

Require the destination not to exist and probe its directory. In one SQLite transaction, using `SchemaV2.InitializeAsync` from the approved local Workspace catalog persistence slice:

1. construct a Domain `Workspace` from the preserved identity, name, and timestamps;
2. call `SchemaV2.InitializeAsync`;
3. insert Mods;
4. insert Source References;
5. insert Collections;
6. insert Memberships with their preserved positions;
7. call `WorkspaceSchemaInspector.ValidateAsync`;
8. commit.

Close the temporary database, validate it again through `WorkspaceFileValidator`, then call `MoveWithoutOverwrite`. Delete only the operation-owned temporary file in `finally`.

- [ ] **Step 5: Run focused tests**

Run the two commands from Step 2.

Expected: all focused tests pass.

- [ ] **Step 6: Commit new-Workspace restore**

```powershell
git add src/Tww3Companion.Application/Workspaces/Transfer src/Tww3Companion.Infrastructure/Storage/Transfer tests/Tww3Companion.Application.Tests/Workspaces/RestoreWorkspaceTests.cs tests/Tww3Companion.Infrastructure.Tests/Storage/SqliteWorkspaceTransferStoreTests.cs
git commit -m "feat: restore JSON into new workspace"
```

---

### Task 4: Replace an open Workspace safely and correct backup retention

**Files:**
- Modify: `src/Tww3Companion.Infrastructure/Settings/IAtomicFileSystem.cs`
- Modify: `src/Tww3Companion.Infrastructure/Settings/AtomicFileSystem.cs`
- Create: `src/Tww3Companion.Infrastructure/Settings/WorkspaceReplacementException.cs`
- Modify: `src/Tww3Companion.Infrastructure/Storage/Backups/WorkspaceBackupService.cs`
- Modify: `src/Tww3Companion.Infrastructure/Storage/Transfer/SqliteWorkspaceTransferStore.cs`
- Modify: `src/Tww3Companion.Application/Workspaces/Transfer/RestoreWorkspace.cs`
- Modify: `tests/Tww3Companion.Infrastructure.Tests/Settings/JsonApplicationSettingsStoreTests.cs`
- Modify: `tests/Tww3Companion.Infrastructure.Tests/Paths/ManagedPathInitializerTests.cs`
- Modify: `tests/Tww3Companion.Infrastructure.Tests/Storage/WorkspaceBackupServiceTests.cs`
- Modify: `tests/Tww3Companion.Infrastructure.Tests/Storage/SqliteWorkspaceCatalogStoreTests.cs`
- Modify: `tests/Tww3Companion.Infrastructure.Tests/Storage/SqliteWorkspaceStoreTests.cs`
- Modify: `tests/Tww3Companion.Infrastructure.Tests/Storage/SqliteWorkspaceTransferStoreTests.cs`
- Modify: `tests/Tww3Companion.Application.Tests/Workspaces/RestoreWorkspaceTests.cs`

**Interfaces:**
- Consumes: inspected restore, Task 3 reconstruction, `WorkspaceBackupService`.
- Produces: `RestoreWorkspace.ReplaceAsync(inspected, destinationPath, confirmed, token)` and an atomic replacement primitive that can restore the original on placement failure.

- [ ] **Step 1: Write failing replacement and retention tests**

Prove:

- `confirmed: false` performs no backup or write;
- export revalidation occurs before backup;
- pre-restore backup failure blocks replacement;
- the automatic backup is a usable SQLite database containing the old Workspace;
- injected reconstruction failure leaves the destination byte-for-byte unchanged;
- injected final-placement failure restores the original destination;
- injected post-placement validation failure restores the original destination from the operation-owned recovery file;
- injected failure while restoring the operation-owned recovery file reports a blocking failure whose `SafeNextAction` contains the exact retained recovery path;
- successful replacement opens the restored Workspace;
- cancellation is honoured before commit and ignored during the final non-cancellable placement section;
- cleanup over interleaved `pre-migration` and `pre-restore` files retains only the newest five total;
- cleanup never deletes unrelated names or a JSON file in or outside the backup directory;
- cleanup runs only after successful replacement.

- [ ] **Step 2: Run focused tests and verify RED**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Infrastructure.Tests/Tww3Companion.Infrastructure.Tests.csproj --filter "WorkspaceBackupServiceTests|SqliteWorkspaceTransferStoreTests"
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj --filter RestoreWorkspaceTests
```

Expected: retention and replacement tests fail.

- [ ] **Step 3: Correct combined automatic-backup retention**

Remove the `GroupBy(reason)` behavior. Continue using the existing exact attributable convention implemented by `ManagedBackupName`: files live under `Backups/<workspace-uuid>/` and match `<workspace-uuid>.<yyyyMMddTHHmmssfffZ>.<pre-migration|pre-restore>.tww3c`. Select all attributable backup names for the Workspace UUID, order by canonical timestamp then filename, and delete only:

```csharp
attributable
    .OrderBy(item => item.Timestamp)
    .ThenBy(item => item.Path, StringComparer.Ordinal)
    .Take(Math.Max(0, attributable.Count - 5));
```

Keep the exact accepted filename pattern and reason validation. Run cleanup only after the protected migration or restore succeeds.

- [ ] **Step 4: Add recoverable atomic replacement**

Extend `IAtomicFileSystem` with:

```csharp
void ReplaceWithRecovery(
    string preparedPath,
    string destinationPath,
    string recoveryPath);
```

The production implementation uses same-volume file operations. It moves the existing destination to the operation-owned recovery path, moves the prepared file into place, and restores the recovery file if the second move fails. It deletes the recovery file only after successful placement. Never pass a managed backup path as `recoveryPath`; the managed pre-restore backup remains independent.

If moving `recoveryPath` back to `destinationPath` also fails, leave the recovery file in place and throw a typed replacement exception carrying `RecoveryPath`. Map it to a blocking `OperationError` with `PersistentChangeCommitted: true`; its `SafeNextAction` names the exact retained recovery path and instructs the user not to overwrite it. Do not log that path or claim that the original destination was restored.

Update every test double implementing `IAtomicFileSystem` in `ManagedPathInitializerTests`, `JsonApplicationSettingsStoreTests`, `SqliteWorkspaceStoreTests`, and `SqliteWorkspaceCatalogStoreTests`. Test doubles not exercising replacement must throw `NotSupportedException` from `ReplaceWithRecovery`; the transfer-store failure doubles must record or inject failure at the requested replacement boundary.

- [ ] **Step 5: Implement confirmed replacement**

The replacement service:

1. returns without mutation when not confirmed;
2. re-reads and structurally compares the export;
3. creates `BackupReason.PreRestore`;
4. constructs and validates the replacement at a temporary path;
5. enters a `CancellationToken.None` commit section;
6. calls `ReplaceWithRecovery`;
7. validates and opens the destination;
8. calls combined backup cleanup;
9. returns the restored Workspace.

If post-placement validation fails, restore from the operation-owned recovery file before reporting failure. Preserve the managed pre-restore backup.

- [ ] **Step 6: Run focused and full Infrastructure tests**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Infrastructure.Tests/Tww3Companion.Infrastructure.Tests.csproj --filter "WorkspaceBackupServiceTests|SqliteWorkspaceTransferStoreTests"
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Infrastructure.Tests/Tww3Companion.Infrastructure.Tests.csproj
```

Expected: all tests pass, including existing atomic settings writes.

- [ ] **Step 7: Commit safe replacement**

```powershell
git add src/Tww3Companion.Application/Workspaces/Transfer src/Tww3Companion.Infrastructure/Settings src/Tww3Companion.Infrastructure/Storage tests/Tww3Companion.Application.Tests/Workspaces/RestoreWorkspaceTests.cs tests/Tww3Companion.Infrastructure.Tests
git commit -m "feat: replace workspaces through safe restore"
```

---

### Task 5: Add the Desktop coordinator, dialogs, and workflow state

**Files:**
- Create: `src/Tww3Companion.Desktop/Services/IWorkspaceTransferCoordinator.cs`
- Create: `src/Tww3Companion.Desktop/Services/WorkspaceTransferCoordinator.cs`
- Create: `src/Tww3Companion.Desktop/Services/WorkspaceFileName.cs`
- Create: `src/Tww3Companion.Desktop/ViewModels/WorkspaceTransferViewModel.cs`
- Create: `src/Tww3Companion.Desktop/Views/WorkspaceTransferView.axaml`
- Create: `src/Tww3Companion.Desktop/Views/WorkspaceTransferView.axaml.cs`
- Modify: `src/Tww3Companion.Desktop/Services/IWorkspaceDialogService.cs`
- Modify: `src/Tww3Companion.Desktop/Services/WorkspaceDialogService.cs`
- Modify: `src/Tww3Companion.Desktop/ViewModels/ShellViewModel.cs`
- Modify: `src/Tww3Companion.Desktop/Views/HomeView.axaml`
- Modify: `src/Tww3Companion.Desktop/Views/MainWindow.axaml`
- Create: `tests/Tww3Companion.Desktop.Tests/Services/WorkspaceTransferCoordinatorTests.cs`
- Create: `tests/Tww3Companion.Desktop.Tests/ViewModels/WorkspaceTransferViewModelTests.cs`
- Modify: `tests/Tww3Companion.Desktop.Tests/ViewModels/ShellViewModelTests.cs`
- Modify: `tests/Tww3Companion.Desktop.Tests/Views/MainWindowLayoutTests.cs`

**Interfaces:**
- Consumes: `ExportWorkspace`, `InspectWorkspaceRestore`, and `RestoreWorkspace`.
- Produces:

```csharp
public enum WorkspaceRestoreDestination { NewWorkspace, ReplaceOpenWorkspace }

public interface IWorkspaceTransferCoordinator
{
  Task<OperationResult<string>> BackupAsync(
      string workspacePath,
      string workspaceDisplayName,
      CancellationToken cancellationToken);

  Task<OperationResult<InspectedWorkspaceRestore>> InspectRestoreAsync(
      CancellationToken cancellationToken);

  Task<OperationResult<Workspace>> RestoreNewAsync(
      InspectedWorkspaceRestore inspected,
      CancellationToken cancellationToken);

  Task<OperationResult<Workspace>> ReplaceOpenAsync(
      InspectedWorkspaceRestore inspected,
      string workspacePath,
      CancellationToken cancellationToken);
}
```

- [ ] **Step 1: Write failing coordinator and ViewModel tests**

Cover:

- Backup is unavailable on Home and available for an open Workspace;
- Restore is available on Home and for an open Workspace;
- Backup cancellation changes no state;
- the injected clock produces the exact suggested backup date;
- filename sanitization trims the name, replaces every `Path.GetInvalidFileNameChars()` character with `-`, and falls back to `Workspace`;
- backup success retains the current library selection and announces completion;
- inspection displays name, format, Mod count, Collection count, Membership count, and “creates” or “replaces; never merges” copy;
- Home restore requests a new `.tww3c` destination and never permits overwrite through that path;
- open restore names the destination and requires explicit confirmation;
- changing or cancelling the selected JSON clears stale inspected state;
- progress state disables conflicting commands;
- cancellation disappears once the non-cancellable commit begins;
- failure retains the summary and states whether anything changed;
- success opens or reloads the restored Workspace and refreshes recent Workspace state.

- [ ] **Step 2: Run focused Desktop tests and verify RED**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj --filter "WorkspaceTransferCoordinatorTests|WorkspaceTransferViewModelTests|ShellViewModelTests|MainWindowLayoutTests"
```

Expected: compilation or assertion failure for missing transfer UI.

- [ ] **Step 3: Extend the dialog boundary**

Add:

```csharp
Task<string?> PromptForBackupPathAsync(
    string suggestedFileName,
    CancellationToken cancellationToken);

Task<string?> PromptForRestoreJsonPathAsync(
    CancellationToken cancellationToken);

Task<string?> PromptForRestoredWorkspacePathAsync(
    string suggestedFileName,
    CancellationToken cancellationToken);

Task<bool> ConfirmWorkspaceReplacementAsync(
    string currentWorkspaceName,
    WorkspaceRestoreSummary source,
    CancellationToken cancellationToken);
```

Use Avalonia `StorageProvider` pickers for `*.json` and `*.tww3c`. Use the platform Save picker so existing JSON destinations receive standard overwrite confirmation. Use a standard owned Avalonia `Window` for the blocking replacement decision, not an inline page control. It must name both Workspaces and state: “This replaces the complete open Workspace. It does not merge data.”

- [ ] **Step 4: Implement the coordinator**

The coordinator owns file-dialog sequencing and calls Application services. Inject the established `IClock`; do not call `DateTimeOffset.UtcNow` directly. Extract the existing private `ShellViewModel.SafeFileName` behavior into `WorkspaceFileName.Sanitize(string)`: trim the input, replace each character returned by `Path.GetInvalidFileNameChars()` with `-`, and return `Workspace` when the result is blank. Use the same helper for Create Workspace and backup suggestions.

Suggest backup names as:

```csharp
$"{WorkspaceFileName.Sanitize(workspaceDisplayName)}-{clock.UtcNow:yyyy-MM-dd}.json"
```

It must never parse JSON or execute SQL. It maps user cancellation to the existing typed cancellation result and passes explicit confirmation to `ReplaceAsync`.

- [ ] **Step 5: Implement the transfer page and shell navigation**

Add a `ShellScreen.WorkspaceTransfer` state and bindings:

```csharp
public ICommand BackupWorkspaceCommand { get; }
public ICommand RestoreFromHomeCommand { get; }
public ICommand RestoreOpenWorkspaceCommand { get; }
public WorkspaceTransferViewModel WorkspaceTransfer { get; }
public bool IsWorkspaceTransferVisible =>
    CurrentScreen == ShellScreen.WorkspaceTransfer;
```

Home gains “Restore Workspace backup”. The open-Workspace sidebar gains a “Workspace data” label followed by “Backup Workspace” and “Restore Workspace”. The page uses standard controls, one summary region, one persistent status/error region, and visible Back/Cancel/Restore actions. Do not add inline custom progress or confirmation widgets; use standard Avalonia progress controls and the standard owned replacement dialog defined in Step 3.

- [ ] **Step 6: Run focused and full Desktop tests**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj --filter "WorkspaceTransferCoordinatorTests|WorkspaceTransferViewModelTests|ShellViewModelTests|MainWindowLayoutTests"
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj
```

Expected: all Desktop tests pass.

- [ ] **Step 7: Commit the Desktop workflow**

```powershell
git add src/Tww3Companion.Desktop tests/Tww3Companion.Desktop.Tests
git commit -m "feat: add workspace backup and restore UI"
```

---

### Task 6: Wire production composition and prove the lossless round trip

**Files:**
- Modify: `src/Tww3Companion.Desktop/Composition/ApplicationComposition.cs`
- Modify: `tests/Tww3Companion.Desktop.Tests/Composition/ApplicationCompositionTests.cs`
- Create: `tests/Tww3Companion.Infrastructure.Tests/Storage/WorkspaceTransferRoundTripTests.cs`

**Interfaces:**
- Consumes: all services from Tasks 1–5.
- Produces: production-composed Backup and Restore actions using the installed/portable `ManagedPaths` and shared `WorkspaceBackupService`.

- [ ] **Step 1: Write failing composition and round-trip tests**

The composition test must prove the production shell receives a non-passive `WorkspaceTransferCoordinator` using the same `SqliteConnectionFactory`, mode-specific `ManagedPaths`, clock, validator, and backup service as Workspace opening and migration.

The round-trip test must:

1. create a schema-v2 Workspace;
2. populate at least two Mods, two Source References, two Collections, and three ordered Memberships;
3. export to JSON;
4. restore to a different `.tww3c` path;
5. export again;
6. assert the two JSON byte arrays are identical;
7. query every authoritative table in both databases and assert equal values.

- [ ] **Step 2: Run focused tests and verify RED**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj --filter ApplicationCompositionTests
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Infrastructure.Tests/Tww3Companion.Infrastructure.Tests.csproj --filter WorkspaceTransferRoundTripTests
```

Expected: production transfer composition is absent.

- [ ] **Step 3: Wire production services**

Construct one `WorkspaceJsonCodec`, `SqliteWorkspaceTransferStore`, `ExportWorkspace`, `InspectWorkspaceRestore`, `RestoreWorkspace`, and `WorkspaceTransferCoordinator`. Reuse the production `WorkspaceBackupService`; do not instantiate a second service with a guessed root. Inject the coordinator through `ShellViewModel.CreateProduction`.

- [ ] **Step 4: Run focused and full solution tests**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj --filter ApplicationCompositionTests
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Infrastructure.Tests/Tww3Companion.Infrastructure.Tests.csproj --filter WorkspaceTransferRoundTripTests
& 'C:\Users\steve\.dotnet\dotnet.exe' test Tww3Companion.sln
```

Expected: all tests pass.

- [ ] **Step 5: Commit production composition**

```powershell
git add src/Tww3Companion.Desktop/Composition tests/Tww3Companion.Desktop.Tests/Composition tests/Tww3Companion.Infrastructure.Tests/Storage/WorkspaceTransferRoundTripTests.cs
git commit -m "feat: compose workspace transfer workflow"
```

---

### Task 7: Align documentation and run release-level verification

**Files:**
- Modify: `docs/architecture/import-export.md`
- Modify: `ROADMAP.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/project-history.md`
- Modify: `docs/development.md` only if commands or fixtures changed

**Interfaces:**
- Consumes: completed behavior and verification evidence from Tasks 1–6.
- Produces: aligned repository documentation and a release-level verification report for the orchestrator attempt.

- [ ] **Step 1: Update architecture and public project state**

Document the exact `workspace-export-v1` boundary, deterministic ordering, stable identity preservation, validation-before-mutation, new versus replacement restore, five-total automatic-backup retention, and deferred single-Collection export.

In `ROADMAP.md`, move JSON backup/restore into completed v0.1 slices. Keep v0.1 `In Progress` with packaging and release verification as the only remaining assessed work.

Add an Unreleased changelog entry and a dated project-history data-portability milestone.

- [ ] **Step 2: Validate Markdown links and whitespace**

Run the repository's local Markdown link check when present. Otherwise resolve every changed relative Markdown link from its containing file. Then run:

```powershell
git diff --check
```

Expected: no missing links and no whitespace errors.

- [ ] **Step 3: Run the complete automated gate**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' format Tww3Companion.sln --verify-no-changes
& 'C:\Users\steve\.dotnet\dotnet.exe' build Tww3Companion.sln -c Release --no-restore
& 'C:\Users\steve\.dotnet\dotnet.exe' test Tww3Companion.sln -c Release --no-build
& 'C:\Users\steve\.dotnet\dotnet.exe' publish src/Tww3Companion.Desktop/Tww3Companion.Desktop.csproj -c Release -r win-x64 --self-contained true
& 'E:\TWW3-Companion\scripts\smoke-test-portable.ps1'
```

Expected: format is clean; Release build has zero warnings and errors; all tests pass; portable publish and smoke test pass.

- [ ] **Step 4: Perform interactive Windows verification**

Using a disposable populated Workspace and export path, verify:

- Backup from an open Workspace;
- restore as new from Home;
- replacement restore from an open Workspace;
- standard overwrite confirmation;
- replacement summary and explicit confirmation;
- keyboard-only completion;
- focus return after cancellation;
- Windows Narrator announcements for progress, success, and failure;
- malformed and unsupported export messages;
- the 1024 × 640 logical minimum and representative increased scaling.

Record each check as pass, fail, or explicitly skipped. Do not convert skipped interactive checks into automated-pass claims.

- [ ] **Step 5: Commit documentation**

```powershell
git add docs/architecture/import-export.md ROADMAP.md CHANGELOG.md docs/project-history.md docs/development.md
git commit -m "docs: record workspace backup and restore"
```

- [ ] **Step 6: Final branch audit**

Run:

```powershell
git status --short
git diff --check
git log --oneline --decorate origin/main..HEAD
```

Expected: clean worktree, no whitespace errors, and only intentional slice commits.

## Orchestrator Execution Contract

After Product Owner approval of this plan, the Architecture Partner must:

1. push the approved design and plan commits;
2. create one AI Dev Orchestrator implementation task referencing this plan and the approved design;
3. bind implementation to Cursor (`IMP`) and independent review to Claude (`REV`);
4. allow the orchestrator to advance `ARCH → IMP → REV → IMP → REV → ARCH` without manual agent substitution;
5. require each review rejection to return its persisted findings to the next Cursor revision packet;
6. stop only for an owner decision that cannot proceed from the approved plan;
7. reconcile GitHub and task-record state forward if a worker exits after pushing but before persistence;
8. treat the task as complete only after accepted Claude review, green required checks, merge, clean repository state, and the verification evidence above.
