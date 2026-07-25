# Import Workspace UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver the complete staged import UI for Markdown, one Steam Collection, and multiple Steam items, with explicit library-only, existing-Collection, or new-Collection destinations and one atomic Apply.

**Architecture:** Revise the Application import contracts so Workspace targeting and optional Collection membership are separate, then extend the SQLite adapter to apply all three destination forms through one shared candidate-write path. A real Infrastructure Steam Web API adapter and a stateless Desktop `IImportTaskCoordinator` feed focused import-session ViewModels; one full-page Avalonia view presents Source, Destination, Preview/Resolve, and Confirm/Apply without adding import state to `ShellViewModel`.

**Tech Stack:** C# / .NET 10, Avalonia 12.1.0, MVVM, `HttpClient`, `System.Text.Json`, Microsoft logging abstractions, Microsoft.Data.Sqlite.Core 10.0.10, SQLitePCLRaw.bundle_winsqlite3 2.1.11, xUnit 3.2.2, existing Application / Infrastructure / Desktop projects.

## Global Constraints

- The approved design is `docs/superpowers/specs/2026-07-25-import-workspace-ui-design.md`.
- Route the approved plan once through AI Dev Orchestrator. The orchestrator must use rigid `IMP` implementation followed by independent Claude `REV` review.
- Use `C:\Users\steve\.dotnet\dotnet.exe` for restore, format, build, test, and publish commands.
- Use TDD for every task: focused failing test, observed RED, minimal implementation, observed GREEN, review, then commit.
- Do not add a new executable test hook. Existing runtime hooks remain gated by `TWW3_COMPANION_TEST_MODE=1`; constructor-injected unit seams do not use that variable.
- Domain, Application, Desktop ViewModels, and the Desktop coordinator may depend only on Microsoft logging abstractions. Serilog APIs and packages remain confined to Infrastructure and existing composition.
- Views and ViewModels must not execute SQL, read files directly, or perform Steam HTTP calls.
- No preview, resolution, Back, or confirmation action may create, open, or mutate a Workspace database.
- Imports are additive. Omission never deletes, replaces, synchronises, or reorders an existing Mod or Membership.
- Library-only import creates or enriches Mods and Source References but creates no Collection or Membership.
- A Steam Collection is a source shape, not a mandatory Collection destination.
- Steam metadata access is initiated only by the user's disclosed Continue/enrichment action. Network failure must retain valid identities.
- Do not log imported prose, clipboard text, display names, notes, full source paths, or full Workspace paths.
- All user-visible workflows must remain usable at 1024 × 640 logical pixels, with keyboard navigation, visible focus, accessible names/states, High Contrast, and text scaling.
- Keep `.superpowers/sdd/` reports, `.superpowers/brainstorm/`, and `.orchestrator-work-packet.json` out of implementation commits.
- The implementation branch must finish with `dotnet format --verify-no-changes`, Release build, full tests, Markdown-link validation, and `git diff --check`.

---

## File and Responsibility Map

### Application

- `ImportMembershipDestination.cs` — sealed destination hierarchy: library-only, existing Collection, or new Collection.
- `ImportTargetContext.cs` — Workspace identity/path plus one explicit membership destination.
- `ImportCandidate.cs` — typed normalized candidate and explicit resolution state.
- `ImportPreviewOperation.cs` — independent Library and Membership outcomes per candidate.
- `ImportPreview.cs` — typed candidates, operations, resolutions, validation issues, warning count, and immutable target.
- `IImportEngine.cs` / `ImportEngine.cs` — build preview, apply one candidate resolution without persistence, validate, and Apply.
- `NewWorkspaceImportSession.cs` / `CurrentWorkspaceImportSession.cs` — validate the revised target shape and delegate atomic persistence.
- `ImportTextDecoder.cs` — strict BOM-aware UTF-8/UTF-16 decoding independent of filesystem APIs.
- Existing Markdown and Steam adapters — source parsing only; no persistence or UI state.

### Infrastructure

- `SqliteWorkspaceCatalogStore.cs` — one candidate-write path plus destination-specific Collection/Membership work.
- `SteamWebApiMetadataClient.cs` — public Steam Workshop collection/item metadata over the documented keyless POST endpoints.

### Desktop services

- `IImportSourceFileService.cs` / `ImportSourceFileService.cs` — Avalonia file picker plus bounded byte read and strict decoding.
- `IImportTaskCoordinator.cs` / `ImportTaskCoordinator.cs` — stateless façade over adapters, metadata, engine preview/resolution, and Apply.

### Desktop ViewModels

- `ImportTaskModels.cs` — source kind, stage, launch context, typed diagnostics, and confirmation summary.
- `ImportWorkspaceViewModel.cs` — session owner and stage navigation.
- `ImportSourceViewModel.cs` — source choice/input and metadata disclosure.
- `ImportDestinationViewModel.cs` — Workspace and membership destination.
- `ImportPreviewViewModel.cs` — rows, filters, counts, and Apply eligibility.
- `ImportResolutionViewModel.cs` — active Needs Attention item and link/create/skip choices.
- `ImportConfirmationViewModel.cs` — immutable summary and Apply lifecycle.

### Desktop views

- `ImportWorkspaceView.axaml` / `.axaml.cs` — the full-page staged task.
- `MainWindow.axaml` — host the import task and keep the existing Workspace shell intact.
- `ShellViewModel.cs` — create launch context, enter/leave the import task, and refresh/navigate after success only.
- `ApplicationComposition.cs` — construct real metadata, file, coordinator, and import-session dependencies once.

---

### Task 1: Make import destinations and previews explicit and resolvable

**Files:**
- Create: `src/Tww3Companion.Application/Importing/ImportMembershipDestination.cs`
- Create: `src/Tww3Companion.Application/Importing/ImportPreviewOperation.cs`
- Modify: `src/Tww3Companion.Application/Importing/ImportTargetContext.cs`
- Modify: `src/Tww3Companion.Application/Importing/ImportCandidate.cs`
- Modify: `src/Tww3Companion.Application/Importing/ImportResolution.cs`
- Modify: `src/Tww3Companion.Application/Importing/ImportPreview.cs`
- Modify: `src/Tww3Companion.Application/Importing/IImportEngine.cs`
- Modify: `src/Tww3Companion.Application/Importing/ImportEngine.cs`
- Modify: `src/Tww3Companion.Application/Importing/NewWorkspaceImportSession.cs`
- Modify: `src/Tww3Companion.Application/Importing/CurrentWorkspaceImportSession.cs`
- Modify: `tests/Tww3Companion.Application.Tests/Importing/ImportEngineTests.cs`
- Modify: `tests/Tww3Companion.Application.Tests/Architecture/DependencyRulesTests.cs`

