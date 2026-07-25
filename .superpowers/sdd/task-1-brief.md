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
