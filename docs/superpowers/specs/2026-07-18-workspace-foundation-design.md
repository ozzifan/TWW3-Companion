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
- `Microsoft.Data.Sqlite.Core` 10.0.10 using direct parameterised SQL;
- `SQLitePCLRaw.bundle_winsqlite3` 2.1.11, initialized once through `SQLitePCL.Batteries_V2.Init()` before any connection is opened, so Windows 10 or later services the native `winsqlite3.dll` library without bundling the vulnerable `e_sqlite3` native package;
- `Microsoft.Extensions.Logging.Abstractions` 10.0.10 as the application-facing logging contract;
- `Microsoft.Extensions.Logging` 10.0.10 for Desktop composition;
- Serilog 4.4.0, `Serilog.Extensions.Logging` 10.0.0, and `Serilog.Sinks.File` 7.0.0 confined to Infrastructure and Desktop composition;
- xUnit v3 package 3.2.2, `xunit.runner.visualstudio` 3.1.5, and `Microsoft.NET.Test.Sdk` 18.8.1 for automated tests.

The application does not introduce Entity Framework Core or Dapper. Entity Framework adds change tracking and migration machinery that this explicit repository boundary does not require. Dapper reduces row-mapping boilerplate but does not provide compile-time SQL or column-rename safety, so it is deferred until repeated mapping code demonstrates a concrete need. Direct SQL remains isolated inside the Infrastructure project. NuGet package versions are centrally pinned and committed so restore is reproducible.

