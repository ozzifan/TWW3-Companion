# Glossary

Definitions of terms used across TWW3 Companion documentation and UI. Wording may be refined as the data model is implemented; changes should be reflected here and in [architecture/data-model.md](architecture/data-model.md).

---

## Workspace

A **Workspace** is the user-owned top-level domain boundary. It contains one shared Mod Library and one or more Collections, and may record a target game version.

Workspace does not imply a particular file or database layout.

---

## Mod Library

The **Mod Library** is the Workspace's shared set of Mods, Source References, relationships, and game compatibility observations. It allows several Collections to reuse the same Mod knowledge without duplication.

---

## Collection

A **Collection** is a named, curated set of Mods represented through Collection Memberships, together with collection-level notes. A player may maintain several Collections for different play contexts.

---

## Collection Membership

A **Collection Membership** links a Collection to one Mod in the shared library. It holds collection-specific category, tags, rationale, notes, tracking state, and optional ordering information.

---

## Mod

A **Mod** is a source-neutral library record for a modification as understood by the companion, not necessarily installed files or one publishing page.

A Mod has a stable internal identity and may have several Source References, such as a Steam Workshop ID or local reference. Shared notes and relationships belong to the Mod; collection-specific organisation belongs to its memberships. The companion documents Mods; it does not install or enable them in the game.

---

## Profile

A **profile** is a player- or machine-specific view of how a collection is used in practice: which mods are active in a given playthrough, optional grouping, or launch context.

Profiles allow one Collection to support multiple play contexts without duplicating Mod definitions. Profile fields, activation rules, and overrides are deferred to the v0.3 design.

---

## Category

A **category** is a structured grouping stored on a Collection Membership (e.g. "UI," "Faction overhaul," "Framework," "Compatibility patch").

Categories are typically hierarchical or fixed-slot taxonomies chosen by the user or project defaults. They aid browse and report views; they are distinct from free-form tags.

---

## Tag

A **tag** is a flexible, usually flat label attached to a Collection Membership (e.g. `needs-update`, `lore-friendly`, `multiplayer-unsafe`).

Tags support filtering and search without requiring a strict taxonomy. The same Mod may have different tags in different Collections.

---

## Dependency

A **dependency** is a directed relationship between mods: mod A **requires** or **recommends** mod B (or a framework) for correct or intended behaviour.

Dependencies are recorded as knowledge in the companion. The application may warn when a dependency is missing from the collection documentation; it does not auto-install dependencies.

Types are **requires**, **recommends**, and **optional integration**.

---

## Compatibility

**Compatibility** describes whether mods are known to work together, conflict, or need patches/workarounds.

Compatibility notes are **annotated facts** (and sources where possible), not automated scan results from game files. Examples: "incompatible with X unless patch Y is loaded after Z," "safe with current versions as of 2026-01."

An explicit **unknown / needs verification** claim differs from having no recorded claim.

---

## Evidence

**Evidence** records why a relationship or compatibility observation is believed. It may include a source, observation date, game or Mod version, notes, and provenance. Conflicting evidence is retained rather than silently overwritten.

---

## Health Score

The **health score** is a summary indicator of how complete and coherent a collection appears **based on recorded data** in the companion.

It is explainable: derived from factors such as documented missing dependencies, unresolved compatibility warnings, stale or empty notes, and outdated workshop references — not from launching the game or parsing pack files.

A low score means *documented issues or gaps exist*, not necessarily that the game will crash. Criteria will be published so players can improve the score by improving their documentation.
