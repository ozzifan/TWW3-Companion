# IMPL-TWW3-0008 Final Fix Report

**Branch:** `impl/TWW3-0008-import-workspace-ui`  
**Base HEAD:** `d1e8e0477a95127f1a1dbc02c086cf96108df82e`  
**Fix HEAD:** `401e387`  
**Date:** 2026-07-25

## Summary

Addressed all five Critical/Important findings from the whole-branch review. Five focused commits; full Release test suite green.

## Commits

| Commit | Message |
|--------|---------|
| `5b9744c` | fix: allow current-workspace import from Mod Library |
| `037e122` | fix: select created collection after current-workspace import |
| `ef7047a` | fix: accept Steam Collection URLs in import coordinator |
| `7acec7b` | fix: improve import preview library and membership actions |
| `401e387` | docs: correct Steam Collection disclosure checklist |

## Fixes

### 1. Current-Workspace Import disabled on Mod Library (Critical)

**Problem:** `ImportIntoCurrentWorkspaceCommand` and `EnterImportForCurrentWorkspace()` required `currentCollectionId`. Selecting Mod Library cleared it, blocking import.

**Change:** CanExecute and launch now require only `currentWorkspaceId` and `currentWorkspacePath`. Launch context passes `SelectedCollectionId: null` when no collection is selected.

**Tests:** `ShellViewModelTests.Current_import_enabled_on_mod_library_without_selected_collection`, `Current_import_from_mod_library_passes_null_selected_collection`.

### 2. Post-apply navigation ignores NewCollection (Important)

**Problem:** `CommitAtomicallyAsync` returned the original `NewCollection` destination. Shell `SelectMembershipDestination` only handles `LibraryOnly` and `ExistingCollection`, so post-apply navigation skipped newly created collections.

**Change:** After successful current-workspace commit, outcome remaps `NewCollection` to `ExistingCollection(createdId)` — mirroring `CommitNewWorkspaceAtomicallyAsync`.

**Tests:** `SqliteWorkspaceCatalogStoreTests.CurrentWorkspace_NewCollection_outcome_remaps_to_created_collection`.

### 3. Steam Collection URLs (Important)

**Problem:** Coordinator accepted only numeric collection IDs; docs and architecture specify ID or supported URL.

**Change:** Extracted shared `SteamImportAdapter.TryGetWorkshopIdentity` with public `TryGetWorkshopItemId` and `TryGetCollectionId`. Coordinator and `SteamCollectionImportAdapter` use shared parsing. Updated validation copy.

**Tests:** `ImportTaskCoordinatorTests.LoadSourceAsync_accepts_steam_collection_url_without_calling_metadata`.

### 4. Engine preview operations incomplete (Important)

**Problem:** `DetermineLibraryAction` never returned `Enrich` or scalar `Conflict`; `DetermineMembershipAction` never returned `Existing`.

**Change:**
- **Enrich:** Linked mod where candidate fills blank display name or adds missing source reference.
- **Conflict:** `import.scalar.conflict` when linked mod and candidate both have non-empty differing display names (distinct from blocking `import.source.owner.conflict`).
- **Membership Existing:** Query collection members via new `IWorkspaceImportStore.ReadCollectionMemberModIdsAsync`; mark linked mods already in target collection.

**Tests:** `ImportEngineTests.Preview_marks_matched_mod_with_blank_display_name_as_enrich`, `Preview_marks_scalar_display_name_conflict`, `Preview_marks_existing_collection_membership_as_unchanged`.

### 5. development.md Steam Collection checklist (Important)

**Change:** Row now states collection ID disclosed on Source Continue; member Workshop IDs after Destination Continue.

## Test Summary

### Focused (Debug)

| Project | Filter | Passed |
|---------|--------|--------|
| Application.Tests | ImportEngineTests | 28 |
| Desktop.Tests | ShellViewModelTests, ImportTaskCoordinatorTests | 24 |
| Infrastructure.Tests | CurrentWorkspace_NewCollection | 3 |

### Full Release suite

```
dotnet test Tww3Companion.sln -c Release
```

| Project | Passed |
|---------|--------|
| Domain.Tests | 16 |
| Application.Tests | 74 |
| Infrastructure.Tests | 83 |
| Desktop.Tests | 94 |
| **Total** | **267** |

Failed: 0 | Skipped: 0

## Remaining Gaps

- **Enrich scope:** Only display-name fill and source-reference addition are detected. Notes, categories, and other RFC-0004 scalar fields are not yet compared — no store preview API exposes them on `ImportCandidate`.
- **NewCollection shell navigation:** Store outcome remapping is sufficient for shell `SelectMembershipDestination`; no separate shell integration test added (store test covers the contract).
- **Minor review items** (step indicator, dead `ConfirmDiscardImportCommand`, RFC-0005 text) intentionally deferred per task scope.

## Files Changed (excluding orchestrator/superpowers artifacts)

- `src/Tww3Companion.Desktop/ViewModels/ShellViewModel.cs`
- `src/Tww3Companion.Infrastructure/Storage/SqliteWorkspaceCatalogStore.cs`
- `src/Tww3Companion.Application/Importing/SteamImportAdapter.cs`
- `src/Tww3Companion.Application/Importing/SteamCollectionImportAdapter.cs`
- `src/Tww3Companion.Desktop/Services/ImportTaskCoordinator.cs`
- `src/Tww3Companion.Application/Importing/IWorkspaceImportStore.cs`
- `src/Tww3Companion.Application/Importing/ImportEngine.cs`
- `docs/development.md`
- Corresponding test files under `tests/`
