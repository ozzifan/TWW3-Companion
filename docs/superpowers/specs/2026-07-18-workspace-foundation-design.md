# Workspace Foundation Design

**Status:** Approved for implementation planning
**Date:** 2026-07-18
**Scope:** First vertical slice of v0.1

## Goal

Deliver the smallest end-to-end TWW3 Companion application that proves the accepted domain, storage, and UI boundaries: the application starts safely, shows Home, creates and opens an empty Workspace, persists it as SQLite, and enters the milestone-bounded Workspace shell.

This slice does not implement import, Mod or Membership editing, JSON export or restore, installer authoring, or later roadmap features.

## Approved Dependency Baseline

- .NET SDK 10.0.302, pinned by `global.json` with patch roll-forward enabled within the 10.0 feature band;
- target framework `net10.0`;
- C# 14 with nullable reference types enabled;
- Avalonia 12.1.0, with all Avalonia packages pinned to the same version;
- `Microsoft.Data.Sqlite` 10.0.10 using direct parameterised SQL;
- xUnit v3 package 3.2.2 and `Microsoft.NET.Test.Sdk` 18.8.1 for automated tests.

The application does not introduce Entity Framework Core or Dapper. Direct SQL remains isolated inside the Infrastructure project. NuGet package versions are centrally pinned and committed so restore is reproducible.

The installed and portable products publish self-contained for `win-x64`. Users do not separately install .NET, Avalonia, or SQLite.

## Solution Boundaries

The solution contains four production projects:

### `Tww3Companion.Domain`

Owns stable domain identities, Workspace metadata, validation primitives, and invariants. It has no dependency on Avalonia, SQLite, filesystem paths, or process APIs.

### `Tww3Companion.Application`

Owns use cases and ports. It defines intention-revealing interfaces for Workspace creation and opening, recent-Workspace settings, application mode and paths, and time/UUID generation where deterministic testing requires them.

Application results distinguish success, validation failure, inaccessible path, existing target, invalid Workspace, corrupt Workspace, unsupported schema, locked file, migration failure, and cancellation. They state whether any persistent change committed.

### `Tww3Companion.Infrastructure`

Implements SQLite storage, schema creation and migration, managed backups, application settings, installed/portable paths, filesystem operations, and the Windows single-instance lock.

Only this project references `Microsoft.Data.Sqlite`. SQL and SQLite-specific types do not cross its public boundary.

### `Tww3Companion.Desktop`

Owns Avalonia Views, ViewModels, composition, startup, dialogs, accessibility properties, theme selection, and Windows display behavior.

Views bind to ViewModels. ViewModels call Application use cases and never access SQL, repositories, or raw filesystem APIs.

Each production project has a matching test project. Infrastructure integration tests use real SQLite files in isolated temporary directories. Desktop tests exercise ViewModels without opening native windows except for explicitly marked UI smoke tests.

## Dependency Direction

Dependencies point inward:

```text
Tww3Companion.Desktop ───────→ Tww3Companion.Application
          │                              │
          │                              ↓
          └──────────────────→ Tww3Companion.Domain

Tww3Companion.Infrastructure → Tww3Companion.Application
          │                              │
          └──────────────────→ Tww3Companion.Domain
```

Application defines the ports; Infrastructure implements them; Desktop composes the implementations. Domain references no other production project. Automated architecture tests reject forbidden project references and SQLite package references outside Infrastructure.

## Workspace Identity and File Contract

A Workspace has:

- a stable UUID generated at creation;
- a required user-entered display name after trimming outer whitespace;
- created and last-modified UTC timestamps;
- a schema version recorded in migration metadata;
- the application identifier `com.ozzifan.tww3-companion.workspace`, used to reject unrelated SQLite files.

The user-facing Workspace extension is `.tww3c`. The contents remain an ordinary SQLite database. The extension is not treated as proof that a file is valid; opening always validates its header and required metadata.

UUIDs are stored in the database and exported later using canonical lowercase hyphenated text. Version 0.1 generates UUID version 4 values. Existing identifiers are never regenerated during open or migration.

Display names must contain at least one non-whitespace character. The initial maximum is 200 Unicode scalar values. The database stores the normalised display name and enforces non-empty trimmed text. Filenames are chosen independently and do not become the display name implicitly.

## Schema Version 1

Schema version 1 establishes the full currently accepted relational foundation rather than an opaque Workspace blob. It includes authoritative tables for:

- Workspace metadata;
- Mods and aliases;
- Source References;
- Collections;
- Collection Memberships;
- Relationships and unresolved targets;
- Evidence and provenance links;
- Game Compatibility Observations;
- migration history.

Profiles are not included because their fields and behavior remain deferred to v0.3.

