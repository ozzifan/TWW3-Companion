# User Interface

TWW3 Companion uses C# on a supported .NET long-term-support release, Avalonia's desktop target and Fluent theme, and Model-View-ViewModel (MVVM). [RFC-0005](../../RFC/RFC-0005.md) is the authoritative initial UI contract.

---

## Platform and Window Model

Version 0.1 supports Windows 10 or later on x64 through self-contained installer and portable distributions. Users do not install .NET or Avalonia separately. ARM64 and non-Windows packages are deferred.

The application uses one main window and starts on Home without opening the most recent Workspace automatically. Home provides Create Workspace, Open Workspace, Import into a new Workspace, recent Workspaces, and basic application settings.

The default window size is 1280 × 800 logical pixels and the minimum is 1024 × 640. The minimum supported display is 1280 × 720 at 100% Windows scaling, or a display with enough effective space for the logical minimum at increased scaling. An unsupported work area produces a compatibility screen with Exit and Continue Anyway choices; continuing retains a session warning and may clip the fixed minimum layout.

RFC-0003's single-instance lock is acquired before settings or Workspace data load. A second process explains why it cannot start and exits.

---

## Presentation Boundary

Views define layout, visual states, bindings, accessibility properties, and platform presentation. ViewModels own screen state, selection, commands, editable drafts, validation, progress, cancellation, and accessible announcements.

Views and ViewModels do not execute SQL or bypass application services. Application services own Workspace, Mod, Membership, import, backup, restore, deletion-impact, and settings operations and return typed success, validation, failure, cancellation, or blocking results.

---

## Workspace Shell

An open Workspace uses three persistent regions:

1. a sidebar for the Mod Library, Collections, Import, and relevant settings;
2. a central master list for the selected library or Collection;
3. a context-sensitive detail pane.

Only destinations implemented for the current milestone appear. Later features are not represented by disabled or empty placeholders.

Selecting a library Mod shows shared Mod detail. Selecting a Collection Membership shows two tabs:

- **Collection Details** — Collection-local category, tags, rationale, notes, tracking state, and ordering knowledge;
- **Shared Mod** — display name, aliases, Source References, imported metadata, shared notes, and supported relationship or compatibility knowledge.

The tabs use separate ViewModels, drafts, validation, and Save commands. Collection Details is selected first for a Membership so the UI preserves RFC-0002's ownership boundary.

---

## Editing and Destructive Actions

Details are read-only until the user chooses Edit. Save applies one valid changed draft through an application service; Cancel discards it. Navigation with unsaved changes offers Save, Discard, or Stay. Save failure retains the draft and states that no change committed. Version 0.1 does not autosave field edits.

Removing a Membership uses a compact confirmation that names its Collection-local effects and states that the shared Mod remains. Deleting a library Mod requires a complete impact dialog naming affected Collections, Membership knowledge, deleted outgoing Relationships, and unresolved incoming Relationships before the destructive action becomes available. Collection deletion separately states that Memberships are removed while shared Mods remain.

---

## Import Workspace

Import is a full-page task reachable from Home (**Import into a new Workspace**) or the Workspace shell (**Import**). It uses four visible stages:

```text
1. Source
→ 2. Destination
→ 3. Preview and resolve
→ 4. Confirm and Apply
```

Source and destination are independent. Supported sources are Markdown (paste or file), one Steam Collection, and multiple Steam items (paste or file). Continuing from Source parses locally and discloses Workshop IDs without network access. Continuing from Destination requests disclosed metadata and builds the preview.

The destination stage collects:

- **New Workspace** — display name, destination `.tww3c` path, and `LibraryOnly` or `NewCollection` with a display name;
- **Current Workspace** — `LibraryOnly`, an existing Collection, or `NewCollection`.

Back is available before Apply. Changing source or destination invalidates confirmation and rebuilds the preview.

Stage 3 keeps the full RFC-0004 preview visible alongside the Needs Attention resolution queue while blocking identity choices, missing display names, ambiguous categories, and conflicts are resolved. Apply remains unavailable until every blocking item is resolved and the domain plan validates. Warnings appear as a warnings-remaining count, not as accepted outcomes. Preview and resolution state are not persisted.

Stage 4 shows exact operation counts, describes the import as additive (never replace or synchronise), and requires explicit Apply for one atomic commit. Failed Apply retains preview state; successful Apply reloads the library and returns to the Workspace shell with the appropriate Collection or Mod Library selected.

---

## Feedback, Responsiveness, and Safety

- Field errors appear beside their controls and in an accessible summary when several fields fail.
- Recoverable failures use a persistent page banner that identifies the operation, whether anything committed, and a safe next action.
- Success uses a brief visible and assistive-technology announcement without stealing focus.
- Dialogs are reserved for blocking decisions.
- Empty, loading, unavailable, and error states replace blank panes.

File, import, migration, backup, restore, and persistence work does not block the UI thread. Cancellation is available only before an atomic commit enters its non-cancellable section. The application never reports success until completion or rollback is known.

---

## Keyboard, Accessibility, and Theme

Every v0.1 workflow is keyboard-completable using standard Tab, Shift+Tab, arrow, Enter, Escape, and confirmation behaviour. Global shortcuts are `Ctrl+N` for Create Workspace, `Ctrl+O` for Open Workspace, and `Ctrl+S` for the active valid edit. Delete opens a confirmation and never deletes immediately.

Accessibility requirements include standard controls and UI Automation semantics, visible focus, accessible names and states, logical focus order, live announcements, no colour-only meaning, text scaling, Windows High Contrast, and manual Windows Narrator verification. Custom controls require equivalent keyboard and automation behaviour.

Theme choices are System, Light, and Dark. Windows High Contrast overrides the stored choice while active; the application restores the stored choice when High Contrast is turned off.

---

## Deferred UI Scope

Version 0.1 does not include search and organisation screens from later milestones, Profiles, health dashboards, a full relationship editor, single-Collection export, multiple windows, detachable panes, a command palette, custom shortcut editing, or custom skins.

Exact .NET, Avalonia, and dependency versions are pinned in the implementation plan. The storage implementation plan separately proposes a .NET SQLite data-access library that preserves RFC-0003's storage and packaging boundaries and requires Product Owner approval before storage code lands.

---

## Related Reading

- [RFC-0002: Collection Domain Model](../../RFC/RFC-0002.md)
- [RFC-0003: Storage Architecture](../../RFC/RFC-0003.md)
- [RFC-0004: Import Architecture](../../RFC/RFC-0004.md)
- [RFC-0005: Initial UI Architecture](../../RFC/RFC-0005.md)
- [Decision-0007: Avalonia MVVM User Interface](../../decisions/Decision-0007.md)
