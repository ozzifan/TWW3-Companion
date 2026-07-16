# User Interface

This document introduces UI architecture and experience constraints for TWW3 Companion. **No wireframes, component library, or framework choices are finalised** in the bootstrap phase.

---

## Scope

The UI layer presents collection knowledge and supports:

- Browsing and filtering mods
- Editing notes, tags, categories, and relationships
- Import and export workflows
- Displaying health score and compatibility warnings with traceable reasons

The UI does **not** present itself as a game launcher or mod installer.

---

## Design Constraints

| Constraint | Rationale |
|------------|-----------|
| Clarity over density | Large collections need scannable lists and strong search |
| Explainability | Warnings and health indicators link to underlying records |
| Keyboard-friendly | Power users manage hundreds of entries |
| Offline-capable | No hard dependency on live workshop UI |
| Accessible | Target WCAG-oriented patterns when stack is chosen |
| Single instance | A second process explains that TWW3 Companion is already running, then exits before accessing data |

---

## Primary Workflows (Planned)

1. **Open / create workspace** — start from empty, import, or recent local data
2. **Browse** — select a Collection, then view its memberships by category or tag while retaining access to shared Mod details
3. **Search** — global and in-context query across names, IDs, notes
4. **Mod detail** — notes, dependencies, compatibility, metadata
5. **Import** — informal Markdown or Workshop IDs through an editable preview, required identity resolution, and explicit additive confirmation
6. **Export** — backup and share documented formats
7. **Health overview** — summary dashboard with drill-down to issues

Removing a membership must be visually distinct from deleting a Mod from the shared library. Library deletion requires an impact summary naming every affected Collection and explicit confirmation.

Screen inventory and navigation model will be expanded in a UI RFC after v0.1 persistence exists.

---

## Separation from Domain

Views consume **application services** (search, import, health calculation), not raw persistence. This keeps the UI boundary replaceable if ever required, though the initial target is a Windows desktop application.

---

## Next Steps

- Personas and primary use cases from playtest feedback
- Wireframes for v0.1 (import + persistence) and v0.2 (search + tags)
- Technology choice recorded in [decisions/](../../decisions/) before `src/` implementation
