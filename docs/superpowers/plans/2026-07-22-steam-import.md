# Steam Import Slice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the Steam import slice so TWW3 Companion can import one Steam collection ID at a time or multiple pasted Workshop IDs/URLs as separate single-item imports, with metadata enrichment happening during import.

**Architecture:** Two Steam-facing adapters feed the shared import candidate model. One adapter handles collection import by expanding a single Steam collection ID into member Workshop items; the other handles single-item import by normalizing multiple pasted Workshop IDs/URLs. Both adapters enrich metadata immediately and surface per-item diagnostics for failures, while the existing application-layer preview/handoff path remains the gate before confirmation.

**Tech Stack:** .NET 10, existing TWW3 Companion domain/application layers, existing import preview/handoff path, Steam metadata lookup adapter or service in the application/infrastructure boundary, xUnit.

## Global Constraints

- Steam Collection import accepts one Steam collection ID as the primary input.
- Steam Single Item import accepts multiple pasted Workshop IDs/URLs in one action.
- Metadata enrichment happens during the import action itself, not as a deferred follow-up.
- Partial success is allowed: failed lookups are reported per item and do not block unrelated valid items.
- The UI must make collection import and single-item import visibly distinct.
- The Steam import slice must not write to Steam Workshop or game data folders.
- Steam collection import must not be mixed with pasted single-item import in one UI action.
- Exact source references should be preserved where available.
- Steam metadata lookup is injected behind a small interface so tests can supply deterministic collection/member fixtures without a live network dependency.
- Imports remain additive-only.

---

### Task 1: Define the Steam import boundary and shared candidate/diagnostic shapes

**Files:**
- Create: `src/Tww3Companion.Application/Importing/SteamImportAdapter.cs`
- Create: `src/Tww3Companion.Application/Importing/SteamCollectionImportAdapter.cs`
- Create: `src/Tww3Companion.Application/Importing/SteamSingleItemImportAdapter.cs`
- Create: `src/Tww3Companion.Application/Importing/SteamImportResult.cs`
- Create: `src/Tww3Companion.Application/Importing/SteamImportCandidate.cs`
- Create: `src/Tww3Companion.Application/Importing/SteamImportDiagnostic.cs`
- Modify: `tests/Tww3Companion.Application.Tests/Importing/SteamImportAdapterTests.cs`

**Interfaces:**
- Consumes: the Markdown slice’s shared preview/handoff conventions and the existing import candidate model patterns in `src/Tww3Companion.Application/Importing`
- Produces: separate collection and single-item adapter entry points that can return metadata-enriched candidates plus diagnostics

- [ ] **Step 1: Write the failing tests**

Add focused application tests that pin the public Steam import contract:

```csharp
[Fact]
public async Task ParseSteamCollection_returns_candidates_and_diagnostics_for_collection_id()
{
    var result = await SteamCollectionImportAdapter.ParseAsync("123456789");

    Assert.NotNull(result);
}

[Fact]
public async Task ParseSteamSingleItems_returns_candidates_for_multiple_pasted_ids_and_urls()
{
    var result = await SteamSingleItemImportAdapter.ParseAsync("""
123456789
https://steamcommunity.com/sharedfiles/filedetails/?id=987654321
""");

    Assert.NotEmpty(result.Candidates);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Tww3Companion.Application.Tests --filter "ParseSteamCollection_returns_candidates_and_diagnostics_for_collection_id|ParseSteamSingleItems_returns_candidates_for_multiple_pasted_ids_and_urls" -v normal`

Expected: fail because the Steam adapter entry points do not exist yet.

- [ ] **Step 3: Add the minimal adapter boundary**

Create the smallest public contract needed by the rest of the slice:

```csharp
public static class SteamCollectionImportAdapter
{
    public static Task<SteamImportResult> ParseAsync(string collectionId, CancellationToken cancellationToken = default);
}

public static class SteamSingleItemImportAdapter
{
    public static Task<SteamImportResult> ParseAsync(string pastedIdsAndUrls, CancellationToken cancellationToken = default);
}

public sealed record SteamImportResult(
    IReadOnlyList<SteamImportCandidate> Candidates,
    IReadOnlyList<SteamImportDiagnostic> Diagnostics);
```

