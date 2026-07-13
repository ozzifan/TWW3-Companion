# Import and Export

This document introduces how data crosses the boundary between TWW3 Companion and the outside world. **No format specifications or parser implementations exist** in the bootstrap phase.

---

## Scope

Import/export adapters translate external representations into the internal domain model (and back) without leaking file-format details into core business logic.

---

## Import Sources (Planned)

| Source | Milestone | Description |
|--------|-----------|-------------|
| Markdown mod lists | v0.1 | Player-maintained lists in common note patterns |
| Steam Workshop IDs | v0.1 | Bulk ID paste or file of numeric IDs |
| Native collection format | v0.1+ | Round-trip from companion export |

Optional future sources (workshop metadata API, other tools' exports) belong in [future.md](future.md) until RFC-approved.

---

## Export Targets (Planned)

| Target | Purpose |
|--------|---------|
| Native collection document | Backup, sync, version migration |
| Markdown summary | Human-readable share in forums or repos |
| ID list | Interop with external load-order tools |

Exports should be **lossless** for fields the companion owns (notes, tags, dependencies). Workshop-only metadata may be omitted if not stored locally.

---

## Adapter Pattern

```
External file / clipboard
        │
        ▼
   Import adapter  ──►  Domain validation  ──►  Persistence
        ▲
   Export adapter  ◄──  Domain snapshot   ◄──  Persistence
```

Each adapter:

- Declares supported format version
- Reports parse warnings (unknown lines, duplicate IDs)
- Never silently drops user-authored fields without notice

---

## Non-Goals

- Importing `.pack` files or game save data
- Writing into Steam workshop or game data folders
- Automatic download of mod archives on import

---

## Next Steps

RFC for markdown grammar conventions and Workshop ID import rules. Example files may live under `examples/` once formats are stable.