**Interfaces:**
- Consumes: existing source-neutral `IReadOnlyList<object>` input to `BuildPreviewAsync`
- Produces: `ImportMembershipDestination`, revised `ImportTargetContext`, typed `ImportPreview.Candidates`, `ImportPreview.Operations`, and `IImportEngine.ResolveAsync`

- [ ] **Step 1: Write failing target-contract and typed-preview tests**

Add focused tests proving all valid and invalid combinations:

```csharp
[Fact]
public void Import_targets_separate_workspace_from_membership_destination()
{
  var libraryOnly = ImportMembershipDestination.ForLibraryOnly();
  var existing = ImportMembershipDestination.ForExistingCollection("collection-1");
  var created = ImportMembershipDestination.ForNewCollection("My Collection");

  Assert.Equal(
      new ImportTargetContext.NewWorkspace("Workspace", @"C:\Data\workspace.tww3c", libraryOnly),
      ImportTargetContext.ForNewWorkspace("Workspace", @"C:\Data\workspace.tww3c", libraryOnly));
  Assert.Equal(
      new ImportTargetContext.CurrentWorkspace("workspace-1", @"C:\Data\workspace.tww3c", existing),
      ImportTargetContext.ForCurrentWorkspace("workspace-1", @"C:\Data\workspace.tww3c", existing));
  Assert.IsType<ImportMembershipDestination.NewCollection>(created);
}

[Fact]
public async Task ResolveAsync_replaces_one_candidate_without_persisting()
{
  var store = new FakeWorkspaceImportStore();
  var engine = new ImportEngine(store);
  var preview = await engine.BuildPreviewAsync(
      ImportTargetContext.ForCurrentWorkspace(
          "workspace-1",
          @"C:\Data\workspace.tww3c",
          ImportMembershipDestination.ForLibraryOnly()),
      [ImportCandidate.Unresolved(
          "candidate-1",
          ImportSourceReference.SteamWorkshop("123"))],
      TestContext.Current.CancellationToken);

  var resolved = await engine.ResolveAsync(
      preview,
      ImportCandidate.CreateWithDisplayName(
          "candidate-1",
          "Resolved Mod",
          ImportSourceReference.SteamWorkshop("123")),
      TestContext.Current.CancellationToken);

  Assert.Equal("Resolved Mod", Assert.Single(resolved.Candidates).DisplayName);
  Assert.Equal(0, store.CommitCalls);
}
```

Also change compile-time callers in the test file so no old factory accepts a bare Collection string.

- [ ] **Step 2: Run focused tests and observe RED**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj --filter "FullyQualifiedName~ImportEngineTests|FullyQualifiedName~DependencyRulesTests" -v minimal
```

Expected: compilation fails because `ImportMembershipDestination`, typed preview members, and `ResolveAsync` do not exist.

- [ ] **Step 3: Add the sealed membership-destination and revised target types**

Create:

```csharp
namespace Tww3Companion.Application.Importing;

public abstract record ImportMembershipDestination
{
  private ImportMembershipDestination()
  {
  }

  public sealed record LibraryOnly : ImportMembershipDestination;

  public sealed record ExistingCollection(string CollectionId)
      : ImportMembershipDestination;

  public sealed record NewCollection(string DisplayName)
      : ImportMembershipDestination;

  public static ImportMembershipDestination ForLibraryOnly() => new LibraryOnly();

  public static ImportMembershipDestination ForExistingCollection(string collectionId) =>
      new ExistingCollection(collectionId);

  public static ImportMembershipDestination ForNewCollection(string displayName) =>
      new NewCollection(displayName);
}
```

Revise the target:

```csharp
public sealed record NewWorkspace(
    string DisplayName,
    string DestinationPath,
    ImportMembershipDestination MembershipDestination) : ImportTargetContext;

public sealed record CurrentWorkspace(
    string WorkspaceId,
    string WorkspacePath,
    ImportMembershipDestination MembershipDestination) : ImportTargetContext;
```

Factories take `ImportMembershipDestination`; delete the superseded Collection-string overloads so stale callers fail at compile time.

- [ ] **Step 4: Add typed candidate resolution and operation contracts**

Add:

```csharp
public enum ImportLibraryAction
{
  Create,
  Enrich,
  Existing,
  SuggestedMatch,
  Conflict,
  Skip
}

public enum ImportMembershipAction
{
  None,
  Add,
  Existing,
  Blocked,
  Skip
}

public sealed record ImportPreviewOperation(
    string CandidateId,
    ImportLibraryAction LibraryAction,
    ImportMembershipAction MembershipAction,
    IReadOnlyList<ImportValidationIssue> Issues);
```

Add an unresolved candidate factory:

```csharp
public static ImportCandidate Unresolved(
    string candidateId,
    ImportSourceReference sourceReference) =>
    new(
        candidateId,
        sourceReference,
        LinkedModId: null,
        DisplayName: null,
        IsSkipped: false);
```

Change `ImportPreview.Candidates` to `IReadOnlyList<ImportCandidate>` and add:

```csharp
IReadOnlyList<ImportPreviewOperation>? Operations = null,
int WarningCount = 0
```

`WarningCount` counts warning records attached to non-skipped candidates only.

- [ ] **Step 5: Add non-persistent resolution to the engine**

Extend:

```csharp
Task<ImportPreview> ResolveAsync(
    ImportPreview preview,
    ImportCandidate resolvedCandidate,
    CancellationToken cancellationToken = default);
```

Implement by replacing exactly one candidate ID, recalculating resolutions, validation issues, and operations, then calling only non-mutating `SavePreviewAsync`. Reject missing or duplicate candidate IDs with `ArgumentException`.

Use a single private builder:

```csharp
private async Task<ImportPreview> BuildNormalizedPreviewAsync(
    ImportTargetContext targetContext,
    IReadOnlyList<ImportCandidate> candidates,
    IReadOnlyList<ImportCandidate> existingCandidates,
    CancellationToken cancellationToken)
