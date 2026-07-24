# Task 3 Report: Persist and query current-Workspace catalog imports

## Status

DONE

## Summary

Implemented `SqliteWorkspaceCatalogStore` implementing `IWorkspaceImportStore` and `IWorkspaceCatalogReader` for current-Workspace imports: non-mutating preview, catalog reads, library snapshots, target verification, and one atomic additive commit with rollback and constructor-injected failure seams. `CommitNewWorkspaceAtomicallyAsync` is stubbed for Task 4.

## TDD Evidence

### RED (Step 2)

Before implementation, `SqliteWorkspaceCatalogStoreTests` failed to compile because `SqliteWorkspaceCatalogStore` did not exist (expected RED state).

### GREEN (Steps 6–7)

```text
SqliteWorkspaceCatalogStoreTests: 12 passed
ImportEngineTests: 19 passed
git diff --check: clean (on committed files)
```

## Files Changed

| File | Change |
|------|--------|
| `src/Tww3Companion.Infrastructure/Storage/SqliteWorkspaceCatalogStore.cs` | **Created** — catalog adapter |
| `tests/Tww3Companion.Infrastructure.Tests/Storage/SqliteWorkspaceCatalogStoreTests.cs` | **Created** — 12 focused integration tests + fixture |
| `src/Tww3Companion.Application/Importing/IWorkspaceImportStore.cs` | Comments documenting non-mutating preview and rollback ownership |
| `tests/Tww3Companion.Application.Tests/Importing/ImportEngineTests.cs` | `FakeWorkspaceImportStore.SavePreviewAsync` returns `ValidationIssues: []` |

## Implementation Notes

- **SavePreviewAsync** returns unapplied preview with empty `ValidationIssues`; never opens SQLite or touches filesystem (verified by new-Workspace path test).
- **ReadCandidatesAsync / ModExistsAsync / CommitAtomicallyAsync** call `WorkspaceFileValidator.OpenAsync` first, require schema v2, verify workspace UUID (case-insensitive) and collection existence with parameterized SQL.
- **Steam source type** stored as `steam-workshop`; unknown types fail on read.
- **CommitAtomicallyAsync** uses one transaction, conditional membership insert SQL from brief, position append only on new membership, `afterCandidatePersisted` seam, commit/rollback with `CancellationToken.None`.
- **Error mapping** uses `ImportPersistenceException` with typed codes including `import.workspace.mismatch`, `import.collection.missing`, `import.source.owner.conflict`, and cancellation/lock/corrupt translations.
- **CommitNewWorkspaceAtomicallyAsync** throws `NotImplementedException` (Task 4).

## Self-Review

- Brief SQL, atomicity rules, and test scenarios implemented verbatim.
- Rollback tests confirm zero partial catalog rows after injected failure.
- No `TWW3_COMPANION_TEST_MODE` usage; failure injection via constructor callback only.
- Scope limited to Task 3 files; composition stub unchanged (Task 5).
- `.orchestrator-work-packet.json` and `.superpowers/` left uncommitted.

## Concerns

1. **`clock`, `fileSystem`, `deleteOwnedFile` constructor parameters** — included per brief interface for Task 4 new-Workspace atomic create; unused in Task 3 paths.
2. **Fixture uses `SqliteWorkspaceStore.CreateAsync`** rather than calling internal `SchemaV2.InitializeAsync` directly; equivalent schema-v2 setup with parameterized collection inserts.

## Commit

- **Subject:** `feat: persist current workspace imports`
- **Files:** catalog store, port comments, both test files (per brief)

## Review Fix (Important findings)

### Changes

Extracted shared `MapPersistenceFailure` helper and applied it to:

- **CommitAtomicallyAsync** — after transaction rollback, in-transaction SQLite/IO failures (e.g. constraint on mods insert) map to `ImportPersistenceException` with `PersistentChangeCommitted: false` instead of raw `SqliteException`. Pre-existing typed errors (`import.source.owner.conflict`, etc.) pass through unchanged; non-persistence exceptions (e.g. injected callback) still propagate as-is.
- **ReadLibrarySnapshotAsync** — lock/access/corrupt/cancel/SQLite I/O during snapshot queries now map to the same typed workspace/import errors as `OpenValidatedConnectionAsync`.
- **OpenValidatedConnectionAsync** — refactored to reuse `MapPersistenceFailure` (no behaviour change).

### Test evidence

```text
SqliteWorkspaceCatalogStoreTests: 12 passed
ImportEngineTests: 19 passed
```

### Fix commit

- **Subject:** `fix: map catalog store sqlite errors to typed exceptions`
- **Hash:** `5269579`
- **Files:** `src/Tww3Companion.Infrastructure/Storage/SqliteWorkspaceCatalogStore.cs` only
