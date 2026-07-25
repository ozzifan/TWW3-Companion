# Task 2 Report: Persist library-only, existing-Collection, and new-Collection imports

## Status: DONE

## Commit

- `45bb9afb221884a50234069b541c700eca0cb824` — feat: persist all import membership destinations

## Summary

Extended `SqliteWorkspaceCatalogStore` with one atomic persistence path supporting all `ImportMembershipDestination` forms from Task 1. Workspace verification remains separate from destination preparation; candidate Mod/Source Reference writes always run; Collection/Membership work is conditional on a resolved collection ID.

## Implementation

### `PrepareMembershipDestinationAsync`

- `LibraryOnly` → `null` (no Collection or Membership SQL)
- `ExistingCollection` → `VerifyAndReturnCollectionAsync`
- `NewCollection` → `InsertAndReturnCollectionAsync` with generated UUID

### `PersistCandidatesAsync`

- Signature: `(connection, transaction, candidates, string? collectionId, cancellationToken)`
- Always resolves/creates Mod and Source Reference
- Calls `EnsureMembershipAsync` only when `collectionId` is non-null
- Single shared loop (no library-only fork)

### `CommitAtomicallyAsync` (current Workspace)

- Opens connection with `requireCollection: false`
- Verifies Workspace UUID in transaction, then prepares destination, then persists candidates

### `CommitNewWorkspaceAtomicallyAsync` (new Workspace)

- Rejects `ExistingCollection` before opening temp database
- Prepares `LibraryOnly` as null or inserts `NewCollection`
- Returns outcome `CurrentWorkspace` with `LibraryOnly` or `ExistingCollection(createdId)` membership destination

## Tests Added (Infrastructure)

| Test | Purpose |
|------|---------|
| `CurrentWorkspace_LibraryOnly_persists_mod_without_collection_or_membership` | Library-only creates Mod, no new Collection/Membership |
| `CurrentWorkspace_NewCollection_creates_collection_and_membership_atomically` | New Collection + Membership in one transaction |
| `NewWorkspace_LibraryOnly_creates_workspace_and_mod_without_collection_or_membership` | New Workspace library-only path |
| `CurrentWorkspace_ExistingCollection_verifies_collection_before_persisting` | Existing Collection verification |
| `CurrentWorkspace_NewCollection_with_duplicate_display_name_creates_separate_collection` | Name collision creates separate Collection |
| `CurrentWorkspace_LibraryOnly_then_add_to_two_collections_without_mod_duplication` | Library-first, then additive Collection membership |
| `CurrentWorkspace_LibraryOnly_failure_after_first_candidate_rolls_back_all_rows` | Rollback with library-only destination |

All existing Infrastructure tests updated to use `ImportMembershipDestination` factories.

## Tests Added (Application)

- `NewWorkspace_import_applies_library_only_without_collection_membership` — Application layer accepts and delegates library-only new-Workspace apply

## Test Results

```
SqliteWorkspaceCatalogStoreTests + SqliteWorkspaceStoreTests: 31 passed
ImportEngineTests: 25 passed
```

## Self-Review

1. **Library-only creates no Collection/Membership** — Confirmed: `PrepareMembershipDestinationAsync` returns null; `PersistCandidatesAsync` skips `EnsureMembershipAsync`.
2. **Collection paths remain additive** — `EnsureMembershipAsync` unchanged (`INSERT … WHERE NOT EXISTS`); re-import and multi-Collection tests pass.
3. **No preview/resolution persistence** — Store changes are Apply/Commit only; no changes to `SavePreviewAsync` / `ReadCandidatesAsync` persistence semantics.
4. **New Workspace ExistingCollection rejected** — `ArgumentException` before temp DB open (Application layer also rejects; store is defense-in-depth).

## Concerns

None blocking. Desktop `ShellViewModel` and Desktop tests still use superseded APIs — expected per plan (later tasks).

## Files Changed

- `src/Tww3Companion.Infrastructure/Storage/SqliteWorkspaceCatalogStore.cs`
- `tests/Tww3Companion.Infrastructure.Tests/Storage/SqliteWorkspaceCatalogStoreTests.cs`
- `tests/Tww3Companion.Application.Tests/Importing/ImportEngineTests.cs`
