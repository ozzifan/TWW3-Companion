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