```

Both `BuildPreviewAsync` and `ResolveAsync` call that builder. `ResolveAsync` never calls a commit method.

- [ ] **Step 6: Validate destination shapes in Application**

`NewWorkspaceImportSession.ValidateDestination` must:

- require Workspace display name and destination path;
- accept `LibraryOnly`;
- accept non-blank `NewCollection.DisplayName`;
- reject `ExistingCollection`.

Current Workspace preview validation must:

- require Workspace UUID and path;
- accept all three membership destination variants;
- reject blank existing Collection UUID or new Collection display name.

Use `ArgumentException` with the invalid parameter named; do not inspect SQLite in Application.

- [ ] **Step 7: Run focused tests and observe GREEN**

Run the Task 1 filter again.

Expected: all focused tests pass and `DependencyRulesTests` confirms no Infrastructure or Serilog dependency entered Application.

- [ ] **Step 8: Review and commit Task 1**

Run:

```powershell
git diff --check
git add src/Tww3Companion.Application tests/Tww3Companion.Application.Tests
git commit -m "feat: support explicit import membership destinations"
```

Review must confirm old factory signatures are gone and `ResolveAsync` performs no persistence.

---

### Task 2: Persist library-only, existing-Collection, and new-Collection imports

**Files:**
- Modify: `src/Tww3Companion.Infrastructure/Storage/SqliteWorkspaceCatalogStore.cs`
- Modify: `tests/Tww3Companion.Infrastructure.Tests/Storage/SqliteWorkspaceCatalogStoreTests.cs`
- Modify: `tests/Tww3Companion.Application.Tests/Importing/ImportEngineTests.cs`

**Interfaces:**
- Consumes: Task 1 `ImportMembershipDestination` and typed `ImportPreview.Candidates`
- Produces: one atomic persistence path supporting all valid destination forms

- [ ] **Step 1: Write failing persistence tests**

Add integration tests:

```csharp
[Fact]
public async Task CurrentWorkspace_LibraryOnly_persists_mod_without_collection_or_membership()
{
  var fixture = await CatalogFixture.CreateAsync();
  var preview = fixture.PreviewForCurrentWorkspace(
      ImportMembershipDestination.ForLibraryOnly(),
      ImportCandidate.CreateWithDisplayName(
          "candidate-1",
          "Library Mod",
          ImportSourceReference.SteamWorkshop("123")));

  var outcome = await fixture.Store.CommitAtomicallyAsync(
      preview,
      confirm: true,
      TestContext.Current.CancellationToken);

  Assert.True(outcome.Applied);
  Assert.Equal(1, await fixture.CountAsync("mods"));
  Assert.Equal(0, await fixture.CountAsync("collections"));
  Assert.Equal(0, await fixture.CountAsync("collection_memberships"));
}

[Fact]
public async Task CurrentWorkspace_NewCollection_creates_collection_and_membership_atomically()
{
  var fixture = await CatalogFixture.CreateAsync();
  var preview = fixture.PreviewForCurrentWorkspace(
      ImportMembershipDestination.ForNewCollection("Imported Collection"),
      ImportCandidate.CreateWithDisplayName("candidate-1", "Member Mod"));

  await fixture.Store.CommitAtomicallyAsync(
      preview,
      confirm: true,
      TestContext.Current.CancellationToken);

  Assert.Equal(1, await fixture.CountAsync("collections"));
  Assert.Equal(1, await fixture.CountAsync("collection_memberships"));
}
```

Add new-Workspace library-only success, existing-Collection verification, new-Collection name collision, rollback after injected candidate failure, and “library-only then later add to two Collections without Mod duplication.”

- [ ] **Step 2: Run focused Infrastructure tests and observe RED**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Infrastructure.Tests/Tww3Companion.Infrastructure.Tests.csproj --filter "FullyQualifiedName~SqliteWorkspaceCatalogStoreTests" -v minimal
```

Expected: failures or compilation errors where the store still reads mandatory `CollectionId` / `CollectionDisplayName`.

- [ ] **Step 3: Split Workspace verification from destination preparation**

Replace mandatory Collection verification with:

```csharp
private async Task<string?> PrepareMembershipDestinationAsync(
    SqliteConnection connection,
    SqliteTransaction transaction,
    ImportMembershipDestination destination,
    CancellationToken cancellationToken)
```

Rules:

```csharp
return destination switch
{
  ImportMembershipDestination.LibraryOnly => null,
  ImportMembershipDestination.ExistingCollection existing =>
      await VerifyAndReturnCollectionAsync(
          connection,
          transaction,
          existing.CollectionId,
          cancellationToken),
  ImportMembershipDestination.NewCollection created =>
      await InsertAndReturnCollectionAsync(
          connection,
          transaction,
          uuidGenerator.NewUuid(),
          created.DisplayName,
          cancellationToken),
  _ => throw new ArgumentException("Unsupported import membership destination.", nameof(destination))
};
```

Workspace UUID/path/schema validation happens before this method.

- [ ] **Step 4: Make candidate persistence membership-optional**

Change the shared candidate loop signature:

```csharp
private async Task PersistCandidatesAsync(
    SqliteConnection connection,
    SqliteTransaction transaction,
    IReadOnlyList<ImportCandidate> candidates,
    string? collectionId,
    CancellationToken cancellationToken)
```

Always resolve/create Mod and Source Reference. Call `EnsureMembershipAsync` only when `collectionId` is non-null.

Do not fork a second library-only loop.

- [ ] **Step 5: Apply the same destination path to new Workspaces**

During sibling-temp creation:

1. initialize schema v2 and Workspace identity;
2. prepare `LibraryOnly` as null or insert `NewCollection`;
3. reject `ExistingCollection` before opening the temp database;
4. persist candidates with optional Collection ID;
5. validate, commit, close, and move without overwrite.

Failure cleanup retains the existing owned-temp semantics.

