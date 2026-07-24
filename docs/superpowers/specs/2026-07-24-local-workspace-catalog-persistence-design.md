# Local Workspace Catalog Persistence Design

## Goal

Persist imported Mods, Collections, Source References, and Collection Memberships in each Workspace's SQLite database so confirmed imports survive closing and reopening the application.

This slice completes the local-persistence part of the v0.1 prototype. It does not add the import input and preview screens.

## Context

The current application can create and open schema-v1 Workspace databases, parse Markdown and Steam inputs, build a shared import preview, and route imports into a new or current Workspace. The collection-library overlay is also wired into the shell.

The remaining gap is the persistence boundary:

- schema v1 stores only Workspace metadata;
- the Desktop composition uses a non-persistent `IWorkspaceImportStore` stub;
- `WorkspaceLibraryQuery` validates the Workspace file but always returns an empty snapshot;
- the current target context does not identify a Collection or give the store enough information to locate and verify the active Workspace file.

The accepted domain model keeps one shared Mod Library inside each Workspace. Collections refer to those shared Mods through Memberships; importing the same Mod into another Collection must not duplicate the Mod.

Implementation tasks for this feature must be routed through the orchestrator every time. The orchestrator must use the rigid `IMP` implementation role followed by the `REV` review role.

## Scope

The slice persists:

- Mod identity and display name;
- one optional canonical source identity for each imported source reference;
- Collections;
- Collection Memberships;
- original import position on each Membership.

The slice supports:

- creating a new Workspace and its initial Collection through one confirmed import;
- importing into one identified existing Collection in the current Workspace;
- reading the persisted library snapshot after import and after reopening the Workspace;
- migrating supported schema-v1 Workspaces to schema v2.

Notes, categories, tags, relationships, compatibility observations, JSON export and restore, and import UI forms are deferred.

## Schema v2

Schema v2 adds normalized catalog tables to the existing Workspace database:

- `mods`
  - stable UUID primary key;
  - non-empty display name.
- `collections`
  - stable UUID primary key;
  - non-empty display name.
- `source_references`
  - source type;
  - canonical external identifier;
  - owning Mod UUID;
  - uniqueness on source type plus external identifier within the Workspace.
- `collection_memberships`
  - Collection UUID;
  - Mod UUID;
  - zero-based imported position;
  - uniqueness on Collection plus Mod;
  - uniqueness on Collection plus position.

Foreign keys enforce ownership and reference validity. The migration enables and validates foreign-key enforcement before exposing the Workspace for editing.

Schema v2 deliberately excludes later organisation and relationship fields. Those features will extend the normalized model through later migrations rather than storing opaque catalog JSON.

Membership position records source-order documentation only. It does not direct the game, enforce load order, or introduce application-level load-order rules.

## Import Identity

`ImportCandidate` separates transient preview identity from persistent source identity:

- `CandidateId` identifies one preview row and is never treated as a Mod identity.
- A structured source identity contains a source type and canonical external identifier.
- Steam identities use the canonical Workshop item ID, regardless of whether the input was a bare ID or Workshop URL.
- Source-neutral Markdown entries have no persisted Source Reference.

Mods and Collections use locally generated stable UUIDs. An exact canonical Source Reference matches its existing Mod automatically. A source-neutral candidate may link to an existing Mod or create a new Mod with a confirmed display name.

Re-importing an existing Mod into another Collection reuses the Workspace-global Mod and adds only the new Membership. Re-importing it into the same Collection retains one Membership rather than creating a duplicate.

For a new Collection, Membership positions follow source order starting at zero. For an existing Collection, existing Memberships retain their positions and newly imported Memberships append after the current highest position in source order. Re-importing an existing Membership does not silently reorder it.

## Import Targets

Every confirmed import targets exactly one Collection.

`ImportTargetContext.NewWorkspace` carries:

- Workspace display name;
- destination database path;
- initial Collection display name.

`ImportTargetContext.CurrentWorkspace` carries:

- expected Workspace UUID;
- active database path;
- target Collection UUID.

The SQLite store verifies that the database's stored Workspace UUID matches the requested UUID before applying a current-Workspace import. It also verifies that the target Collection exists in that Workspace. This prevents stale UI state or an incorrect path from writing into the wrong Workspace or Collection.

This is an intentional breaking extension of the sealed `ImportTargetContext` Application contract. The `ForNewWorkspace` and `ForCurrentWorkspace` factories, every production caller, and all test callers change together in this slice. The build must have no remaining call site that can construct a persistence-capable target without its Collection destination; compile-time failures identify callers that still use the old signatures.

## Persistence Boundary

The Application layer adds a narrow catalog-read port used by `WorkspaceLibraryQuery`. A production SQLite catalog adapter implements both that read port and `IWorkspaceImportStore`, allowing imports and overlay queries to share one catalog implementation without exposing SQL above Infrastructure.

`ApplicationComposition` removes `CompositionWorkspaceImportStore`, constructs the SQLite catalog adapter beside `SqliteWorkspaceStore`, passes it to `ImportEngine`, and passes its read port to `WorkspaceLibraryQuery`. Adding the adapter without replacing the composition stub is incomplete.

Preview construction remains non-mutating. `SavePreviewAsync` preserves the existing application boundary but performs no filesystem or database write. It returns a new unapplied `ImportPreview` containing the supplied target, normalized candidates, and resolutions. It does not cache mutable state or require a Workspace file to exist for a new-Workspace preview.