Foreign keys are enabled on every connection. Database constraints enforce unique UUIDs, one Workspace row, unique Source Reference type plus external identifier within a Workspace, unique Mod membership within a Collection, valid ownership, and the accepted deletion behavior. Application validation provides user-facing messages before database constraints act as the final integrity boundary.

Derived indexes or caches are excluded from schema version 1 unless a measured query used by this slice requires one. Required uniqueness indexes are authoritative constraints, not derived data.

## Create Workspace Flow

Home requests a display name and destination through an owned dialog and Windows file picker. The picker defaults to `.tww3c` and does not silently overwrite an existing file.

Creation follows this sequence:

```text
validate display name and destination
→ reserve a temporary file in the destination directory
→ create schema version 1 in one transaction
→ write Workspace metadata
→ run integrity and application-identity validation
→ close SQLite handles
→ atomically move the temporary file to the final .tww3c path
→ reopen through the normal Open Workspace flow
→ add the successful path to recent Workspaces
```

Using the destination directory for the temporary file keeps the final move on one volume. The temporary filename is `.<final-name>.<operation-uuid>.tmp`. If the final path appears before the move, creation fails without replacing it. A failure removes only the temporary file created by the operation. The application never deletes or modifies a pre-existing destination.

## Open Workspace Flow

Open accepts a selected `.tww3c` path or recent-Workspace entry and performs:

```text
path and access checks
→ SQLite header/open check
→ application identifier check
→ schema-version check
→ structural and foreign-key validation
→ migration when required and supported
→ load exactly one Workspace identity
→ enter the Workspace shell
→ update recent Workspaces
```

An unrelated SQLite file, corrupt database, missing Workspace row, duplicate Workspace row, invalid UUID, unsupported schema, lock failure, or inaccessible path returns a distinct typed result. Home remains usable and the path is not added to recents after failure.

A database newer than the application supports is rejected without modification. Schema version zero or an absent application identifier is not guessed into validity.

## Migration and Backup Policy

Migrations are ordered, forward-only, bundled with the application, and executed transactionally. Before the first migration of a Workspace during an application run, Infrastructure creates a timestamped SQLite-safe backup in the installed application's managed backup directory or the portable distribution's managed backup directory.

Automatic backup retention is five backups per Workspace UUID, counting pre-migration and pre-restore backups together. Backups live beneath `Backups/<workspace-uuid>/` and use `<utc-timestamp>.<reason>.tww3c`, where reason is `pre-migration` or `pre-restore`. Cleanup occurs only after the new backup and the operation that required it both succeed. Cleanup removes oldest managed automatic backups beyond five. It never removes:

- manual JSON exports;
- user-copied `.tww3c` files;
- Workspace files outside managed backup directories;
- backups whose filename or metadata cannot be attributed safely to the Workspace UUID.

This slice supplies schema version 1 and the migration runner contract. Integration tests use a test-only version-zero fixture to prove backup, migration, rollback, and retention behavior without shipping a fictitious production migration.

## Application Mode and Settings