- [ ] **Step 6: Run focused store and Application tests**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Infrastructure.Tests/Tww3Companion.Infrastructure.Tests.csproj --filter "FullyQualifiedName~SqliteWorkspaceCatalogStoreTests|FullyQualifiedName~SqliteWorkspaceStoreTests" -v minimal
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj --filter "FullyQualifiedName~ImportEngineTests" -v minimal
```

Expected: all destination, rollback, and no-duplication tests pass.

- [ ] **Step 7: Review and commit Task 2**

```powershell
git diff --check
git add src/Tww3Companion.Infrastructure/Storage/SqliteWorkspaceCatalogStore.cs tests/Tww3Companion.Infrastructure.Tests/Storage/SqliteWorkspaceCatalogStoreTests.cs tests/Tww3Companion.Application.Tests/Importing/ImportEngineTests.cs
git commit -m "feat: persist imports with optional collection membership"
```

Review must confirm library-only SQL creates no Collection/Membership and Collection paths remain additive.

---

### Task 3: Add strict text decoding and a real Steam metadata adapter

**Files:**
- Create: `src/Tww3Companion.Application/Importing/ImportTextDecoder.cs`
- Delete: `src/Tww3Companion.Application/Importing/SteamMetadataClient.cs`
- Modify: `src/Tww3Companion.Application/Importing/SteamCollectionImportAdapter.cs`
- Create: `src/Tww3Companion.Infrastructure/Importing/SteamWebApiMetadataClient.cs`
- Modify: `src/Tww3Companion.Infrastructure/Tww3Companion.Infrastructure.csproj`
- Create: `tests/Tww3Companion.Application.Tests/Importing/ImportTextDecoderTests.cs`
- Modify: `tests/Tww3Companion.Application.Tests/Importing/SteamCollectionImportAdapterTests.cs`
- Create: `tests/Tww3Companion.Infrastructure.Tests/Importing/SteamWebApiMetadataClientTests.cs`
- Modify: `tests/Tww3Companion.Application.Tests/Architecture/DependencyRulesTests.cs`

**Interfaces:**
- Consumes: existing `ISteamMetadataClient`, `SteamCollectionMetadata`, and `SteamWorkshopItemMetadata`
- Produces: strict `ImportTextDecoder.Decode(ReadOnlySpan<byte>)` and production `SteamWebApiMetadataClient`

- [ ] **Step 1: Write failing decoder tests**

Cover UTF-8 without BOM, UTF-8 BOM, UTF-16 LE BOM, UTF-16 BE BOM, and invalid bytes:

```csharp
[Theory]
[MemberData(nameof(SupportedDocuments))]
public void Decode_accepts_supported_encodings(byte[] bytes, string expected)
{
  Assert.Equal(expected, ImportTextDecoder.Decode(bytes));
}

[Fact]
public void Decode_rejects_invalid_utf8_without_replacement_characters()
{
  var exception = Assert.Throws<ImportTextDecodingException>(
      () => ImportTextDecoder.Decode([0xC3, 0x28]));

  Assert.Equal("import.source.encoding.unsupported", exception.Code);
}
```

- [ ] **Step 2: Implement strict BOM-aware decoding**

Use strict encoders:

```csharp
private static readonly UTF8Encoding Utf8 = new(
    encoderShouldEmitUTF8Identifier: false,
    throwOnInvalidBytes: true);
private static readonly UnicodeEncoding Utf16LittleEndian = new(
    bigEndian: false,
    byteOrderMark: true,
    throwOnInvalidBytes: true);
private static readonly UnicodeEncoding Utf16BigEndian = new(
    bigEndian: true,
    byteOrderMark: true,
    throwOnInvalidBytes: true);
```

Strip a recognised BOM, decode, normalize CRLF/CR to LF, and throw typed `ImportTextDecodingException` for empty/unsupported data or `DecoderFallbackException`.

- [ ] **Step 3: Write failing Steam HTTP contract tests**

Use a recording `HttpMessageHandler` and exact JSON fixtures:

```csharp
[Fact]
public async Task GetCollectionAsync_posts_documented_form_and_returns_members()
{
  var handler = new RecordingHandler("""
      {"response":{"collectiondetails":[{"publishedfileid":"900","result":1,
      "children":[{"publishedfileid":"111"},{"publishedfileid":"222"}]}]}}
      """);
  var client = new SteamWebApiMetadataClient(new HttpClient(handler)
  {
    BaseAddress = new Uri("https://api.steampowered.com/")
  });

  var result = await client.GetCollectionAsync(
      "900",
      TestContext.Current.CancellationToken);

  Assert.Equal(["111", "222"], result.Members.Select(x => x.WorkshopItemId));
  Assert.Equal(
      "/ISteamRemoteStorage/GetCollectionDetails/v1/",
      handler.LastRequest!.RequestUri!.AbsolutePath);
  Assert.Contains("collectioncount=1", handler.LastBody);
  Assert.Contains("publishedfileids%5B0%5D=900", handler.LastBody);
}
```

Also test item title mapping, HTTP failure, malformed JSON, missing result/title, cancellation, and no API key/query secret.

- [ ] **Step 4: Write the failing Collection partial-enrichment test**

Prove that a valid member identity survives a title lookup failure:

```csharp
[Fact]
public async Task ParseAsync_retains_member_identity_when_title_lookup_fails()
{
  var client = new StubSteamMetadataClient(
      collectionMembers: ["111", "222"],
      itemResults: new Dictionary<string, object>
      {
        ["111"] = new SteamWorkshopItemMetadata("111", "Resolved title"),
        ["222"] = new SteamMetadataException("lookup unavailable")
      });

  var result = await SteamCollectionImportAdapter.ParseAsync(
      "900",
      client,
      TestContext.Current.CancellationToken);

  Assert.Equal(["111", "222"], result.Candidates.Select(x => x.SourceReference));
  Assert.Null(result.Candidates.Single(x => x.SourceReference == "222").DisplayName);
  Assert.Contains(result.Diagnostics, x =>
      x.SourceReference == "222" && x.IsWarning);
}
```

The candidate type must represent a valid Workshop identity without inventing the ID as its display name.

- [ ] **Step 5: Run focused tests and observe RED**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj --filter "FullyQualifiedName~ImportTextDecoderTests" -v minimal
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj --filter "FullyQualifiedName~SteamCollectionImportAdapterTests" -v minimal
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Infrastructure.Tests/Tww3Companion.Infrastructure.Tests.csproj --filter "FullyQualifiedName~SteamWebApiMetadataClientTests" -v minimal
```

Expected: missing decoder and HTTP adapter, plus the existing Collection adapter omits member `222`.

- [ ] **Step 6: Implement strict decoding, identity retention, and the keyless Steam Web API adapter**

Implement `ImportTextDecoder` as specified in Step 2. In `SteamCollectionImportAdapter`, add every valid member as a candidate even when its title lookup fails; attach the warning diagnostic and leave `DisplayName` absent. Do not substitute the Workshop ID into `DisplayName`.

