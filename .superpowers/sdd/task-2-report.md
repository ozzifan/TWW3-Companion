# Task 2 Report: Add schema v2 and safe v1 migration

## Status

**DONE**

## Commits

| SHA | Subject |
|-----|---------|
| `b489d3e` | feat: add workspace catalog schema v2 |

## Summary

Implemented workspace catalog schema v2 with transactional v1→v2 migration, pre-migration backup, and version-aware structure validation per the task brief.

### New files

- `SchemaV2.cs` — Creates all seven v2 tables and seed metadata/migration rows inside a caller-supplied transaction.
- `MigrateV1ToV2.cs` — Adds the four catalog tables (`mods`, `collections`, `source_references`, `collection_memberships`) during migration.
- `WorkspaceSchemaInspector.cs` — Centralized version-aware validation (tables, columns, constraints, `integrity_check`, `foreign_key_check`) for schema v1 and v2.
- `SchemaVersionOneFixture.cs` — Test fixture producing exact schema-v1 databases with a valid workspace identity.

### Modified files

- `SchemaVersion.cs` — `Current = 2`.
- `MigrationRunner.cs` — Delegates post-migration structure validation to `WorkspaceSchemaInspector`.
- `WorkspaceFileValidator.cs` — Version-aware migration metadata checks; structure validation via inspector; exposes `ReadSchemaVersionAsync` for open-path migration gating.
- `SqliteWorkspaceStore.cs` — New workspaces initialize schema v2 atomically; `OpenAsync` injects `MigrationRunner` to migrate v1→v2 with backup before revalidation.
- Test files updated with v2 creation assertions, v1 migration tests, and v2-compatible invalid-structure cases.

## Test results

```
dotnet test ... --filter "FullyQualifiedName~Storage" -v minimal
Passed!  Failed: 0, Passed: 35, Skipped: 0, Total: 35
git diff --check  (clean on committed files)
```

Key new/updated scenarios verified:

- `MigrateV1ToV2_AddsNormalizedCatalogTablesAndRetainsWorkspace`
- `FailedV1ToV2Migration_RollsBackAndRetainsV1Backup`
- `CreateAndOpen_RoundTripsWorkspaceAndCreatesSchemaV2Tables`
- `Open_MigratesSchemaV1AndCreatesPreMigrationBackup`
- Existing backup, cancellation, corruption, lock, and atomic-placement tests retained.

## Self-review

### Correctness

- Schema v2 SQL matches the brief verbatim (four catalog tables with specified constraints).
- `SchemaV2.InitializeAsync` inserts metadata at version 2 and records migrations 1 and 2.
- `MigrateV1ToV2` only creates catalog tables; `MigrationRunner` records version 2 and validates before commit.
- Failed migrations roll back; pre-migration backup retained with v1 content.
- Newer schema (v3+) rejected without mutation.
- `MigrationRunner` is constructor-injected into `SqliteWorkspaceStore`; no silent default with guessed backup paths.

### Scope adherence

- Did **not** implement `SqliteWorkspaceCatalogStore` (Task 3) or composition wiring (Task 5).
- Did **not** commit `.superpowers/sdd/` or `.orchestrator-work-packet.json`.
- SQL/Sqlite/filesystem remain in Infrastructure only.

### Minor notes (non-blocking)

- `Open_RejectsForeignKeyViolations` now expects `workspace.file.invalid` instead of `workspace.file.corrupt` because v2 structure-constraint validation runs before FK integrity checks when workspace table DDL is altered.
- `ApplicationComposition` still constructs `SqliteWorkspaceStore()` without a `MigrationRunner`; v1 open-for-edit migration will be wired in Task 5. Tests inject the runner explicitly.

## Concerns

None blocking. Task 5 must wire `MigrationRunner` (with `MigrateV1ToV2` and managed backup paths) into production composition so v1 workspaces migrate on open in the Desktop app.

---

## Review fix: pre-migration identity validation

**Status:** DONE

**Finding:** `OpenAsync` migrated v1→v2 before full identity/metadata validation, so invalid `application_id` or workspace identity could trigger migration (with backup) before rejection.

**Fix:**
- On the v1 open path, call `validator.OpenAsync` before `MigrateAsync` so identity, metadata, and v1 structure validation match post-migration behavior.
- Reject invalid files without migration or backup; valid v1 files still migrate transactionally and revalidate at v2.
- Changed `SchemaV2.InitializeAsync` from `public static` to `internal static` per brief.

**Commit:** `ab54e6e` — fix: validate workspace identity before v1 migration

**Tests:**

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Infrastructure.Tests/Tww3Companion.Infrastructure.Tests.csproj --filter "FullyQualifiedName~Storage" -v minimal
```

```
Passed!  - Failed: 0, Passed: 35, Skipped: 0, Total: 35
```
