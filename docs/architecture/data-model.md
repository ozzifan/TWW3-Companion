# Data Model

This document summarises the conceptual data model accepted in [RFC-0002](../../RFC/RFC-0002.md). Storage architecture is defined by [RFC-0003](../../RFC/RFC-0003.md); exact tables and JSON Schema fields remain implementation decisions.

---

## Scope

The data model defines domain entities, ownership boundaries, lifecycle rules, and invariants. Storage, import grammar, UI layout, and Profile behaviour remain separate decisions.

---

## Aggregate and Ownership

### Workspace

The **Workspace** is the user-owned aggregate root. It contains one shared Mod Library and one or more Collections, and may declare an optional target game version.

Workspace is a domain boundary, not a decision to store all data in one file or database.

### Mod Library

The library owns reusable knowledge shared across Collections:

- **Mods** — source-neutral records with stable internal identities, display names, aliases, shared notes, and imported metadata
- **Source References** — external identities such as Steam Workshop IDs; each source type and external identifier pair identifies at most one Mod within a Workspace
- **Relationships** — dependencies and compatibility claims, including unresolved targets
- **Game Compatibility Observations** — versioned evidence retained as history

### Collection

A **Collection** is a named, curated set of Mods with collection-level notes. It owns Collection Memberships, not copies of Mods.

### Collection Membership

A membership links one Collection to one library Mod and owns collection-specific category, tags, rationale, notes, tracking state, and optional ordering or load-order notes. A Mod may appear once per Collection and in any number of Collections.

Ordering information is documented knowledge; the application does not enforce load order.

### Profile

A Profile belongs to exactly one Collection and will later describe a playable subset or configuration. Its fields, activation rules, and overrides are deferred to the v0.3 design.

---

## Relationships and Evidence

A Dependency is directional and is classified as **requires**, **recommends**, or **optional integration**. A Compatibility Claim records **compatible**, **incompatible**, **patch required**, or **unknown / needs verification**.

Relationship targets may resolve to a library Mod or remain as an unresolved external reference. Resolution and deletion of the target Mod preserve the incoming relationship and its evidence.

Evidence may record a source, observation date, game version, Mod version, notes, and provenance. A Game Compatibility Observation records **works**, **works with caveats**, **broken**, or **unverified** for a game version or patch. Conflicting observations remain visible; last-known-good and latest-known-broken values are derived summaries.

---

## Information Ownership

- **User-authored information** includes preferred names, notes, categories, tags, rationale, and personal observations. Imports must not silently overwrite it.
- **Imported source metadata** retains its source and observation time where practical. It may enrich blank fields; conflicts require an explicit choice.
- **Derived information** includes warnings, health findings, compatibility summaries, counts, and indexes. It must be reproducible from authoritative records.

---

## Lifecycle Rules

Removing a Mod from a Collection deletes only its Collection Membership.

Deleting a Mod from the library requires an explicit confirmed operation that first lists every affected Collection and states which membership data will be lost. Confirmation removes the Mod, all memberships, and relationships it owns as the source Mod. Incoming relationships targeting the deleted Mod remain and become unresolved.

---

## Invariants

1. A Source Reference identifies at most one Mod within a Workspace.
2. A Collection contains a given Mod at most once.
3. A Profile belongs to exactly one Collection.
4. Collection-specific organisation lives on Collection Membership, not Mod.
5. Unresolved relationship targets are valid stored knowledge.
6. Imports do not silently overwrite user-authored information.
7. Derived values are reproducible from authoritative records.
8. Library deletion requires a complete impact warning and explicit confirmation.
9. Relationship evidence survives target resolution and deletion of the target Mod.
10. Compatibility history is retained rather than replaced by latest-status summaries.

---

## Persistence Requirements

Each Workspace is stored canonically in one embedded SQLite database. Stable domain identities use UUIDs. A versioned, lossless JSON format provides inspection, archival, transfer, and restore.

Storage provides:

- local, user-controlled persistence without a separate database service;
- atomic application of confirmed operations and imports;
- explicit schema versions, pre-migration backups, and forward migrations;
- preservation of unresolved knowledge and observation history;
- rebuildable derived data;
- rejection of unsupported newer schemas.

Development resources use `examples/` for example data, `schemas/` for schemas, `tests/` for fixtures, and `src/` for application source. No application implementation begins before the v0.0.2 architecture gate is approved.