Use Valve's documented public POST endpoints:

- `ISteamRemoteStorage/GetCollectionDetails/v1/`
- `ISteamRemoteStorage/GetPublishedFileDetails/v1/`

Reference: `https://partner.steamgames.com/doc/webapi/isteamremotestorage`.

Constructor:

```csharp
public sealed class SteamWebApiMetadataClient(HttpClient httpClient)
    : ISteamMetadataClient
```

Post `FormUrlEncodedContent` with `collectioncount` / `itemcount` and `publishedfileids[0]`. Deserialize with private DTO records and `JsonSerializerOptions(JsonSerializerDefaults.Web)`. Require result code `1`, non-empty child IDs, and non-empty item title. Convert HTTP, JSON, and response-shape failures to `SteamMetadataException` without including response bodies in messages or logs.

- [ ] **Step 7: Remove the Application stub and verify dependency direction**

Delete `Application/Importing/SteamMetadataClient.cs`. Application retains only `ISteamMetadataClient` and source-neutral records. Infrastructure owns `HttpClient` and JSON transport. Update dependency tests to reject `System.Net.Http` usage from Domain and to keep Serilog out of Application/Desktop coordinator code.

- [ ] **Step 8: Run focused tests and commit Task 3**

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj --filter "FullyQualifiedName~ImportTextDecoderTests|FullyQualifiedName~SteamCollectionImportAdapterTests|FullyQualifiedName~DependencyRulesTests" -v minimal
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Infrastructure.Tests/Tww3Companion.Infrastructure.Tests.csproj --filter "FullyQualifiedName~SteamWebApiMetadataClientTests" -v minimal
git diff --check
git add src/Tww3Companion.Application src/Tww3Companion.Infrastructure tests/Tww3Companion.Application.Tests tests/Tww3Companion.Infrastructure.Tests
git commit -m "feat: add import text decoding and Steam metadata transport"
```

---

### Task 4: Build the stateless Desktop import coordinator and file service

**Files:**
- Create: `src/Tww3Companion.Desktop/Services/IImportSourceFileService.cs`
- Create: `src/Tww3Companion.Desktop/Services/ImportSourceFileService.cs`
- Create: `src/Tww3Companion.Desktop/Services/IImportTaskCoordinator.cs`
- Create: `src/Tww3Companion.Desktop/Services/ImportTaskCoordinator.cs`
- Create: `src/Tww3Companion.Desktop/ViewModels/ImportTaskModels.cs`
- Create: `tests/Tww3Companion.Desktop.Tests/Services/ImportSourceFileServiceTests.cs`
- Create: `tests/Tww3Companion.Desktop.Tests/Services/ImportTaskCoordinatorTests.cs`

**Interfaces:**
- Consumes: `ImportTextDecoder`, source adapters, `ISteamMetadataClient`, and Task 1 `IImportEngine`
- Produces: typed source load, preview, resolution, and Apply façade for ViewModels

- [ ] **Step 1: Define the presentation contracts in failing tests**

Use exact models:

```csharp
public enum ImportSourceKind
{
  Markdown,
  SteamCollection,
  SteamItems
}

public sealed record ImportSourceDocument(string Name, string Text);

public sealed record ImportTaskDiagnostic(
    string Code,
    string Message,
    bool IsBlocking,
    string SafeNextAction);

public sealed record ImportSourceLoadResult(
    IReadOnlyList<object> Candidates,
    IReadOnlyList<ImportTaskDiagnostic> Diagnostics,
    IReadOnlyList<string> DisclosedWorkshopIds);
```

Test:

```csharp
[Fact]
public async Task LoadSourceAsync_does_not_contact_Steam_until_requestMetadata_is_true()
{
  var metadata = new RecordingSteamMetadataClient();
  var coordinator = CreateCoordinator(metadata);
  var request = new ImportSourceRequest(
      ImportSourceKind.Markdown,
      "- 123456789",
      "notes.md",
      RequestMetadata: false);

  var result = await coordinator.LoadSourceAsync(
      request,
      TestContext.Current.CancellationToken);

  Assert.Equal(["123456789"], result.DisclosedWorkshopIds);
  Assert.Empty(metadata.RequestedItemIds);
}
```

Add tests for collection single-ID validation, multiple items, Markdown enrichment, partial metadata failure retaining unresolved identity, and coordinator Apply delegation.

- [ ] **Step 2: Implement the source file service**

Interface:

```csharp
public interface IImportSourceFileService
{
  Task<ImportSourceDocument?> ChooseTextFileAsync(
      CancellationToken cancellationToken = default);
}
```

`ImportSourceFileService` uses `TopLevel.StorageProvider.OpenFilePickerAsync` with one file, `.md` / `.txt` filters, a bounded read, and `ImportTextDecoder.Decode`. Set a 4 MiB v0.1 maximum before allocation. Return only `Path.GetFileName(storageFile.Name)` plus decoded text; never return or log the full path.

- [ ] **Step 3: Implement the coordinator interface**

```csharp
public interface IImportTaskCoordinator
{
  Task<ImportSourceLoadResult> LoadSourceAsync(
      ImportSourceRequest request,
      CancellationToken cancellationToken = default);

  Task<ImportPreview> BuildPreviewAsync(
      ImportTargetContext targetContext,
      IReadOnlyList<object> candidates,
      CancellationToken cancellationToken = default);

  Task<ImportPreview> ResolveAsync(
      ImportPreview preview,
      ImportCandidate resolvedCandidate,
      CancellationToken cancellationToken = default);

  Task<ImportOutcome> ApplyAsync(
      ImportPreview preview,
      CancellationToken cancellationToken = default);
}
```

The implementation is stateless. It never stores source text, destination, preview, or resolutions in fields.

- [ ] **Step 4: Implement source loading and metadata disclosure**

Rules:

- Markdown: parse locally first, collect recognised Workshop IDs, and request item metadata only when `RequestMetadata` is true.
- Steam Collection: validate exactly one ID/URL, disclose the Collection identity, and call the collection adapter only when metadata is requested.
- Steam items: normalize/disclose valid IDs, preserve invalid tokens as diagnostics, and call the item adapter only when requested.
- A failed item lookup produces an unresolved `ImportCandidate` with its canonical Steam Source Reference plus a non-blocking lookup diagnostic; it is not dropped.
- Never invent a display name from an ID or URL.
- `ApplyAsync` always delegates as `engine.ApplyAsync(preview, confirm: true, cancellationToken)`.

- [ ] **Step 5: Run Desktop service tests**

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj --filter "FullyQualifiedName~ImportSourceFileServiceTests|FullyQualifiedName~ImportTaskCoordinatorTests" -v minimal
```