Installed mode uses `%LOCALAPPDATA%\TWW3 Companion\`: `settings.json`, `Backups\`, and the default `Workspaces\` destination live beneath it. Portable mode is selected by a fixed distribution marker named `portable.flag` beside the executable and uses `Data\settings.json`, `Data\Backups\`, and the default `Data\Workspaces\` destination beneath the portable folder.

Portable mode does not write to installed-mode managed directories. Installed and portable settings do not share recent files, theme, placement, or backup retention state.

Settings are application data, not Workspace data. They contain:

- schema version for the settings document;
- theme choice;
- window placement when valid;
- ordered recent-Workspace paths with last-successful-open time.

Settings writes use a sibling temporary file and atomic replacement. A missing settings file produces defaults. An invalid settings file produces defaults and a diagnostic without changing the invalid file. On the first later settings write, the application atomically renames the invalid file to `settings.invalid.<utc-timestamp>.json` before writing a new valid `settings.json`; failure to preserve it aborts the write. Invalid settings never prevent Workspace access.

## Single-Instance Startup

Before Avalonia loads settings or Workspace data, Desktop requests an Infrastructure single-instance lease backed by `Local\TWW3Companion.SingleInstance.<current-user-sid>`, a named Windows mutex shared across installed and portable copies for that Windows user.

If acquisition fails because another process owns the lease, the new process displays a native blocking message identifying that TWW3 Companion is already running and exits. Windows releases the mutex automatically after normal exit or process failure. A persistent lock file is not authoritative.

## Home and Workspace Shell

Home is the only complete screen in this slice. It exposes:

- Create Workspace;
- Open Workspace;
- recent Workspaces with missing/unavailable state and a remove action;
- System, Light, and Dark theme choices.

Home and the Create/Open dialogs warn that opening the same live Workspace from multiple machines through OneDrive, Dropbox, or similar synchronisation is unsupported and can create conflicts or corruption. The warning points users toward the later lossless JSON transfer workflow without implying that JSON export exists in this slice.

Create and Open operations expose busy state, prevent duplicate submission, and remain cancellable before their atomic or migration commit sections. Errors are persistent and actionable and state whether a file changed. Success is announced visibly and through accessibility semantics without stealing focus.

After successful creation or opening, the application enters the accepted three-region shell. This slice displays only:

- the Mod Library destination;
- an empty Collections area;
- the empty master list and contextual empty detail state;
- Return Home.

Import, search, Profiles, health, relationship editing, and later destinations do not appear as disabled placeholders.

Returning Home closes the active Workspace after all SQLite handles and Workspace-scoped services are disposed. Closing the application persists valid application settings and releases the single-instance lease.

## Theme, Window, and Accessibility Baseline

System is the default theme. Light and Dark are application settings. Windows High Contrast overrides the stored choice while active and restores it afterward.

The first window opens at 1280 × 800 logical pixels and enforces a 1024 × 640 logical minimum. Invalid or off-screen saved placement is ignored. If the available work area cannot contain the logical minimum, the compatibility screen offers Exit or Continue Anyway before Home; continuing maximizes the window and retains a warning for that session.

Home and the empty shell are fully keyboard operable. Focus order, accessible names and states, visible focus, status announcements, High Contrast, representative text scaling, and Windows Narrator behavior are verified before the slice is complete.

## Error and Cancellation Contract

Every lifecycle result identifies:

- the operation;
- the affected path when safe to display;
- the failure category;
- whether any persistent change committed;
- a safe next action.

Expected validation and environment failures are typed results rather than exceptions crossing into ViewModels. Unexpected exceptions are caught at the application boundary, logged without Workspace content by default, and converted to a generic failure result. Logs avoid unnecessary full paths and user-authored data.

Cancellation before commit removes operation-owned temporary artifacts and reports no change. Cancellation is disabled once schema creation, migration, or atomic replacement enters its non-cancellable section. The UI never reports completion until commit or rollback is known.

## Verification Strategy

### Unit tests

- UUID and display-name rules;
- lifecycle result mapping;
- Home and shell ViewModel states and commands;
- recent-list behavior;
- installed/portable path selection;
- theme and High Contrast precedence.
- multi-machine synchronisation warning copy and visibility.

### Infrastructure integration tests

- schema version 1 creation and all accepted constraints;
- create/open round trip using a real `.tww3c` file;
- refusal to overwrite;
- invalid, unrelated, corrupt, locked, inaccessible, and newer-schema files;
- foreign-key and integrity validation;
- migration commit and rollback;
- SQLite-safe backup creation;
- five-backup retention and protection of unrelated files;
- atomic settings writes and invalid-settings recovery.

### Process and UI tests

- second-process launch aborts before settings or Workspace access;
- installed and portable copies contend for the same user-scoped lease;
- keyboard-only Home and empty-shell workflows;
- focus, announcements, System/Light/Dark, High Contrast, and text scaling;
- supported and undersized display behavior.

### Packaging smoke tests

- self-contained `win-x64` publish runs on a clean Windows 10-or-later environment without separately installed .NET or SQLite;
- portable mode writes only to its portable managed directories and user-selected Workspace locations;
- installed mode writes only to its accepted managed directories and user-selected Workspace locations.

## Completion Criteria

The slice is complete when:

1. all unit, integration, architecture, process, and UI tests pass;
2. a self-contained Windows x64 smoke build creates, closes, reopens, and displays the same empty Workspace identity;
3. failure-path tests demonstrate no silent overwrite or partial Workspace creation;
4. migration tests demonstrate safe backup, rollback, and five-backup retention;
5. installed and portable modes remain isolated;
6. no unapproved v0.1 or later feature appears in the UI;
7. implementation documentation records the exact build, test, and publish commands.

## References

- [RFC-0002: Collection Domain Model](../../../RFC/RFC-0002.md)
- [RFC-0003: Storage Architecture](../../../RFC/RFC-0003.md)
- [RFC-0005: Initial UI Architecture](../../../RFC/RFC-0005.md)
- [Decision-0007: Avalonia MVVM User Interface](../../../decisions/Decision-0007.md)
- [.NET 10 downloads](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [Avalonia 12 breaking changes](https://docs.avaloniaui.net/docs/avalonia12-breaking-changes)
- [Microsoft.Data.Sqlite transactions](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions)
