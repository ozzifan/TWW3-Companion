# Glossary

Definitions of terms used across TWW3 Companion documentation and UI. Wording may be refined as the data model is implemented; changes should be reflected here and in [architecture/data-model.md](architecture/data-model.md).

---

## Collection

A **collection** is the top-level unit of data in TWW3 Companion: a curated set of mods together with metadata that describes how they are organised, annotated, and related.

A player may maintain multiple collections (e.g. "Immortal Empires main," "custom battle only," "deprecated experiments"). Each collection is persisted as a distinct document or database record under the user's control.

---

## Mod

A **mod** is a single workshop item or locally referenced modification as represented *in the companion* — not necessarily the installed files on disk.

At minimum, a mod entry typically includes a stable identifier (e.g. Steam Workshop ID), display name, and optional metadata (tags, notes, dependencies). The companion documents mods; it does not install or enable them in the game.

---

## Profile

A **profile** is a player- or machine-specific view of how a collection is used in practice: which mods are active in a given playthrough, optional grouping, or launch context.

Profiles allow one collection knowledge base to support multiple runtime configurations without duplicating mod definitions. Exact profile behaviour will be specified in the data model RFC; at vision level, a profile answers *"this is how I run this collection right now."*

---

## Category

A **category** is a structured grouping for mods within a collection (e.g. "UI," "Faction overhaul," "Framework," "Compatibility patch").

Categories are typically hierarchical or fixed-slot taxonomies chosen by the user or project defaults. They aid browse and report views; they are distinct from free-form tags.

---

## Tag

A **tag** is a flexible, usually flat label attached to a mod (e.g. `needs-update`, `lore-friendly`, `multiplayer-unsafe`).

Tags support filtering and search without requiring a strict taxonomy. A mod may have many tags; tags may be shared across collections or defined per collection depending on implementation.

---

## Dependency

A **dependency** is a directed relationship between mods: mod A **requires** or **recommends** mod B (or a framework) for correct or intended behaviour.

Dependencies are recorded as knowledge in the companion. The application may warn when a dependency is missing from the collection documentation; it does not auto-install dependencies.

Types (require vs recommend vs optional) will be detailed in the data model.

---

## Compatibility

**Compatibility** describes whether mods are known to work together, conflict, or need patches/workarounds.

Compatibility notes are **annotated facts** (and sources where possible), not automated scan results from game files. Examples: "incompatible with X unless patch Y is loaded after Z," "safe with current versions as of 2026-01."

---

## Health Score

The **health score** is a summary indicator of how complete and coherent a collection appears **based on recorded data** in the companion.

It is explainable: derived from factors such as documented missing dependencies, unresolved compatibility warnings, stale or empty notes, and outdated workshop references — not from launching the game or parsing pack files.

A low score means *documented issues or gaps exist*, not necessarily that the game will crash. Criteria will be published so players can improve the score by improving their documentation.