Expected: all file, disclosure, metadata, diagnostics, and delegation tests pass without real UI or network.

- [ ] **Step 6: Review and commit Task 4**

```powershell
git diff --check
git add src/Tww3Companion.Desktop/Services src/Tww3Companion.Desktop/ViewModels/ImportTaskModels.cs tests/Tww3Companion.Desktop.Tests/Services
git commit -m "feat: coordinate import sources and previews"
```

Review must confirm coordinator statelessness and no source/full-path logging.

---

### Task 5: Implement the staged import-session ViewModels

**Files:**
- Create: `src/Tww3Companion.Desktop/ViewModels/ImportWorkspaceViewModel.cs`
- Create: `src/Tww3Companion.Desktop/ViewModels/ImportSourceViewModel.cs`
- Create: `src/Tww3Companion.Desktop/ViewModels/ImportDestinationViewModel.cs`
- Create: `src/Tww3Companion.Desktop/ViewModels/ImportPreviewViewModel.cs`
- Create: `src/Tww3Companion.Desktop/ViewModels/ImportResolutionViewModel.cs`
- Create: `src/Tww3Companion.Desktop/ViewModels/ImportConfirmationViewModel.cs`
- Create: `tests/Tww3Companion.Desktop.Tests/ViewModels/ImportWorkspaceViewModelTests.cs`
- Create: `tests/Tww3Companion.Desktop.Tests/ViewModels/ImportPreviewViewModelTests.cs`
- Create: `tests/Tww3Companion.Desktop.Tests/ViewModels/ImportResolutionViewModelTests.cs`

**Interfaces:**
- Consumes: Task 4 `IImportTaskCoordinator`, `IImportSourceFileService`, and Task 1 typed preview contracts
- Produces: complete four-stage task state, commands, filters, resolution, immutable confirmation, and result events

- [ ] **Step 1: Write failing stage and launch-context tests**

Define:

```csharp
public enum ImportTaskStage
{
  Source,
  Destination,
  Preview,
  Confirmation,
  Finalizing,
  Complete
}

public sealed record ImportLaunchContext(
    bool IsNewWorkspace,
    string? WorkspaceId,
    string? WorkspacePath,
    IReadOnlyList<CollectionSummary> Collections,
    string? SelectedCollectionId);
```

Test:

```csharp
[Fact]
public void SteamCollection_suggests_selected_collection_but_does_not_lock_it()
{
  var subject = CreateSubject(new ImportLaunchContext(
      IsNewWorkspace: false,
      WorkspaceId: "workspace-1",
      WorkspacePath: @"C:\Data\workspace.tww3c",
      Collections: [new CollectionSummary("collection-1", "Current", 0)],
      SelectedCollectionId: "collection-1"));

  subject.Source.Select(ImportSourceKind.SteamCollection);
  subject.OpenDestination();

  Assert.Equal("collection-1", subject.Destination.SelectedCollectionId);
  subject.Destination.SelectLibraryOnly();
  Assert.IsType<ImportMembershipDestination.LibraryOnly>(
      subject.Destination.BuildMembershipDestination());
}
```

Add Home option absence, source/destination fingerprint reuse, changed fingerprint rebuild, discard confirmation, and Apply finalization tests.

- [ ] **Step 2: Implement source and destination ViewModels**

`ImportSourceViewModel` exposes:

- `SelectedKind`;
- `InputText`;
- `SelectedDocumentName`;
- `DisclosedWorkshopIds`;
- `Diagnostics`;
- `IsBusy`;
- `CanContinue`;
- `ChooseFileCommand`;
- `ContinueCommand`.

`ImportDestinationViewModel` exposes:

- Workspace name/path only for Home;
- `IsLibraryOnly`, `IsExistingCollection`, `IsNewCollection`;
- existing Collection list and selection;
- new Collection name;
- `CanContinue`;
- `BuildTargetContext()`.

Existing Collection is absent for `IsNewWorkspace`. Suggestions initialize once and never overwrite an explicit user choice.

- [ ] **Step 3: Implement preview rows, filters, and summary**

Each `ImportPreviewRowViewModel` exposes:

```csharp
string CandidateId
string DisplayName
ImportLibraryAction LibraryAction
ImportMembershipAction MembershipAction
bool HasWarning
bool IsBlocking
bool IsSkipped
```

Filters are an enum, not string comparisons:

```csharp
public enum ImportPreviewFilter
{
  All,
  Additions,
  Enrichments,
  Existing,
  SuggestedMatches,
  Conflicts,
  Warnings,
  Skipped
}
```

`ImportConfirmationSummary` contains exact integer counts and `WarningsRemaining`; it never contains “warnings accepted.”

- [ ] **Step 4: Implement Needs Attention resolution**

`ImportResolutionViewModel` exposes link/create/skip actions. Each action creates a replacement `ImportCandidate` and awaits coordinator `ResolveAsync`. Source-owner collision supports only link to owning Mod or Skip. Scalar conflict exposes the competing values and requires one explicit selected value.

After resolution:

- replace `ImportPreviewViewModel.Preview`;
- move to the next blocking row;
- retain the full preview list;
- set `CanContinue` only when no blocking operation remains.

- [ ] **Step 5: Implement parent session navigation and fingerprint rules**

`ImportWorkspaceViewModel` owns all child ViewModels and the last successful:

```csharp
public sealed record ImportPreviewFingerprint(
    ImportSourceKind SourceKind,
    string SourceDigest,
    ImportTargetContext TargetContext);
```

Hash source text in memory with SHA-256; never log or persist it. On Continue:

- same fingerprint: reuse preview/resolutions without parsing, metadata, or preview call;
- changed fingerprint: rebuild and retain a resolution only when candidate identity and available choices are unchanged;
- any change invalidates confirmation.

Apply sets `Finalizing`, disables Back/Cancel, and emits a typed completion event containing `ImportOutcome`.

