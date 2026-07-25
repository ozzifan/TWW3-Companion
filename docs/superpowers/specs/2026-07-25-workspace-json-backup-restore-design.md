# Workspace JSON Backup and Restore Design

**Status:** Approved for implementation planning
**Date:** 2026-07-25
**Scope:** Lossless full-Workspace JSON backup and restore for v0.1

## Goal

Give users a documented, lossless, and safe way to back up or transfer a complete Workspace without treating the live SQLite database as a portable interchange format.

A user can:

- back up the open Workspace to a user-selected JSON file;
- restore a JSON backup into a new Workspace from Home;
- replace the open Workspace from a JSON backup after reviewing a summary and explicitly confirming replacement;
- recover the prior SQLite Workspace from an automatic pre-restore backup if replacement begins.

This slice completes the backup and restore workflow accepted by RFC-0003 and RFC-0005. It does not add merge restore, single-Collection export, direct JSON editing, or cross-machine synchronisation.

Implementation tasks for this feature must be routed through AI Dev Orchestrator every time. The orchestrator uses the rigid `IMP` implementation role followed by the independent `REV` review role defined in [AGENTS.md](../../../AGENTS.md).

## Current State

The repository already contains:

- a schema-v2 SQLite Workspace with persisted Workspace, Mod, Source Reference, Collection, and ordered Membership data;
- validated Workspace creation and opening;
- SQLite-safe pre-migration backups;
- automatic managed-backup cleanup;
- atomic file-writing and Workspace-placement infrastructure;
- Home and open-Workspace navigation;
- typed application-operation results.

The application does not yet define a JSON Schema, export authoritative Workspace data, reconstruct a Workspace from JSON, or expose Backup and Restore actions.

The approved Workspace foundation design retains five managed automatic backups total per Workspace UUID. The current cleanup implementation instead groups by backup reason and can retain five `pre-migration` plus five `pre-restore` backups. This slice corrects cleanup to enforce the approved combined limit while leaving user-selected JSON exports and unrelated files untouched.

## Design Principles

1. SQLite remains the canonical live Workspace store.
2. JSON is a versioned, documented, lossless portable representation.
3. Export never mutates the Workspace.
4. Restore validates the complete input before mutating a destination.
5. Restore preserves Workspace and record UUIDs.
6. Restore replaces or creates; it never merges.
7. Replacement requires a summary, an automatic backup, and explicit confirmation.
8. Temporary and partially constructed files never become visible as successful Workspaces.
9. User-selected exports are user-owned files and are never subject to automatic cleanup.
10. Backup and restore remain responsive, keyboard-completable, and accessible.

## Scope

### Included

- a versioned JSON Schema for the complete authoritative v0.1 Workspace;
- deterministic full-Workspace JSON export;
- strict JSON parsing, version checks, and record validation;
- restoration into a new user-selected `.tww3c` destination;
- replacement of the open Workspace after explicit confirmation;
- SQLite-safe pre-restore backup;
- atomic destination placement and reload;
- Home and open-Workspace Backup/Restore entry points;
- correction of managed automatic-backup retention to five total per Workspace;
- automated and interactive verification.

### Excluded

- single-Collection export or sharing;
- Markdown summaries or Workshop-ID list export;
- merge restore;
- regenerating identities to create an independent clone;
- in-place JSON editing;
- JSON as the live Workspace store;
- cloud synchronisation;
- automatic scheduling or rotation of user-selected JSON exports;
- restoring application settings, recent files, window state, logs, or backup history.

## Architecture

The workflow remains layered:

```text
Desktop
→ Application backup/restore services
→ Infrastructure JSON and SQLite boundaries
→ user-selected JSON or canonical Workspace file
```

Views and ViewModels own navigation, file selection, summaries, progress, cancellation, confirmation, focus, and accessible announcements.

Application services expose intention-revealing backup and restore operations. They coordinate validation and return typed success, validation, cancellation, recoverable-failure, or blocking-failure results. They do not execute SQL or parse presentation state.

Infrastructure owns:

- reading a consistent authoritative Workspace snapshot;
- deterministic JSON serialization;
- strict JSON deserialization and export-version dispatch;
- construction of a temporary SQLite Workspace;
- database and domain-invariant validation;
- SQLite-safe automatic backup;
- atomic file placement or replacement;
- managed automatic-backup cleanup.

The Domain layer remains independent of JSON, SQLite, file dialogs, and UI concerns.

## Lossless JSON Format

The first format is identified as `workspace-export-v1`. Its JSON Schema lives under `schemas/` and is committed before the format ships.

Every export contains:

- the format identifier and format version;
- the Workspace UUID and metadata;
- every persisted Mod and stable UUID;
- Source References and stored imported metadata;
- every Collection and stable UUID;
- every ordered Collection Membership and its Collection-owned fields.

The schema represents all authoritative information that v0.1 can persist. Future schema versions add later authoritative entities, such as Relationships or Evidence, when those entities become part of the implemented canonical store.

