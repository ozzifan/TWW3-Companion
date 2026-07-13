# Data Model

This document introduces the conceptual data model for TWW3 Companion. **No storage schema or serialization format is finalised** in the bootstrap phase.

---

## Scope

The data model answers:

- What entities exist (collection, mod, profile, tag, etc.)?
- How do they relate?
- What invariants must hold (e.g. unique Workshop IDs within a collection)?

Terminology aligns with [glossary.md](../glossary.md).

---

## Core Entities (Conceptual)

### Collection

Root aggregate. Contains mods, organisation structures (categories, tags), collection-level notes, and computed summaries (e.g. health score inputs).

### Mod

A documented workshop or local mod reference. Holds identifiers, display fields, user notes, and links to relationships.

### Profile

Optional lens on a collection representing a particular active configuration or play context. Exact fields deferred to a future RFC.

### Organisation

- **Categories** — structured grouping
- **Tags** — flexible labels

### Relationships

- **Dependencies** — require / recommend edges between mods
- **Compatibility** — annotated compatibility records between mods or mod sets

---

## Invariants (Draft)

These principles guide later schema design:

1. A mod within a collection has at most one canonical entry per stable ID (e.g. Workshop ID).
2. Dependencies and compatibility are explicit records, not inferred solely from names.
3. Deleting a mod cascades or prompts according to documented rules (TBD in RFC).
4. Health score inputs are derivable from stored facts, not hidden state.

---

## Persistence

Whether collections are stored as JSON documents, SQLite, or hybrid storage is **undecided**. Requirements:

- Human-inspectable export
- Atomic save where possible
- Version field for migration

---

## Next Steps

A dedicated RFC will propose concrete schemas, migration strategy, and file layout under `data/`. Until then, treat this document as the shared vocabulary for design discussions.