- [ ] **Step 6: Run focused ViewModel tests**

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj --filter "FullyQualifiedName~ImportWorkspaceViewModelTests|FullyQualifiedName~ImportPreviewViewModelTests|FullyQualifiedName~ImportResolutionViewModelTests" -v minimal
```

Expected: all stage, default, filter, resolution, fingerprint, failure-retention, and Apply-state tests pass.

- [ ] **Step 7: Review and commit Task 5**

```powershell
git diff --check
git add src/Tww3Companion.Desktop/ViewModels tests/Tww3Companion.Desktop.Tests/ViewModels
git commit -m "feat: add staged import task view models"
```

Review must confirm import-session state did not move into `ShellViewModel`.

---

### Task 6: Add the full-page Avalonia task and wire shell/composition

**Files:**
- Create: `src/Tww3Companion.Desktop/Views/ImportWorkspaceView.axaml`
- Create: `src/Tww3Companion.Desktop/Views/ImportWorkspaceView.axaml.cs`
- Modify: `src/Tww3Companion.Desktop/Views/MainWindow.axaml`
- Modify: `src/Tww3Companion.Desktop/ViewModels/ShellViewModel.cs`
- Modify: `src/Tww3Companion.Desktop/Composition/ApplicationComposition.cs`
- Modify: `src/Tww3Companion.Desktop/App.axaml`
- Modify: `tests/Tww3Companion.Desktop.Tests/ViewModels/ShellViewModelTests.cs`
- Modify: `tests/Tww3Companion.Desktop.Tests/Views/MainWindowLayoutTests.cs`
- Modify: `tests/Tww3Companion.Desktop.Tests/Composition/ApplicationCompositionTests.cs`

**Interfaces:**
- Consumes: Task 5 `ImportWorkspaceViewModel` and Task 4 coordinator/file service
- Produces: user-visible Home/current-Workspace import entry, full staged page, success navigation, and production Steam metadata wiring

- [ ] **Step 1: Write failing shell, composition, and layout tests**

Add tests asserting:

```csharp
[Fact]
public void Home_import_opens_task_with_new_workspace_launch_context()
{
  var shell = ShellViewModel.CreateForTest();

  shell.ImportIntoNewWorkspaceCommand.Execute(null);

  Assert.True(shell.IsImportVisible);
  Assert.True(shell.ImportWorkspace.LaunchContext.IsNewWorkspace);
  Assert.False(shell.ImportWorkspace.Destination.CanChooseExistingCollection);
}

[Fact]
public void Current_import_passes_workspace_and_selected_collection()
{
  var shell = ShellViewModel.CreateForTest();
  shell.SetCurrentWorkspaceImportTargetForTest(
      "workspace-1",
      @"C:\Data\workspace.tww3c",
      "collection-1");

  shell.ImportIntoCurrentWorkspaceCommand.Execute(null);

  Assert.Equal("collection-1", shell.ImportWorkspace.LaunchContext.SelectedCollectionId);
}
```

Layout tests load `ImportWorkspaceView.axaml` and assert four stages, three source actions, full preview table, Needs Attention pane, Back, Continue, Apply, accessible names, and no future roadmap controls.

- [ ] **Step 2: Add an Import screen to shell navigation**

Extend `ShellScreen` with `Import`. `ShellViewModel` owns only:

- current `ImportWorkspaceViewModel`;
- launch context construction;
- `IsImportVisible`;
- enter/leave commands;
- completion handling.

Do not move source text, preview rows, resolutions, or confirmation counts into the shell.

- [ ] **Step 3: Build the full-page Avalonia view**

Use standard controls:

- top step indicator bound to current stage;
- three source-choice buttons;
- multiline TextBox and Choose file;
- destination RadioButtons, ComboBox, and TextBoxes;
- DataGrid or ItemsControl preview with Library and Membership columns;
- filter controls;
- persistent Needs Attention pane;
- immutable confirmation counts;
- Back, Continue, Cancel, and Apply.

At 1024 × 640, place preview and Needs Attention in rows rather than clipping fixed columns. Every button/control has `AutomationProperties.Name`; status TextBlocks use live-region behavior available in the existing Avalonia version.

- [ ] **Step 4: Wire successful completion**

For a new Workspace:

- use `ImportOutcome.TargetContext` to obtain the newly created Workspace identity/path returned by persistence;
- set active Workspace;
- load library;
- enter Workspace screen;
- select created Collection when applicable, otherwise Mod Library.

For current Workspace:

- reload the library snapshot;
- select target existing/new Collection when applicable;
- otherwise select Mod Library.

Failure leaves `ImportWorkspaceViewModel` visible with preview/resolutions intact.

- [ ] **Step 5: Wire production dependencies once**

In `ApplicationComposition`:

```csharp
var steamHttpClient = new HttpClient
{
  BaseAddress = new Uri("https://api.steampowered.com/"),
  Timeout = TimeSpan.FromSeconds(30)
};
var steamMetadataClient = new SteamWebApiMetadataClient(steamHttpClient);
var importEngine = new ImportEngine(catalogStore);
var importFileService = new ImportSourceFileService(() => topLevel);
var importCoordinator = new ImportTaskCoordinator(
    importEngine,
    steamMetadataClient);
```

Pass coordinator and file service into the shell/import ViewModel factory. Extend `ApplicationRuntime.Dispose` to dispose the owned `HttpClient`. Do not create a client per metadata request.

- [ ] **Step 6: Run focused Desktop tests**

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj --filter "FullyQualifiedName~ShellViewModelTests|FullyQualifiedName~MainWindowLayoutTests|FullyQualifiedName~ApplicationCompositionTests|FullyQualifiedName~Import" -v minimal
```

Expected: all import task, layout, composition, reload, and navigation tests pass.

- [ ] **Step 7: Review and commit Task 6**

```powershell
git diff --check
git add src/Tww3Companion.Desktop tests/Tww3Companion.Desktop.Tests
git commit -m "feat: add complete import workspace UI"
```

Review must confirm the full preview remains visible during resolution and no runtime test hook was added.

---

### Task 7: Align documentation and verify the complete vertical slice

**Files:**
- Modify: `CHANGELOG.md`
- Modify: `ROADMAP.md`
- Modify: `docs/project-history.md`
- Modify: `docs/architecture/import-export.md`
- Modify: `docs/architecture/ui.md`
- Modify: `docs/development.md`
- Modify: `RFC/RFC-0005.md` only if its maintained flow text is updated directly; do not change accepted decision semantics
- Test: all solution projects