Only a fully resolved and confirmed preview enters the persistence transaction.

### New Workspace import

1. Parse, normalize, match, and resolve candidates in memory.
2. Create a temporary schema-v2 database as an application-owned sibling of the destination file.
3. Insert Workspace metadata, the initial Collection, Mods, Source References, and Memberships in one transaction.
4. Validate the completed database.
5. Atomically move it to the user-selected destination without overwriting an existing file.
6. Open the new Workspace and read its persisted library snapshot.

Using a sibling temporary file keeps creation and the final non-overwriting move on the destination filesystem in both Installed and Portable modes. The Infrastructure layer probes the destination directory before creation. Cancellation or failure before placement removes only the exact application-owned temporary file. An existing destination is never overwritten.

### Current Workspace import

1. Open and validate the selected Workspace file.
2. Migrate schema v1 to v2 if required.
3. Verify the expected Workspace UUID and target Collection UUID.
4. Match or insert global Mods and Source References.
5. Retain existing Membership positions and append new Memberships in source order.
6. Commit the complete confirmed import in one transaction.
7. Reload the library snapshot for the desktop overlay.

Any failure before commit rolls back the complete import. Caller cancellation must not prevent rollback cleanup.

## Migration

Opening a supported schema-v1 Workspace for editing runs an explicit v1-to-v2 migration through the existing migration runner.

Before migration, the existing SQLite-safe backup service creates a pre-migration backup. Migration and post-migration validation run inside one transaction. If either fails:

- the migration transaction rolls back;
- the original Workspace remains usable;
- the pre-migration backup is retained;
- the Workspace is not exposed for editing.

Schema-v2 creation and migration must produce the same required table and constraint set. A newer unsupported schema is rejected without modification.

## Query Path

`WorkspaceLibraryQuery` reads the schema-v2 catalog and returns:

- all Mods in the Workspace's shared Mod Library;
- all Collections in that Workspace;
- all Collection Memberships.

The query orders Memberships by their persisted import position. It returns the same logical snapshot immediately after import and after closing and reopening the database.

The Desktop layer continues to receive a ready-to-render `WorkspaceLibrarySnapshot`; it does not issue SQL or assemble joins.

## Error Handling

The persistence adapter reports actionable failures for:

- an existing destination;
- inaccessible, locked, corrupt, or invalid files;
- an unsupported newer schema;
- a Workspace UUID mismatch;
- a missing target Collection;
- conflicting Source Reference ownership;
- invalid or unresolved import candidates;
- constraint, transaction, migration, or atomic-placement failure.

Failures state whether any persistent change committed. The application never reports import success before the transaction or new-file placement completes.

Duplicate canonical source identities resolve to the existing Mod. Duplicate Memberships remain one Membership. A conflicting Source Reference that would point to two Mods fails atomically rather than silently changing ownership.

Source Reference ownership conflicts are detected while constructing or resolving the preview whenever the existing catalog makes the conflict knowable. The affected candidate receives a blocking validation result that identifies the source identity and existing Mod, and apply remains unavailable. The database uniqueness constraint is the final race- and corruption-safety backstop; a conflict discovered only at commit returns a user-visible import failure with `PersistentChangeCommitted` false and leaves the preview available for correction.

## Testability and Logging

Infrastructure failure paths use the repository's existing constructor-injected seams, such as filesystem, connection, clock, UUID, and transaction collaborators. Test doubles can fail an exact operation without adding a production command or environment-variable branch.

`TWW3_COMPANION_TEST_MODE=1` remains required for executable smoke hooks and other runtime-only test commands. This slice adds no new executable test hook. Ordinary unit-test dependency injection is not a runtime hook and must not depend on that environment variable.

Application code may depend on Microsoft logging abstractions but not Serilog. Concrete Serilog configuration remains in Infrastructure and composition. Diagnostics must not log imported descriptions, notes, or other user-authored content.

## Verification

Automated tests cover:

- updated import-target factories requiring one Collection destination;
- `SavePreviewAsync` returning a complete unapplied preview without filesystem writes;
- schema-v2 creation and required constraints;
- successful v1-to-v2 migration with a usable pre-migration backup;
- failed migration rollback and backup retention;
- canonical Steam source identity from bare IDs and URLs;
- new-Workspace import creation and reload;
- current-Workspace import into one existing Collection and reload;
- Mod reuse across multiple Collections;
- duplicate Membership handling;
- persisted Membership order;
- source-neutral Mod creation without a fake Source Reference;
- wrong Workspace and missing Collection rejection;
- preview-time Source Reference ownership conflict diagnostics;
- rollback on a mid-import failure;
- cancellation without partial persistence;
- sibling temporary-file placement and cleanup;
- library-query equivalence immediately after import and after reopen;
- Desktop composition using the SQLite adapter with no remaining production stub.

Repository completion requires formatter verification, a Release build, the complete test suite, and `git diff --check`.

## Non-Goals

- Import input, preview, resolution, or Collection-management screens
- Persisting unfinished preview sessions
- Notes, categories, tags, relationships, or compatibility observations
- JSON export or restore
- Cross-Workspace Mod sharing or a machine-global catalog
- Replace or synchronize import behavior
- Deleting Mods or Memberships by omission
- Multiple simultaneously active Workspaces

## Completion Criteria

The slice is complete when a confirmed import can create or update one Collection, the resulting Mods and Memberships survive application restart, the collection-library overlay reads the persisted data, supported schema-v1 files migrate safely, and no production import path still uses the non-persistent composition stub.
