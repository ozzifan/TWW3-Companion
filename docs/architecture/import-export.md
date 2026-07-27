# Import and Export

This document summarises how data crosses the boundary between TWW3 Companion and the outside world. Import architecture is accepted in [RFC-0004](../../RFC/RFC-0004.md); parser libraries and exact implementation details remain undecided.

---

## Scope

Import/export adapters translate external representations into the internal domain model (and back) without leaking file-format details into core business logic. Imports distinguish reusable library Mods from Collection Memberships.

---

## Import Sources (v0.1)

| Source | Description |
|--------|-------------|
| Informal Markdown notes | Headings, lists, Workshop links/IDs, names, and attached prose without a mandatory template; paste or file |
| Steam Collection | One numeric collection ID or supported URL; expands to member Workshop IDs after explicit metadata request |
| Steam items | One or more Workshop IDs or URLs; paste or file |

Import source and import destination are independent. A Steam Collection source does not require a Collection destination.

Optional future sources (workshop metadata API, other tools' exports) belong in [future.md](future.md) until RFC-approved.

Lossless Workspace JSON is handled by the separate backup/restore boundary, not by the RFC-0004 import candidate pipeline.

---

## Export Targets

| Target | Status | Purpose |
|--------|--------|---------|
| Lossless Workspace JSON (`workspace-export-v1`) | Shipped in v0.1 | Backup, transfer, inspection, version migration |
| Markdown summary | Planned | Human-readable share in forums or repos |
| ID list | Planned | Interop with external load-order tools |

### Lossless Workspace JSON (`workspace-export-v1`)

The shipped complete v0.1 Workspace format is identified as `workspace-export-v1`. Its JSON Schema lives at [schemas/workspace-export-v1.schema.json](../../schemas/workspace-export-v1.schema.json).

Every export contains:

- the format identifier;
- the Workspace UUID and metadata (`displayName`, `createdUtc`, `modifiedUtc`);
- every persisted Mod and stable UUID;
- Source References (`sourceType`, `externalId`, `modId`);
- every Collection and stable UUID;
- every ordered Collection Membership with its preserved `position`.

Exports exclude database schema details, local paths, application settings, managed-backup history, and rebuildable caches. Single-Collection snapshot export remains deferred.

Serialization is deterministic: Mods order by ID, Source References by source type then external ID, Collections by ID, and Memberships by Collection ID then position then Mod ID. Repeated exports of unchanged authoritative data produce byte-stable UTF-8 JSON (two-space indentation, trailing newline, no BOM).

Restore validates the complete export before any destination mutation. It preserves every UUID and Membership position; it never merges or regenerates identities. Restore as a new Workspace creates a `.tww3c` file at a user-chosen path that must not already exist. Replacement restore targets the open Workspace path, requires explicit confirmation, creates a managed `pre-restore` SQLite backup, and retains the five newest managed automatic backups total per Workspace UUID across `pre-migration` and `pre-restore` reasons.

User-selected JSON exports are never subject to automatic cleanup.

---

## Import Pipeline

Desktop import task:

```text
Source
→ Destination
→ Preview and resolve
→ Confirm and Apply
```

Engine pipeline (no persistence before Apply):

```text
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

### Import destinations

Every confirmed import targets a Workspace and an explicit membership destination:

| Destination | New Workspace | Current Workspace |
|-------------|---------------|-------------------|
| Mod Library only | Yes | Yes |
| Existing Collection | No | Yes |
| New Collection | Yes | Yes |

The prior mandatory-Collection import rule is superseded. `ImportTargetContext` carries `LibraryOnly`, `ExistingCollection(collectionId)`, or `NewCollection(displayName)` instead of an unconditional target Collection.

Library-only commits create or enrich Mods and Source References only. Collection-targeted commits perform the same library work and additionally create or verify the target Collection and append new Memberships in source order without removing, reordering, replacing, or synchronising existing Memberships.

Each adapter:

- reads one representation without accessing persistence;
- retains source locations and diagnostics;
- never performs domain mutation or implicit network access;
- emits the common candidate model used by later stages.

Exact Source References may match automatically. Names and aliases only suggest matches. Source-neutral candidates must be linked to an existing Mod, created with a display name, or skipped before application.

Imports are additive-only: omission never removes a Membership or Mod. Headings propose one editable category value without deciding whether the future Category domain is flat or hierarchical. Source position proposes documented ordering information on Collection Memberships, but free-form prose remains notes; v0.1 does not infer Dependencies, Compatibility Claims, or ordering rules from natural language.

Blank fields may be enriched after preview. Distinct imported notes append with source document name, date, and source lines. Scalar conflicts require an explicit choice. Failed validation or persistence rolls back the entire confirmed import.

Workshop metadata enrichment is optional and user-initiated. The UI discloses which Workshop IDs will be requested when the user continues from Source (local parse only, no network). Metadata is fetched when the user continues from Destination. Partial metadata failure surfaces as diagnostics; valid identities remain importable and every new Mod still requires a user-entered or explicitly accepted display name.

Preview construction and user resolution perform no persistence. Warnings are counted as warnings remaining, not as accepted outcomes. Failed Apply retains the preview and resolution state; successful Apply reloads the Workspace library.

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
