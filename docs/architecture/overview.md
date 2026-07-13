# Architecture Overview

This document introduces the high-level architecture of TWW3 Companion. Detailed design for each area lives in sibling documents under `docs/architecture/`.

---

## Purpose

TWW3 Companion is a **desktop knowledge-management application** for Warhammer III mod collections. The architecture separates:

1. **Domain model** — collections, mods, relationships, and metadata ([data-model.md](data-model.md))
2. **Presentation** — how users browse, search, and edit ([ui.md](ui.md))
3. **Boundaries** — how data enters and leaves the app ([import-export.md](import-export.md))
4. **Evolution** — deferred and exploratory ideas ([future.md](future.md))

Implementation technology (language, framework, storage engine) is **not fixed** until approved in **v0.0.2**. Choices will be recorded via RFC and [decisions/](../../decisions/) before code lands in `src/`.

---

## Architectural Principles

| Principle | Implication |
|-----------|-------------|
| Offline-first | Core workflows work without network; optional enrichment (e.g. workshop metadata fetch) fails gracefully |
| Player-owned data | Persistence formats are documented and exportable |
| Thin integration | No direct modification of game or Steam install paths by default |
| Layered design | UI → application services → domain → persistence |
| Approved before built | See [AGENTS.md](../../AGENTS.md) |

---

## Logical Layers

```
┌─────────────────────────────────────────┐
│  UI (views, search, editors)            │
├─────────────────────────────────────────┤
│  Application services                   │
│  (import, export, health, search index) │
├─────────────────────────────────────────┤
│  Domain model                           │
│  (Collection, Mod, Tag, Dependency…)   │
├─────────────────────────────────────────┤
│  Persistence                            │
│  (local files / database — TBD)         │
└─────────────────────────────────────────┘
```

External systems (Steam Workshop, markdown files on disk) connect only through **import/export adapters**, not as hard dependencies of the domain core.

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
- [Glossary](../glossary.md)
- [ROADMAP.md](../../ROADMAP.md)
