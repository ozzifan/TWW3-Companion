# Future Considerations

This document collects **exploratory ideas** that are not committed to the roadmap. Items here may be promoted via RFC, moved to [ROADMAP.md](../../ROADMAP.md), or rejected.

---

## Purpose

Bootstrap architecture intentionally avoids implementation detail. This file prevents good ideas from being lost while keeping [ROADMAP.md](../../ROADMAP.md) focused on agreed milestones.

---

## Under Consideration

### Workshop metadata enrichment

Periodically fetch public workshop titles, descriptions, and update timestamps by ID. Would require network, caching, and rate-limit policy. Must remain optional.

### Diff between collections

Compare two collection exports: added/removed mods, changed notes, dependency drift. Useful for players who maintain "stable" and "bleeding edge" lists.

### Plugin or extension API

Allow community extensions for custom importers or health rules without forking core. High design cost; defer until domain model stabilises.

### Collaboration and sharing

Export to git-friendly formats; optional read-only published snapshots. Not a hosted sync service by default.

### Localization

UI and documentation in multiple languages after v1.0 English baseline.

### Integration hooks (read-only)

Open a mod's workshop page or copy a load-order snippet for external tools — deep links only, no install automation.

---

## Risks to Monitor

| Risk | Mitigation direction |
|------|----------------------|
| Scope creep into mod management | Enforce non-goals in README and review |
| Opaque health score | Publish criteria; show contributing factors |
| Format churn | Versioned export schema and migration tests |
| Workshop API changes | Adapter isolation; graceful degradation |

---

## Promotion Process

1. Discuss in an issue or draft RFC
2. Product Owner accepts or defers
3. If accepted: add to ROADMAP, expand relevant architecture doc, remove or mark item here

---

## Related

- [ROADMAP.md](../../ROADMAP.md) — committed milestones
- [RFC-0001](../../RFC/RFC-0001.md) — vision and non-goals