Exports exclude:

- database schema or migration implementation details;
- local Workspace and export paths;
- application settings and recent files;
- window placement, theme, and other presentation preferences;
- logs and diagnostics;
- managed-backup history;
- rebuildable indexes, caches, counts, and summaries.

The format uses deterministic property names and record ordering where practical. Repeated exports of unchanged authoritative data must produce semantically identical content and should produce byte-stable output apart from fields explicitly defined as operation metadata. The v1 format must not add a changing export timestamp if doing so would prevent byte-stable backups without improving restore correctness.

The JSON Schema is part of the public compatibility contract. Unknown properties are rejected for v1 unless the schema explicitly identifies an extension location. Unsupported newer format versions are rejected without attempting a best-effort downgrade.

## Export Workflow

Backup is available only while a Workspace is open.

```text
Choose Backup
→ choose user-owned .json destination
→ read one consistent authoritative snapshot
→ validate snapshot
→ serialize deterministically to an operation-owned temporary file
→ flush and atomically place the JSON destination
→ report success
```

The Save dialog suggests a filesystem-safe name derived from the Workspace name and current UTC date. The user may choose a different name or location. Existing destinations use the standard Windows overwrite confirmation.

Export does not close, modify, migrate, or reload the Workspace. A failed or cancelled export leaves the Workspace and any pre-existing destination unchanged. User-selected JSON backups are not registered for automatic retention and are never deleted by TWW3 Companion cleanup.

## Restore Inspection

Both restore entry points begin with a JSON file picker and a non-destructive inspection stage:

```text
Choose JSON
→ parse
→ verify format and version
→ validate all records, identities, references, and ordering
→ show restore summary
```

The summary contains:

- Workspace name;
- export format version;
- Mod count;
- Collection count;
- Membership count;
- destination mode: new Workspace or replacement;
- a clear statement that restore replaces or creates and never merges.

The selected JSON is revalidated immediately before commit so a changed file cannot bypass the inspected contract.

## Restore as a New Workspace

Restore from Home creates a new Workspace file:

```text
Validated restore summary
→ choose new .tww3c destination
→ build temporary SQLite Workspace
→ validate database schema and domain invariants
→ atomically place destination
→ open restored Workspace
```

The restored Workspace preserves its exported Workspace UUID, name, record UUIDs, references, and Membership order. This is restoration or transfer, not creation of an independent duplicate with regenerated identities.

The destination must not be an already open Workspace. If the chosen path exists, the operation must use the replacement safety flow or require a different path; it must not overwrite through the new-Workspace path.

Invalid input, cancellation, construction failure, or placement failure does not create a partial destination. Operation-owned temporary files are removed after failure where safe.

## Replace the Open Workspace

Restore from an open Workspace targets that Workspace's existing path. Before confirmation, the UI names and summarises both:

- the currently open destination being replaced;
- the validated source Workspace from the JSON export.

The blocking confirmation states that restore replaces the entire Workspace and does not merge current data.

After explicit confirmation:

```text
Revalidate JSON
→ create SQLite-safe pre-restore backup
→ build restored database at operation-owned temporary path
→ validate database and domain invariants
→ close active destination handles
→ atomically replace destination
→ reopen and reload restored Workspace
```

Cancellation is available before the non-cancellable commit section begins. Once final replacement begins, the UI reports progress without offering a cancellation control that cannot be honoured safely.

Any failure before final placement leaves the open Workspace unchanged. If final placement cannot complete, the operation restores the original destination where necessary or reports a blocking failure without claiming success. The pre-restore backup is retained whenever replacement has begun far enough that recovery may be required.

Successful replacement refreshes recent-Workspace metadata as needed, reloads the Workspace library, and returns to the normal Workspace shell.

## Validation

Restore rejects the complete export before destination mutation when any of the following is true:

- malformed JSON or invalid text encoding;
- absent or unsupported format identifier or version;
- schema violation or unknown v1 property outside an approved extension point;
- invalid or duplicate UUID;
- missing required Workspace metadata;
- duplicate Source Reference identity;
- reference to a missing Mod or Collection;
- duplicate Membership identity;
- invalid or duplicate Membership position within a Collection;
- invalid domain value;
- any invariant required by the current SQLite schema.

Validation reports bounded, actionable errors. It must not echo user-authored notes, imported descriptions, or full local paths into logs. The UI may name the selected file where necessary to explain the failure.

## Managed Automatic Backups

Managed automatic backups continue to live beneath the mode-specific managed backup directory and use attributable names for `pre-migration` and `pre-restore` reasons.

Cleanup retains the five newest managed automatic backups total for one Workspace UUID, regardless of reason. Cleanup:

- runs only after both the new backup and the operation requiring it succeed;
- orders attributable backups by their canonical UTC timestamp;
- removes only the oldest attributable excess files;
- never removes files whose names cannot be safely attributed;
- never removes files outside the Workspace's managed backup directory;
- never removes user-selected JSON exports.