**Interfaces:**
- Consumes: complete Tasks 1–6
- Produces: aligned maintained documentation and release-ready verification evidence

- [ ] **Step 1: Update maintained architecture**

Document:

- Source → Destination → Preview/Resolve → Confirm/Apply;
- library-only, existing-Collection, and new-Collection destination forms;
- source/destination independence;
- Steam metadata disclosure and explicit request;
- warning count as “warnings remaining”;
- no persistence before Apply;
- the prior mandatory-Collection import rule is superseded.

Keep RFC-0002's domain semantics and RFC-0004's additive/no-synchronisation rules unchanged.

- [ ] **Step 2: Update milestone documents**

Add an Unreleased changelog entry for the complete import UI. Mark the v0.1 import workflow complete in `ROADMAP.md` without marking v0.1 itself complete until backup/restore and packaging are assessed. Add a dated project-history entry only if this is the first fully user-operable import milestone.

- [ ] **Step 3: Add exact manual verification instructions**

In `docs/development.md`, record:

- Markdown paste and file;
- one Steam Collection;
- multiple Steam items paste and file;
- library-only new/current Workspace;
- existing/new Collection;
- metadata partial failure;
- Back unchanged and changed destination;
- blocking resolution and Skip;
- failed Apply retaining preview;
- successful reload;
- 1024 × 640, text scaling, High Contrast, keyboard, and Narrator.

Do not add a new executable smoke command.

- [ ] **Step 4: Run formatter, build, and all tests**

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' format Tww3Companion.sln --verify-no-changes
& 'C:\Users\steve\.dotnet\dotnet.exe' build Tww3Companion.sln -c Release --no-restore
& 'C:\Users\steve\.dotnet\dotnet.exe' test Tww3Companion.sln -c Release --no-build
git diff --check
```

Expected: every command exits 0.

- [ ] **Step 5: Validate local Markdown links**

Run this exact relative-link check over every tracked Markdown file:

```powershell
$repoRoot = (Resolve-Path '.').Path
$missingLinks = [System.Collections.Generic.List[string]]::new()
git ls-files '*.md' | ForEach-Object {
  $markdownPath = Join-Path $repoRoot $_
  $markdownDirectory = Split-Path -Parent $markdownPath
  $content = Get-Content -LiteralPath $markdownPath -Raw
  [regex]::Matches($content, '(?<!\!)\[[^\]]+\]\((?<target>[^)]+)\)') | ForEach-Object {
    $target = $_.Groups['target'].Value.Trim().Trim('<', '>')
    if ($target -notmatch '^(?:https?://|mailto:|#)' -and $target -notmatch '^[A-Za-z]:[\\/]') {
      $relativeTarget = [uri]::UnescapeDataString(($target -split '#', 2)[0])
      if ($relativeTarget -and -not (Test-Path -LiteralPath (Join-Path $markdownDirectory $relativeTarget))) {
        $missingLinks.Add("$markdownPath -> $target")
      }
    }
  }
}
if ($missingLinks.Count -gt 0) {
  $missingLinks | ForEach-Object { Write-Error $_ }
  throw "$($missingLinks.Count) missing local Markdown link(s)."
}
```

Expected: the command exits without errors. Record the exact result in `.superpowers/sdd/task-7-report.md`; keep the report out of the commit.

- [ ] **Step 6: Perform manual Desktop verification**

Use a disposable Workspace outside the repository. Verify every source/destination combination listed in Step 3. Use a real public Steam Collection and item IDs only after the UI discloses the request. Confirm no source or full Workspace path appears in the application log.

Record pass/fail evidence, Windows version, display scale, and any skipped accessibility check in the uncommitted Task 7 report. Do not claim a skipped manual check passed.

- [ ] **Step 7: Commit documentation**

```powershell
git add CHANGELOG.md ROADMAP.md docs/project-history.md docs/architecture/import-export.md docs/architecture/ui.md docs/development.md RFC/RFC-0005.md
git commit -m "docs: record complete import workspace workflow"
```

If `RFC/RFC-0005.md` did not change, omit it from `git add`.

- [ ] **Step 8: Final IMP self-review**

Review the complete branch against every completion criterion in the approved spec. Confirm:

- old mandatory-Collection target factories are absent;
- all three destinations persist correctly;
- Steam production composition uses the real Infrastructure adapter;
- metadata is not requested before explicit user action;
- library and Membership outcomes remain separate;
- warnings are not described as accepted;
- preview/resolution are non-persistent;
- Apply is atomic;
- import state remains outside `ShellViewModel`;
- no new executable test hook exists;
- maintained docs agree.

Fix any issue, rerun the relevant focused tests, then rerun the full verification gate.

---

## Orchestrator Execution and Review Gate

After Product Owner approval, commit and push this plan on `main`, then create one new orchestrator task from that exact commit.

The task must name:

- architecture work item `ARCH-TWW3-0008`;
- implementation work item `IMPL-TWW3-0008`;
- review work item `REV-TWW3-0008`;
- implementation agent `cursor`;
- review agent `claude`;
- the exact plan path and approved plan commit.

Dispatch the task once through AI Dev Orchestrator. `IMP` performs Tasks 1–7 in order with focused tests and task reports. The orchestrator adopts agent-authored commits, pushes the implementation branch, opens the PR, waits for required CI, fetches the PR diff, and invokes Claude with `claude -p -`.

Claude `REV` acceptance must explicitly confirm:

1. the sealed target contract no longer requires a Collection;
2. library-only import creates no Collection or Membership;
3. existing/new Collection imports remain additive and atomic;
4. the production Steam adapter uses the documented public POST endpoints without an API key;
5. metadata requests occur only after disclosed user action;
6. source/file content and full paths are not logged;
7. `IImportTaskCoordinator` is stateless and owns no session;
8. `ImportWorkspaceViewModel`, not `ShellViewModel`, owns the import session;
9. Back/fingerprint behavior preserves or invalidates state exactly as specified;
10. warning semantics, resolution, accessibility, and confirmation match the approved design;
11. full verification and manual evidence are truthful;
12. no fixture adapter or self-review substitutes for Claude.

If `REV` requests changes, the orchestrator must route `REV → IMP → CI → REV` until accepted or a genuine owner-only blocker occurs. Accepted CI-green work auto-merges and records both IMP and REV attempt evidence.
