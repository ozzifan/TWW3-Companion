# Import and Export

This document summarises how data crosses the boundary between TWW3 Companion and the outside world. Import architecture is accepted in [RFC-0004](../../RFC/RFC-0004.md); parser libraries and exact implementation details remain undecided.

---

## Scope

Import/export adapters translate external representations into the internal domain model (and back) without leaking file-format details into core business logic. Imports distinguish reusable library Mods from Collection Memberships.

---

## Import Sources (Planned)

| Source | Milestone | Description |
|--------|-----------|-------------|
| Informal Markdown notes | v0.1 | Headings, lists, Workshop links/IDs, names, and attached prose without a mandatory template |
| Steam Workshop IDs | v0.1 | Pasted or file-based IDs and supported Workshop URLs |

Optional future sources (workshop metadata API, other tools' exports) belong in [future.md](future.md) until RFC-approved.

Lossless Workspace JSON is handled by the separate backup/restore boundary, not by the RFC-0004 import candidate pipeline.

---

## Export Targets (Planned)

| Target | Purpose |
|--------|---------|
| Lossless Workspace JSON | Backup, transfer, inspection, version migration |
| Markdown summary | Human-readable share in forums or repos |
| ID list | Interop with external load-order tools |

Lossless Workspace JSON restore is the inverse of full-Workspace export. It validates and replaces through RFC-0003's restore transaction; it is not a third v0.1 import adapter.

Exports should be **lossless** for fields the companion owns (notes, tags, dependencies). Workshop-only metadata may be omitted if not stored locally.

The accepted lossless JSON format represents a complete Workspace and excludes rebuildable caches. Rules for exporting one Collection with its referenced shared knowledge remain deferred to the import/export RFC. A full-Workspace export must not be presented as a privacy-preserving way to share one Collection.

Live SQLite Workspace files are not a supported cross-machine synchronisation format. JSON export and restore are the supported transfer path until a separate synchronisation design is accepted.

---

## Import Pipeline

```
Input
→ source adapter
→ candidates
→ normalisation
→ exact identity matching
→ suggested name matches
→ editable preview
→ required resolutions
→ domain validation
→ one atomic transaction
```

Each adapter:

- reads one representation without accessing persistence;
- retains source locations and diagnostics;
- never performs domain mutation or implicit network access;
- emits the common candidate model used by later stages.

Exact Source References may match automatically. Names and aliases only suggest matches. Source-neutral candidates must be linked to an existing Mod, created with a display name, or skipped before application.

Imports are additive-only: omission never removes a Membership or Mod. Headings propose one editable category value without deciding whether the future Category domain is flat or hierarchical. Source position proposes documented ordering information on Collection Memberships, but free-form prose remains notes; v0.1 does not infer Dependencies, Compatibility Claims, or ordering rules from natural language.

Blank fields may be enriched after preview. Distinct imported notes append with source document name, date, and source lines. Scalar conflicts require an explicit choice. Failed validation or persistence rolls back the entire confirmed import.

Workshop metadata enrichment is optional and user-initiated. Network failure never prevents importing a valid identity, although every new Mod still requires a user-entered or explicitly accepted display name.

---

## Non-Goals

- Importing `.pack` files or game save data
- Writing into Steam workshop or game data folders
- Automatic download of mod archives on import
- Replace or synchronise Collection imports
- Relationship inference from free-form prose

---

## Deferred Import Work

- exact parser and name-similarity algorithms;
- resource-limit values;
- resumable import sessions;
- additional source adapters;
- replace or synchronise behaviour;
- scoped Collection export and other sharing formats.
