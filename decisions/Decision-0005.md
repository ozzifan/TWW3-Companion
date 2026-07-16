# Decision-0005: Embedded SQLite Storage

**Status:** Accepted
**Date:** 2026-07-16

## Decision

Each TWW3 Companion Workspace uses one local SQLite database as its canonical working store. SQLite and its runtime dependencies are embedded in both the Windows installer and portable distribution; users do not install or administer a database service.

Stable domain identities use UUIDs. Confirmed operations and imports are applied atomically. Schema migrations are versioned and backed up, and a versioned, lossless JSON format provides inspection, archival, transfer, and restore.

Version 0.1 targets Windows through a one-step installer and a portable ZIP. Only one TWW3 Companion process may run for the current Windows user. The complete storage, recovery, distribution, and data-retention contract is defined in [RFC-0003](../RFC/RFC-0003.md).

## Rationale

- SQLite reinforces domain invariants and atomic imports without an external service.
- A self-contained application supports offline-first use and lowers installation friction.
- Lossless JSON preserves user ownership without making direct JSON editing the primary workflow.
- UUIDs preserve source-neutral identity across databases, exports, restores, and migrations.
- Installer and portable distributions serve both conventional installation and cautious evaluation.

## Consequences

- SQLite is canonical; JSON is the portable representation.
- Storage code remains behind a boundary that does not expose SQL to the domain or UI.
- Windows is the initial platform target; other desktop platforms are deferred.
- Live Workspace databases must not be edited concurrently through cross-machine synchronisation.
- Single-Collection snapshot rules remain deferred to the import/export design.
- Backup retention must be defined in the storage implementation plan before application code lands.
