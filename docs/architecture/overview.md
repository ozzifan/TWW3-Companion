# Architecture Overview

This document introduces the high-level architecture of TWW3 Companion. Detailed design for each area lives in sibling documents under `docs/architecture/`.

---

## Purpose

TWW3 Companion is a **desktop knowledge-management application** for Warhammer III mod collections. The architecture separates:

1. **Domain model** — workspaces, the shared Mod Library, collections, memberships, relationships, and evidence ([data-model.md](data-model.md))
2. **Presentation** — how users browse, search, and edit ([ui.md](ui.md))
3. **Boundaries** — how data enters and leaves the app ([import-export.md](import-export.md))
4. **Evolution** — deferred and exploratory ideas ([future.md](future.md))

The initial application uses C# on a supported .NET long-term-support release, Avalonia's desktop target, and MVVM. [RFC-0003](../../RFC/RFC-0003.md) selects embedded SQLite as canonical Workspace storage with lossless JSON export, while [RFC-0005](../../RFC/RFC-0005.md) defines the presentation and UI verification contract. Exact dependency versions and the .NET SQLite data-access library remain implementation-plan decisions that must be approved before their application code lands.

---

## Architectural Principles

| Principle | Implication |
|-----------|-------------|
| Offline-first | Core workflows work without network; optional enrichment (e.g. workshop metadata fetch) fails gracefully |
| Player-owned data | SQLite Workspaces have documented, lossless JSON export |
| Thin integration | No direct modification of game or Steam install paths by default |
| Layered design | UI → application services → domain → persistence |
| Approved before built | See [AGENTS.md](../../AGENTS.md) |

---

## Logical Layers

```
┌─────────────────────────────────────────┐
│  Avalonia UI (Views and ViewModels)     │
├─────────────────────────────────────────┤
│  Application services                   │
│  (import, export, health, search index) │
├─────────────────────────────────────────┤
│  Domain model                           │
│  (Workspace, Collection, Mod, Evidence) │
├─────────────────────────────────────────┤
│  Persistence                            │
│  (embedded SQLite + JSON export)        │
└─────────────────────────────────────────┘
```

External systems (Steam Workshop, markdown files on disk) connect only through **import/export adapters**, not as hard dependencies of the domain core.

Imports use the deterministic staged pipeline accepted in [RFC-0004](../../RFC/RFC-0004.md): adapters produce candidates, exact identities are matched, users resolve an editable preview, and confirmed additive changes are applied atomically.

---

## Document Map

| Document | Scope |
|----------|--------|
| [data-model.md](data-model.md) | Entities, relationships, invariants |
| [ui.md](ui.md) | Screens, workflows, UX constraints |
| [import-export.md](import-export.md) | Input/output formats and adapter boundaries |
| [future.md](future.md) | Non-committed ideas and risks |

---

## Related Reading

- [RFC-0001: Project Vision](../../RFC/RFC-0001.md)
- [RFC-0002: Collection Domain Model](../../RFC/RFC-0002.md)
- [RFC-0003: Storage Architecture](../../RFC/RFC-0003.md)
- [RFC-0004: Import Architecture](../../RFC/RFC-0004.md)
- [RFC-0005: Initial UI Architecture](../../RFC/RFC-0005.md)
- [Glossary](../glossary.md)
- [ROADMAP.md](../../ROADMAP.md)