Pre-restore backups use SQLite's backup API rather than direct copying of a live database.

## User Experience

### Entry points

- **Open Workspace:** Backup and Restore appear in the Workspace settings/actions area.
- **Home:** Restore appears alongside Create Workspace, Open Workspace, and Import into a new Workspace.

Only actions available in the current context are shown.

### Feedback

Validation, snapshot reads, serialization, database construction, backup, and restore do not block the UI thread. Progress is determinate only where totals are meaningful; otherwise it is honest and indeterminate.

Recoverable failures use a persistent page message that identifies:

- the operation;
- the affected file or Workspace where appropriate;
- whether anything changed;
- a safe next action.

Successful backup and restore produce brief visible and assistive-technology announcements without stealing focus. Backup leaves the user's current Workspace selection intact. Restore opens or reloads the restored Workspace.

Dialogs are limited to file selection, overwrite confirmation supplied by the platform, and the blocking open-Workspace replacement decision.

### Accessibility

Both workflows are keyboard-completable using standard navigation and confirmation behaviour. Controls expose accessible names, state, validation, and progress. Focus moves predictably into a blocking confirmation and returns to the triggering control after cancellation. Completion and failure announcements do not rely on colour alone.

The workflows remain usable at the v0.1 minimum supported window size and scaling contract.

## Error and Recovery Contract

Every failure result states whether a persistent change committed.

- Export failure never changes the Workspace.
- Inspection or validation failure never changes a destination.
- New-Workspace restore failure never exposes a partial Workspace as successful.
- Replacement failure before placement leaves the existing Workspace authoritative.
- Replacement failure during placement restores the existing destination where necessary or provides a blocking recovery message pointing to the retained managed backup.
- Failure to create the required pre-restore backup blocks replacement.
- Failure to clean old automatic backups after a successful restore does not invalidate the restored Workspace, but it is reported and logged without exposing Workspace content.

The application never silently substitutes an empty Workspace, partially restores records, regenerates stable identities, or claims a merge occurred.

## Verification

### Format and service tests

Tests prove:

- deterministic export of a populated schema-v2 Workspace;
- exclusion of paths, settings, logs, caches, and backup history;
- conformance to `workspace-export-v1`;
- export, restore, then export preserves authoritative values and stable IDs;
- malformed input and every specified invariant violation are rejected;
- unsupported newer versions are rejected;
- cancellation and write failure preserve pre-existing files;
- new-Workspace restore never leaves a partial destination;
- replacement creates a usable pre-restore SQLite backup;
- reconstruction and placement failures preserve the original Workspace;
- successful restore produces a normally openable Workspace.

### Backup-retention tests

Tests use interleaved `pre-migration` and `pre-restore` files to prove:

- only the newest five managed backups total remain;
- reason does not create a separate allowance;
- unrelated and unattributable files remain;
- user-selected JSON exports remain;
- cleanup occurs only after the associated operation succeeds.

### Desktop tests

ViewModel and shell tests prove:

- Backup is available only for an open Workspace;
- Restore is available from Home and the open Workspace;
- inspection displays the required summary and replacement meaning;
- Home restore requires a new destination;
- open-Workspace restore requires explicit replacement confirmation;
- commands, cancellation, progress, failure retention, and success reload behave correctly;
- accessible state and announcements are exposed without premature success.

### Repository and interactive verification

The implementation plan requires:

- formatting verification;
- Release build;
- all automated tests;
- self-contained portable publish;
- portable smoke test;
- interactive Backup and both Restore entry points;
- keyboard-only completion;
- Windows Narrator announcements and focus return;
- overwrite and replacement confirmations;
- representative failure messages;
- minimum supported window size and Windows scaling checks.

## Documentation Alignment

The implementation updates:

- [schemas/README.md](../../../schemas/README.md) to identify the shipped v1 schema;
- [docs/architecture/import-export.md](../../architecture/import-export.md) with the exact implemented backup/restore boundary;
- [ROADMAP.md](../../../ROADMAP.md) to mark JSON backup/restore complete without completing v0.1;
- [CHANGELOG.md](../../../CHANGELOG.md) with the new workflow;
- [docs/project-history.md](../../project-history.md) with the data-portability milestone when the slice ships;
- developer documentation if verification or fixture commands change.

Packaging and release verification remain the final v0.1 slice.

## Success Criteria

The slice is complete when:

1. a user can create a deterministic lossless JSON backup of the open Workspace;
2. the application can restore that export into a new Workspace while preserving all authoritative v0.1 data and stable IDs;
3. the application can safely replace the open Workspace only after summary, backup, and explicit confirmation;
4. invalid, cancelled, or failed operations do not partially create or overwrite Workspace data;
5. managed automatic retention is five backups total per Workspace and never touches user-owned exports or unrelated files;
6. automated, packaging, smoke, keyboard, Narrator, focus, and supported-size checks pass or any skipped interactive check is reported explicitly;
7. architecture, schema, roadmap, changelog, and project history agree with the shipped behaviour.