If the repository already has a reusable import candidate/diagnostic shape, reuse it instead of introducing parallel types.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Tww3Companion.Application.Tests --filter "ParseSteamCollection_returns_candidates_and_diagnostics_for_collection_id|ParseSteamSingleItems_returns_candidates_for_multiple_pasted_ids_and_urls" -v normal`

Expected: pass after the minimal adapter boundary exists.

- [ ] **Step 5: Commit**

```bash
git add src/Tww3Companion.Application/Importing tests/Tww3Companion.Application.Tests/Importing
git commit -m "feat: add steam import adapter boundary"
```

### Task 2: Implement Steam collection import with immediate metadata enrichment

**Files:**
- Modify: `src/Tww3Companion.Application/Importing/SteamCollectionImportAdapter.cs`
- Modify: `src/Tww3Companion.Application/Importing/SteamImportAdapter.cs`
- Create: `src/Tww3Companion.Application/Importing/ISteamMetadataClient.cs`
- Create: `src/Tww3Companion.Application/Importing/SteamMetadataClient.cs`
- Modify: `src/Tww3Companion.Application/Importing/SteamImportResult.cs`
- Modify: `src/Tww3Companion.Application/Importing/SteamImportCandidate.cs`
- Modify: `src/Tww3Companion.Application/Importing/SteamImportDiagnostic.cs`
- Modify: `tests/Tww3Companion.Application.Tests/Importing/SteamCollectionImportAdapterTests.cs`

**Interfaces:**
- Consumes: `SteamCollectionImportAdapter.ParseAsync(string collectionId, CancellationToken cancellationToken = default)`
- Consumes: `ISteamMetadataClient`
- Produces: collection-member candidates with per-item metadata enrichment, source-aware diagnostics, and partial success for failed lookups

- [ ] **Step 1: Write the failing tests**

Add tests that describe the collection-first behavior and the injectable metadata seam:

```csharp
[Fact]
public async Task ParseSteamCollection_expands_collection_into_member_candidates()
{
    var result = await SteamCollectionImportAdapter.ParseAsync("123456789");

    Assert.NotEmpty(result.Candidates);
}

[Fact]
public async Task ParseSteamCollection_reports_failed_member_lookups_without_blocking_successful_items()
{
    var result = await SteamCollectionImportAdapter.ParseAsync("123456789");

    Assert.Contains(result.Diagnostics, diagnostic => diagnostic.IsLookupFailure);
}