Application and Desktop code depend only on Microsoft logging abstractions. Serilog configuration and sinks do not cross into Domain or Application. The file sink rolls daily or at 10 MiB, whichever comes first, and retains seven files. Installed logs live under `%LOCALAPPDATA%\TWW3 Companion\Logs\`; portable logs live under `Data\Logs\`.

The installed and portable products publish self-contained for `win-x64`. Users do not separately install .NET, Avalonia, or SQLite; the supported Windows baseline supplies and services the native SQLite library.

## Solution Boundaries

The solution contains four production projects:

### `Tww3Companion.Domain`

Owns stable domain identities, Workspace metadata, validation primitives, and invariants. It has no dependency on Avalonia, SQLite, filesystem paths, or process APIs.

### `Tww3Companion.Application`

Owns use cases and ports. It defines intention-revealing interfaces for Workspace creation and opening, recent-Workspace settings, application mode and paths, and time/UUID generation where deterministic testing requires them.

Application results distinguish success, validation failure, inaccessible path, existing target, invalid Workspace, corrupt Workspace, unsupported schema, locked file, migration failure, and cancellation. They state whether any persistent change committed.

### `Tww3Companion.Infrastructure`

Implements SQLite storage, schema creation and migration, managed backups, application settings, installed/portable paths, filesystem operations, and the Windows single-instance lock.

Only this project references `Microsoft.Data.Sqlite.Core` and the Windows SQLite provider bundle. SQL and SQLite-specific types do not cross its public boundary.

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

UUIDs are stored in the database and exported later using canonical lowercase hyphenated text. The modest storage and index cost of textual UUIDs is accepted in exchange for inspectability and direct JSON compatibility at the expected Workspace scale. Version 0.1 generates UUID version 4 values. Existing identifiers are never regenerated during open or migration.

Display names must contain at least one non-whitespace character. The initial maximum is 200 Unicode scalar values. The database stores the normalised display name and enforces non-empty trimmed text. Filenames are chosen independently and do not become the display name implicitly.

## Schema Version 1

Schema version 1 contains only records exercised by the Workspace lifecycle slice:

- application identity and schema metadata;
- migration history;
- Workspace metadata;

Later vertical slices add their tables and constraints through production migrations alongside the application behaviour that exercises them. The import slice owns Mods, aliases, Source References, Collections, and Collection Memberships. Relationship, Evidence, Game Compatibility Observation, and Profile tables arrive only after their owning feature designs are accepted. This keeps the schema normalised around RFC-0002 without hardening unexercised implementation semantics.

Schema version 1 enforces one application-identity row, one valid Workspace row, unique UUID identity, non-empty normalised display name, and valid migration ordering. Application validation provides user-facing messages before database constraints act as the final integrity boundary.

Foreign keys are enabled by the single Infrastructure connection factory before any command executes, including migration commands. Later slices use the same factory. Architecture and integration tests prove that every repository and migration connection comes from this factory and that `PRAGMA foreign_keys` reports enabled.

Derived indexes or caches are excluded from schema version 1 unless a measured query used by this slice requires one. Required identity indexes are authoritative constraints, not derived data.

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

Using the destination directory for the temporary file keeps the final move on one volume. The temporary filename is `<final-name>.<operation-uuid>.tmp`; it does not use a Unix-style dot prefix because that prefix has no consistent hidden-file meaning on Windows. If the final path appears before the move, creation fails without replacing it. A failure removes only the temporary file created by the operation. The application never deletes or modifies a pre-existing destination.

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

The future restore design owns any lineage metadata linking a pre-restore backup to its restore operation. This slice reserves the `pre-restore` reason only for the shared retention contract and does not invent unused restore records.

## Application Mode and Settings

Installed mode uses `%LOCALAPPDATA%\TWW3 Companion\`: `settings.json`, `Backups\`, `Logs\`, and the default `Workspaces\` destination live beneath it. Portable mode is selected by a fixed distribution marker named `portable.flag` beside the executable and uses `Data\settings.json`, `Data\Backups\`, `Data\Logs\`, and the default `Data\Workspaces\` destination beneath the portable folder.

On first run, the application creates the selected mode's missing managed directories before loading settings. It verifies that settings, backup, log, and default Workspace directories are writable using operation-owned temporary probes that are removed immediately. If any required managed directory cannot be created or written, startup displays a blocking error naming the mode and directory, suggests moving the portable folder or correcting permissions, and exits before opening user data. Read-only portable media is not a supported execution location.

Portable mode does not write to installed-mode managed directories. Installed and portable settings do not share recent files, theme, placement, or backup retention state.

Settings are application data, not Workspace data. They contain:

- schema version for the settings document;
- theme choice;
- window placement when valid;
- ordered recent-Workspace paths with last-successful-open time.

Settings writes use a sibling temporary file and atomic replacement. A missing settings file produces defaults. An invalid settings file produces defaults and a diagnostic without changing the invalid file. On the first later settings write, the application atomically renames the invalid file to `settings.invalid.<utc-timestamp>.json` before writing a new valid `settings.json`.

If preservation or replacement fails, the application keeps the changed settings in memory for the current session, shows a persistent banner stating that settings will not survive restart, and offers Retry and Open Settings Folder actions. It never overwrites the invalid original or reports that settings were saved. Workspace access remains available when the managed directory itself passed startup validation.

## Single-Instance Startup

Before Avalonia loads settings or Workspace data, Desktop requests an Infrastructure single-instance lease backed by `Local\TWW3Companion.SingleInstance.<current-user-sid>`, a named Windows mutex shared across installed and portable copies for that Windows user.

If acquisition fails because another process owns the lease, the new process displays a native blocking message identifying that TWW3 Companion is already running and exits. Windows releases the mutex automatically after normal exit or process failure. A persistent lock file is not authoritative.

This intentionally prevents simultaneous installed and portable instances even when they would open different Workspaces. The limitation follows RFC-0003's one-process-per-Windows-user rule and favors a simple machine-wide safety boundary over relying on SQLite file locks as the user-facing concurrency policy.

## Home and Workspace Shell

Home is the only complete screen in this slice. It exposes:

- Create Workspace;
- Open Workspace;
- recent Workspaces with missing/unavailable state and a remove action;
- System, Light, and Dark theme choices.

RFC-0005 requires **Import into a new Workspace** on the completed v0.1 Home screen. That action is intentionally absent from this foundation slice because RFC-0004's importer and its domain tables arrive in the next vertical slice; this is sequencing, not a change to accepted v0.1 scope.

Home and the Create/Open dialogs warn that opening the same live Workspace from multiple machines through OneDrive, Dropbox, or similar synchronisation is unsupported and can create conflicts or corruption. Until lossless JSON transfer ships, the current safe guidance is to close TWW3 Companion on every machine before manually copying a `.tww3c` file, then open only the completed local copy on one machine at a time.

Create and Open operations expose busy state, prevent duplicate submission, and remain cancellable before their atomic or migration commit sections. Errors are persistent and actionable and state whether a file changed. Success is announced visibly and through accessibility semantics without stealing focus.

After successful creation or opening, the application enters the accepted three-region shell. This slice displays only:

- the Mod Library destination;
- an empty Collections area;
- the empty master list and contextual empty detail state;
- Return Home.

The empty state says that the Workspace contains no Mods or Collections yet and that no data has been added. It does not advertise unavailable features or present disabled calls to action.

Import, search, Profiles, health, relationship editing, and later destinations do not appear as disabled placeholders.

Returning Home closes the active Workspace after all SQLite handles and Workspace-scoped services are disposed. Closing the application persists valid application settings and releases the single-instance lease.

## Theme, Window, and Accessibility Baseline

System is the default theme. Light and Dark are application settings. Windows High Contrast overrides the stored choice while active and restores it afterward.

The first window opens at 1280 × 800 logical pixels and enforces a 1024 × 640 logical minimum. Invalid or off-screen saved placement is ignored. If the available work area cannot contain the logical minimum, the compatibility screen offers Exit or Continue Anyway before Home; continuing maximizes the window and retains a warning for that session. A minimum-size shell prototype with representative Windows text scaling is the first Desktop implementation task; further screens do not proceed until its three regions, primary actions, focus indicators, and text remain usable at the accepted bound.

Home and the empty shell are fully keyboard operable. Focus order, accessible names and states, visible focus, status announcements, High Contrast, representative text scaling, and Windows Narrator behavior are verified before the slice is complete.

## Error and Cancellation Contract

Every lifecycle result identifies:

- the operation;
- the affected path when safe to display;
- the failure category;
- whether any persistent change committed;
- a safe next action.

Expected validation and environment failures are typed results rather than exceptions crossing into ViewModels. Unexpected exceptions are caught at the application boundary, logged through `Microsoft.Extensions.Logging`, and converted to a generic failure result. Logs include event ID, severity, UTC timestamp, operation name, failure category, exception type, and stack trace where available. They exclude Workspace content, imported text, display names, Source References, filenames, and full user-selected paths by default; a per-session opaque identifier provides correlation when necessary.

Cancellation before commit removes operation-owned temporary artifacts and reports no change. When schema creation, migration, or atomic replacement enters its non-cancellable section, the Cancel action disables and the operation status changes to **Finalizing — please wait**. Closing during this state explains why immediate exit is unavailable. The UI never reports completion until commit or rollback is known.

## Verification Strategy

### Unit tests

- UUID and display-name rules;
- lifecycle result mapping;
- Home and shell ViewModel states and commands;
- recent-list behavior;
- installed/portable path selection;
- first-run managed-directory creation and read-only-location failure;
- theme and High Contrast precedence;
- multi-machine synchronisation warning copy and visibility.

### Infrastructure integration tests

- schema version 1 application identity, migration history, and Workspace metadata constraints;
- create/open round trip using a real `.tww3c` file;
- refusal to overwrite;
- invalid, unrelated, corrupt, locked, inaccessible, and newer-schema files;
- connection-factory enforcement of `PRAGMA foreign_keys = ON` for repositories and migrations;
- schema and integrity validation;
- migration commit and rollback;
- SQLite-safe backup creation;
- five-backup retention and protection of unrelated files;
- atomic settings writes and invalid-settings recovery;
- logging retention, redaction, and installed/portable path isolation.

### Process and UI tests

- second-process launch aborts before settings or Workspace access;
- installed and portable copies contend for the same user-scoped lease;
- keyboard-only Home and empty-shell workflows;
- focus, announcements, System/Light/Dark, High Contrast, and text scaling;
- supported and undersized display behaviour;
- minimum-size shell usability at representative Windows text-scaling settings before later Desktop tasks proceed;
- **Finalizing — please wait** state and disabled cancellation during non-cancellable work.

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
- [Microsoft.Extensions.Logging.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions/10.0.10)
- [Serilog.Extensions.Logging](https://www.nuget.org/packages/Serilog.Extensions.Logging/10.0.0)
- [Serilog.Sinks.File](https://www.nuget.org/packages/Serilog.Sinks.File/7.0.0)
