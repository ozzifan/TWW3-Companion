# Collection Library Overlay Design

## Goal

Make the desktop workspace surface show the full mod library while clearly marking which mods belong to the currently open workspace, and keep the collection panel driven from the same workspace snapshot.

## Context

The desktop shell already exposes a workspace area with a mod library panel and a collections panel. The view models can render an empty state and respond to selection, but the data currently arrives as an in-memory snapshot contract rather than a concrete workspace-backed source.

The product rule is that the user works in one workspace at a time, but the workspace still needs access to the global mod catalog so every available mod can be shown and membership can be indicated.

Implementation tasks for this feature must be routed through the orchestrator every time; no ad hoc task execution outside the orchestrator flow.

## Design

The read path will be a workspace-scoped overlay on top of the global mod catalog:

- the global catalog is the canonical source for all known mods
- the current workspace supplies membership rows that say which global mods are included
- the query result returns a single snapshot containing:
  - the full mod catalog
  - the workspace collections
  - the membership overlay

The desktop layer does not join these pieces itself. It receives a ready-to-render snapshot and maps it into the existing `ModLibraryViewModel` and `CollectionDetailViewModel`.

Selection behavior stays local to the desktop view models:

- selecting a mod updates the detail inspector
- selecting a collection toggles the highlighted membership markers in the mod list
- the collections panel shows the current collection summary and its empty-state prompt

## Error handling

If the workspace snapshot cannot be loaded, the shell should fail closed:

- keep the existing workspace error surface
- leave the library and collection panels empty
- do not render partial or stale overlay state

## Tests

Cover the following:

- the snapshot overlay marks workspace memberships correctly
- the shell exposes the library and collection view models
- the window binds those panels into the workspace surface
- the desktop still boots cleanly when no workspace query is available
- the existing empty-state and selection behavior still works

## Non-goals

- No new import or editing behavior
- No multi-workspace switching
- No duplication of the global catalog into workspace-local storage
- No persistence schema expansion beyond what is needed for the read overlay
