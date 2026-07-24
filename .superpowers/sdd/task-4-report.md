# Task 4 Report: Create a new Workspace and initial Collection atomically

## Status

DONE

## TDD Evidence

### RED (Step 2)

`NewImport` filter: 7 failed with `NotImplementedException` before implementation.

### GREEN (Steps 6–7)

```text
SqliteWorkspaceCatalogStoreTests + SqliteWorkspaceStoreTests + WorkspaceBackupServiceTests: 26 passed
git diff --check: clean (committed files)
```

## Files Changed

| File | Change |
|------|--------|
| `src/Tww3Companion.Infrastructure/Storage/SqliteWorkspaceCatalogStore.cs` | Implemented `CommitNewWorkspaceAtomicallyAsync`; added `InsertCollectionAsync`, `PersistCandidatesAsync`, `CreateWorkspaceIdentity` |
| `tests/Tww3Companion.Infrastructure.Tests/Storage/SqliteWorkspaceCatalogStoreTests.cs` | Added 7 `NewImport_*` tests, `CreateDeterministicStore`, `ReadWorkspaceNameAsync`, `FixedClock`, `MoveFailingAtomicFileSystem` |

## Implementation Notes

- Sibling temp file: `{destination}.{uuid without dashes}.tmp`; destination-exists guard; write probe before create.
- Single transaction: `SchemaV2.InitializeAsync`, collection insert, candidate persist, schema validate, commit; connection closed before `MoveWithoutOverwrite`.
- Success returns `ImportTargetContext.ForCurrentWorkspace` only after move completes.
- Failure cleanup via injected `deleteOwnedFile`; IOException/UnauthorizedAccessException on cleanup ignored after preserving primary error.
- Reuses `ResolveOrCreateModAsync` / `EnsureMembershipAsync` and `afterCandidatePersisted` seam from Task 3.

## Self-Review

- Brief semantics followed verbatim (temp ownership, non-overwriting move, atomic transaction, typed failures).
- All failure cases leave no destination or orphan `.tmp` sibling.
- Scope limited to Task 4 files; `.superpowers/` and `.orchestrator-work-packet.json` excluded from commit.

## Concerns

None blocking. Task 5 still needed to wire production composition.

## Commit

- **Subject:** `feat: persist new workspace imports atomically`
- **Hash:** `310bd21`
- **Files:** catalog store + tests (per brief)