[Fact]
public async Task ParseSteamCollection_uses_injected_metadata_client()
{
    var client = new FakeSteamMetadataClient(/* deterministic collection/member fixture */);
    var result = await SteamCollectionImportAdapter.ParseAsync("123456789", client);

    Assert.NotEmpty(result.Candidates);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Tww3Companion.Application.Tests --filter "ParseSteamCollection_expands_collection_into_member_candidates|ParseSteamCollection_reports_failed_member_lookups_without_blocking_successful_items" -v normal`

Expected: fail until collection expansion, metadata lookup, and the injectable client seam exist.

- [ ] **Step 3: Implement collection expansion and enrichment**

Teach the collection adapter to:

```csharp
- accept exactly one collection ID;
- expand the collection into its member Workshop items;
- call the injected metadata client for the collection and its members;
- enrich each member immediately;
- preserve exact source references when they exist;
- emit per-item diagnostics for failed lookups;
- keep successful items even when some members fail.
```

Keep the importer item-scoped: a bad member lookup should not discard successful members from the same collection.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Tww3Companion.Application.Tests --filter "ParseSteamCollection_expands_collection_into_member_candidates|ParseSteamCollection_reports_failed_member_lookups_without_blocking_successful_items|ParseSteamCollection_uses_injected_metadata_client" -v normal`

Expected: pass with the collection adapter wired to metadata enrichment and the injected client seam.

- [ ] **Step 5: Commit**

```bash
git add src/Tww3Companion.Application/Importing tests/Tww3Companion.Application.Tests/Importing
git commit -m "feat: import steam collections with metadata enrichment"
```

### Task 3: Implement multi-item Steam single import with partial success

**Files:**
- Modify: `src/Tww3Companion.Application/Importing/SteamSingleItemImportAdapter.cs`
- Modify: `src/Tww3Companion.Application/Importing/SteamImportAdapter.cs`
- Modify: `src/Tww3Companion.Application/Importing/SteamImportResult.cs`
- Modify: `src/Tww3Companion.Application/Importing/SteamImportCandidate.cs`
- Modify: `src/Tww3Companion.Application/Importing/SteamImportDiagnostic.cs`
- Modify: `tests/Tww3Companion.Application.Tests/Importing/SteamSingleItemImportAdapterTests.cs`

**Interfaces:**
- Consumes: `SteamSingleItemImportAdapter.ParseAsync(string pastedIdsAndUrls, CancellationToken cancellationToken = default)`
- Produces: multiple per-item candidates from pasted IDs/URLs, immediate metadata enrichment, and item-scoped diagnostics for failures

- [ ] **Step 1: Write the failing tests**

Add tests that lock down the pasted multi-item behavior:

```csharp
[Fact]
public async Task ParseSteamSingleItems_accepts_multiple_ids_and_urls_in_one_paste()
{
    var result = await SteamSingleItemImportAdapter.ParseAsync("""
123456789
https://steamcommunity.com/sharedfiles/filedetails/?id=987654321
""");

    Assert.True(result.Candidates.Count >= 2);
}

[Fact]
public async Task ParseSteamSingleItems_reports_failed_lookups_per_item_without_stopping_the_batch()
{
    var result = await SteamSingleItemImportAdapter.ParseAsync("""
123456789
bad input
987654321
""");

    Assert.Contains(result.Diagnostics, diagnostic => diagnostic.IsLookupFailure);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Tww3Companion.Application.Tests --filter "ParseSteamSingleItems_accepts_multiple_ids_and_urls_in_one_paste|ParseSteamSingleItems_reports_failed_lookups_per_item_without_stopping_the_batch" -v normal`

Expected: fail until multi-item parsing and item-level partial success exist.

- [ ] **Step 3: Implement multi-item normalization and enrichment**

Teach the single-item adapter to:

```csharp
- parse multiple pasted IDs and URLs in one action;
- normalize valid Workshop identities;
- enrich each item immediately;
- preserve exact source references where supplied;
- keep successful items even if one item fails lookup;
- surface one diagnostic per failed item.
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Tww3Companion.Application.Tests --filter "ParseSteamSingleItems_accepts_multiple_ids_and_urls_in_one_paste|ParseSteamSingleItems_reports_failed_lookups_per_item_without_stopping_the_batch" -v normal`

Expected: pass with the multi-item path wired correctly.

- [ ] **Step 5: Commit**

```bash
git add src/Tww3Companion.Application/Importing tests/Tww3Companion.Application.Tests/Importing
git commit -m "feat: import multiple steam items with metadata enrichment"
```

### Task 4: Wire Steam import through the validated handoff and verify the slice

**Files:**
- Modify: `src/Tww3Companion.Application/Importing/SteamCollectionImportAdapter.cs`
- Modify: `src/Tww3Companion.Application/Importing/SteamSingleItemImportAdapter.cs`
- Modify: `src/Tww3Companion.Application/Importing/SteamImportAdapter.cs`
- Modify: `src/Tww3Companion.Application/Importing/SteamImportResult.cs`
- Modify: `src/Tww3Companion.Application/Importing/SteamImportCandidate.cs`
- Modify: `src/Tww3Companion.Application/Importing/SteamImportDiagnostic.cs`
- Modify: `tests/Tww3Companion.Application.Tests/Importing/SteamImportServiceTests.cs`

**Interfaces:**
- Consumes: collection and single-item Steam import results from Tasks 2 and 3
- Produces: preview-ready Steam import candidates that feed the same validated handoff path used by the Markdown slice

- [ ] **Step 1: Write the failing tests**

Add tests that prove the Steam adapters feed the preview/handoff path and keep the action split visible:

```csharp
[Fact]
public async Task SteamCollection_preview_uses_the_collection_action()
{
    var result = await SteamCollectionImportAdapter.ParseAsync("123456789");

    Assert.NotNull(result);
}

[Fact]
public async Task SteamSingleItem_preview_accepts_multiple_items()
{
    var result = await SteamSingleItemImportAdapter.ParseAsync("123456789\n987654321");

    Assert.NotNull(result);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Tww3Companion.Application.Tests --filter "SteamCollection_preview_uses_the_collection_action|SteamSingleItem_preview_accepts_multiple_items" -v normal`

Expected: fail until the Steam paths are wired into the shared preview/handoff boundary.

- [ ] **Step 3: Connect the Steam results to the preview/handoff boundary**

Keep the behavior aligned with the spec:

```csharp
- collection import remains one collection ID per action;
- single-item import remains one pasted multi-ID/URL action;
- metadata enrichment happens during import;
- failed lookups stay item-scoped;
- the validated handoff gates confirmation;
- the UI can present the two workflows distinctly.
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Tww3Companion.Application.Tests --filter "SteamCollection_preview_uses_the_collection_action|SteamSingleItem_preview_accepts_multiple_items" -v normal`

Expected: pass with the Steam slice fully wired.

- [ ] **Step 5: Commit**

```bash
git add src/Tww3Companion.Application/Importing tests/Tww3Companion.Application.Tests/Importing
git commit -m "feat: wire steam import through preview and handoff"
```

### Task 5: Verify the full slice with format, build, tests, and diff check

**Files:**
- None expected unless the prior tasks expose a small fix

**Interfaces:**
- Consumes: the completed Steam import slice
- Produces: a verified branch ready for review

- [ ] **Step 1: Run the full verification commands**

Run:

```powershell
dotnet format Tww3Companion.sln --verify-no-changes
dotnet build Tww3Companion.sln -c Release --no-restore
dotnet test Tww3Companion.sln -c Release --no-build
git diff --check
```

Expected: all commands succeed.

- [ ] **Step 2: Fix any small verification issues**

If formatting or test failures appear, make the smallest code change needed, then rerun the same verification commands.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: complete steam import slice"
```
