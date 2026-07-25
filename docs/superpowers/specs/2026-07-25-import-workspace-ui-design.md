# Import Workspace UI Design

**Status:** Approved for implementation planning
**Date:** 2026-07-25
**Scope:** Complete v0.1 import user interface and library-only import destinations

## Goal

Deliver the complete user-facing import workflow promised by RFC-0004 and RFC-0005. A user can start from Home or an open Workspace, choose any supported import source, target the Workspace Mod Library alone or a Collection, inspect and resolve a full preview, explicitly confirm the additive operation, and see the persisted result after an atomic commit.

This slice turns the existing import adapters, shared import engine, schema-v2 catalog persistence, and shell entry points into one usable vertical workflow.

Implementation tasks for this feature must be routed through AI Dev Orchestrator every time. The orchestrator uses the rigid `IMP` implementation role followed by the independent `REV` review role defined in [AGENTS.md](../../../AGENTS.md).

## Current State

The repository already contains:

- Markdown, Steam Collection, and multiple-Steam-item adapters;
- a common candidate and preview pipeline;
- exact source-reference matching and explicit resolution contracts;
- schema-v2 Workspace catalog persistence;
- atomic imports into new and current Workspaces;
- shell commands that can build a preview through the shared engine;
- library reload after a successful current-Workspace import.

The current Desktop shell does not expose the complete source, preview, resolution, confirmation, or Apply workflow. The persistence contract also requires every confirmed import to target one Collection even though RFC-0002 permits a Mod to exist in the Workspace library without any Collection Membership.

## Design Principles

1. Import source and import destination are independent choices.
2. A Mod belongs to one Workspace library and may exist without a Collection Membership.
3. Collection-targeted imports reuse the same library import path and add Membership work conditionally.
4. Preview construction and user resolution perform no persistence.
5. Apply is explicit, additive, and atomic.
6. The full preview remains visible while blocking items are resolved.
7. Expected failures state whether any persistent change committed and provide a safe next action.
8. Imported content, clipboard text, display names, and full local paths are excluded from logs.
9. The import task is keyboard-completable and accessible at the v0.1 minimum window size.

## Architecture

The workflow remains layered:

```text
Desktop import task
→ source adapter
→ common candidates
→ import engine preview
→ user resolutions
→ validated operation plan
→ atomic SQLite commit
→ refreshed Workspace library
```

Views and ViewModels own task presentation, editable session state, navigation, commands, focus, and accessible announcements. Application services own parsing coordination, preview construction, resolutions, validation, and Apply. Infrastructure owns Steam metadata transport, SQLite, filesystem access, migration, and atomic placement.

No View or ViewModel executes SQL, calls static source adapters directly, or performs Steam HTTP requests.

Domain, Application, Desktop ViewModels, and the Desktop import coordinator may depend only on Microsoft logging abstractions when logging is necessary. They must not reference Serilog APIs or packages. Concrete Serilog sinks and configuration remain confined to Infrastructure and the existing composition boundary.

## Import Destination Model

The import contracts separate the Workspace target from the optional Collection destination.

### Workspace target

`NewWorkspace` carries:

- Workspace display name;
- destination `.tww3c` path.

`CurrentWorkspace` carries:

- expected Workspace UUID;
- active Workspace database path.

### Membership destination

Both Workspace targets carry one explicit membership destination:

- `LibraryOnly`;
- `ExistingCollection(collectionId)`;
- `NewCollection(displayName)`.

This is an intentional breaking revision of the sealed `ImportTargetContext` Application contract introduced by the local Workspace catalog persistence slice. `NewWorkspace` no longer carries an unconditional initial Collection display name, and `CurrentWorkspace` no longer carries an unconditional target Collection UUID. Their factories, `IWorkspaceImportStore`, every production caller, and all test callers change together so no persistence-capable target can retain the superseded mandatory-Collection shape.

`ExistingCollection` is invalid for a new Workspace because the Workspace does not exist before Apply. All three destinations are valid for an open Workspace.

The source never dictates the destination. A Steam Collection source initially selects `NewCollection` or the currently selected Collection as the likely choice, but the user may switch to `LibraryOnly`.

The destination choice is never implicit at confirmation time. The preview and confirmation summary name it explicitly.

### Persistence semantics

A library-only commit:

- matches or creates Mods;
- inserts or verifies Source References;
- applies accepted shared Mod enrichment;
- creates no Collection;
- creates no Collection Membership.

A Collection-targeted commit performs the same library work and additionally:

- creates or verifies the target Collection;
- retains all existing Memberships and positions;
- appends only new Memberships in source order;
- does not reorder, remove, replace, or synchronise existing Memberships.

One shared persistence path performs candidate library writes. Collection and Membership work is conditional on the explicit membership destination; there is no separate library-only import engine.

## User Entry Points

### Home

**Import into a new Workspace** opens the import task without creating a file. The destination stage collects:

- Workspace display name;
- destination file;
- `LibraryOnly` or `NewCollection`;
- Collection display name when `NewCollection` is selected.

The destination file appears only after a confirmed import commits and atomic placement succeeds.

### Open Workspace

**Import** in the Workspace shell opens the same task. The destination stage offers:

- `LibraryOnly`;
- any existing Collection;
- `NewCollection`.

If the command was invoked from a selected Collection, that Collection is the initial suggestion. The user may change it.

## Full-Page Workflow

Import is one full-page task with four visible stages:

```text
1. Source
→ 2. Destination
→ 3. Preview and resolve
→ 4. Confirm and Apply
```

This intentionally expands RFC-0005's abbreviated import flow by making Destination a separate visible stage. It does not change RFC-0005's full-page, preview, resolution, confirmation, or atomic-Apply requirements. The implementation must update the maintained architecture summary in `docs/architecture/ui.md` to show the four-stage flow.

Back is available before Apply. Changing source or destination invalidates the previous confirmation and rebuilds the preview because matching, Membership outcomes, and summary counts may differ.

### Stage 1: Source

The source choices are visually distinct:

1. **Markdown notes**
   - paste text;
   - choose a supported text file.
2. **Steam Collection**
   - enter exactly one Steam Collection ID or supported URL.
3. **Steam items**
   - paste multiple Workshop IDs or supported URLs;
   - choose a supported text file containing one item per logical line.

Paste and file input feed the same source adapter after safe decoding. Selected-file handling follows RFC-0004 encoding rules. The application retains only the source document name needed for provenance, not the full path.

Steam Collection remains a distinct action from multiple individual Steam items. A Collection action accepts one Collection identity per import session.

#### Metadata enrichment

Workshop metadata access is explicit and user initiated. Before requesting metadata, the UI identifies that the disclosed Workshop IDs will be contacted.

For Steam sources, the Continue action requests metadata and builds the preview. Markdown inputs containing recognised Workshop identities offer the same disclosed enrichment action before preview.

Metadata failure never discards a valid Workshop identity. A candidate without an accepted or entered display name remains visible and blocking until the user:

- accepts available metadata;
- enters a display name;
- links the candidate to an existing Mod;
- or skips the candidate.

### Stage 2: Destination

The user explicitly chooses:

- **Add to Mod Library only**;
- **Add to an existing Collection**;
- **Add to a new Collection**.

Invalid combinations are unavailable rather than accepted and rejected later. The selected Workspace and Collection destination are named in the stage and in the final confirmation.

### Stage 3: Preview and Resolve

The full candidate table remains visible throughout resolution. Each row reports two independent outcomes.

#### Library outcome

- Create;
- Enrich;
- Existing exact match;
- Suggested match;
- Conflict;
- Skip.

`Conflict` in the Library outcome means an RFC-0004 resolvable scalar conflict between different non-empty values. It enters Needs Attention and requires an explicit value choice or Skip. A Source Reference already owned by a different Mod is not represented as this scalar outcome: it is a blocking candidate validation error that names the ownership collision and requires linking to the owning Mod or skipping the candidate before Apply.

#### Membership outcome

- None, for library-only;
- Add;
- Already present;
- Blocked;
- Skip.

Filters cover:

- Additions;
- Enrichments;
- Existing;
- Suggested Matches;
- Conflicts;
- Warnings;
- Skipped.

The **Needs Attention** queue presents one blocking candidate at a time without hiding the full table. Supported resolutions include:

- link to an existing Mod;
- create a new Mod;
- accept or edit a display name;
- resolve a source-identity or scalar conflict;
- skip the candidate.

Apply remains unavailable while any blocking issue exists or the resulting operation plan is invalid.

### Stage 4: Confirmation and Apply

The confirmation summary is immutable unless the user goes Back. It states exact counts for:

- Mods created;
- Mods enriched;
- existing Mods unchanged;
- Collections created;
- Memberships added;
- existing Memberships unchanged;
- candidates skipped.

Warnings are non-blocking under RFC-0004 and have no separate acceptance action. The confirmation page instead shows **Warnings remaining: N**, where `N` is the number of warning records attached to non-skipped candidates in the operation plan. The count links back to the Warnings filter. Proceeding confirms the complete operation summary but does not change warning state or describe warnings as individually accepted.

The page explicitly states:

> This import is additive. It does not replace or synchronise your Mod Library or Collections.

Apply requires an explicit user action and executes one atomic operation.

## Presentation Components

The import task is isolated from `ShellViewModel` through focused components.

### `ImportWorkspaceViewModel`

Owns:

- task stage;
- the mutable import-session state and lifetime;
- the immutable launch context supplied by Home or the Workspace shell;
- Back and Continue navigation;
- Apply lifecycle;
- safe task dismissal;
- focus restoration;
- accessible status announcements.

The launch context identifies whether the task began from Home or an open Workspace and, for an open Workspace, includes its UUID, path, available Collection summaries, and optional selected Collection UUID. `ImportWorkspaceViewModel` passes the chosen source type and launch context to `ImportDestinationViewModel`. This is the only signal path used to calculate the initial destination suggestion:

- a selected shell Collection is suggested when present;
- otherwise a Steam Collection source suggests `NewCollection`;
- other sources require an explicit library-only, existing-Collection, or new-Collection choice.

The suggestion initializes editable session state once. Later source changes do not silently overwrite a destination the user has already chosen.

### `ImportSourceViewModel`

Owns:

- source selection;
- paste/file input state;
- decoding and parsing progress;
- metadata disclosure and request;
- source diagnostics;
- retryable source errors.

### `ImportDestinationViewModel`

Owns:

- Workspace details for a new target;
- available existing Collections;
- library-only, existing-Collection, and new-Collection selection;
- destination validation.

### `ImportPreviewViewModel`

Owns:

- candidate rows;
- Library and Membership outcomes;
- filters;
- warning and blocking counts;
- operation totals;
- Apply eligibility.

### `ImportResolutionViewModel`

Owns:

- the active blocking candidate;
- link/create/skip choice;
- editable resolution fields;
- validation feedback;
- movement through the Needs Attention queue.

### `ImportConfirmationViewModel`

Owns:

- immutable operation summary;
- additive-operation explanation;
- explicit Apply command;
- finalization status.

### Desktop import coordinator

`IImportTaskCoordinator` is a Desktop-layer presentation service implemented beside the import ViewModels and constructed by `ApplicationComposition`. It is a stateless façade over injected source-specific Application services, Workshop metadata access, the shared import engine, and the existing file-picker abstraction.

It exposes intention-revealing asynchronous operations for:

- selecting and safely decoding a source file into source document name plus text;
- parsing pasted or decoded Markdown;
- expanding one Steam Collection;
- parsing multiple Steam items;
- disclosing and requesting Workshop metadata;
- building a preview from common candidates and the explicit target context;
- applying one confirmed preview.

The coordinator returns typed source, preview, and Apply results. It does not own navigation, source text, destination choice, resolutions, confirmation state, or session lifetime; `ImportWorkspaceViewModel` owns those values. It does not execute SQL or filesystem persistence itself. Apply delegates to the shared Application import engine, whose Infrastructure store owns atomic mutation.

The six ViewModels depend on `IImportTaskCoordinator` and narrower existing presentation abstractions where appropriate. All dependencies are injected so unit tests do not require real file pickers, Steam calls, or SQLite.

## Session and Navigation Rules

- Back preserves source input and resolutions that remain applicable.
- A source or destination change invalidates the prior preview and confirmation.
- Going Back and continuing without changing the source or destination reuses the existing preview and resolutions; it does not parse, request metadata, or rebuild merely because the user revisited a stage.
- `ImportWorkspaceViewModel` compares an immutable source-and-destination fingerprint before Continue. A changed fingerprint rebuilds the preview and retains only resolutions whose candidate identity and available choices are unchanged; an unchanged fingerprint restores the existing preview directly.
- Rebuilding a preview performs no persistent write.
- Navigating away with an active preview asks whether to discard the import session.
- Cancellation before the atomic finalization boundary leaves no persistent change and removes operation-owned temporary artifacts.
- During non-cancellable atomic finalization, navigation and cancellation disable and the UI announces **Finalizing — please wait**.
- A successful new-Workspace import opens that Workspace.
- A successful current-Workspace library-only import returns to the refreshed Mod Library.
- A successful Collection-targeted import returns to the refreshed target Collection.

## Error Handling

Expected failures are represented as import task state rather than unhandled exceptions.

### Source and file failures

Examples:

- unsupported or ambiguous encoding;
- unreadable selected file;
- invalid Steam Collection identity;
- malformed Workshop items;
- empty input;
- source input beyond accepted limits.

The UI identifies the field or source, retains safe input state, and offers correction or retry.

### Metadata failures

Metadata failures identify affected candidates and retain valid source identities. The user may retry, enter required values, link existing Mods, or skip.

### Preview and validation failures

Blocking issues remain in the Needs Attention queue. The UI distinguishes correctable candidate issues from target-level failures that require returning to Destination.

### Persistence failures

Lock, access, corruption, stale Workspace identity, missing Collection, source ownership conflict, constraint, cancellation, and atomic-placement failures retain the preview and resolutions whenever retrying is safe. The result states:

- whether any persistent change committed;
- the affected operation;
- a safe next action.

Unexpected exceptions are logged through the established application boundary and converted to a generic no-change failure. Logs exclude imported prose, names, notes, clipboard contents, source IDs where not operationally required, full source paths, and full Workspace paths.

## Accessibility and Layout

The entire workflow is keyboard-completable using standard Tab, Shift+Tab, arrow, Enter, and Escape behavior.

Requirements include:

- standard Avalonia controls and UI Automation semantics;
- accessible names and state for source and destination choices;
- logical focus order;
- focus moved to the first invalid field or active Needs Attention item;
- visible focus;
- live announcements for parsing, metadata, preview readiness, blocking counts, Apply progress, success, cancellation, and failure;
- no color-only status meaning;
- Windows High Contrast support;
- text scaling without losing Apply, Back, or resolution controls;
- usable layout at the 1024 × 640 logical minimum.

The preview table and Needs Attention pane may reflow vertically at the minimum size, but neither becomes a modal dialog and the full preview remains reachable.

## Verification

This slice adds no new executable test hook. ViewModel, coordinator, file-picker, metadata, and persistence behavior use constructor-injected test seams; ordinary unit-test dependency injection is not a runtime hook and must not depend on an environment variable. Existing executable smoke hooks remain gated by `TWW3_COMPANION_TEST_MODE=1`. Any proposal for an additional runtime-only test command requires an explicit spec amendment and the same environment-variable gate before it can enter an implementation plan.

### Application and Infrastructure tests

Automated tests cover:

- optional Collection destination contracts and invalid target combinations;
- library-only new-Workspace and current-Workspace imports;
- existing-Collection and new-Collection imports;
- importing the same Mod library-only and later adding it to one or more Collections without duplication;
- no Collection or Membership rows for library-only imports;
- unchanged additive and position-preservation behavior for Collection imports;
- preview rebuilding after source or destination changes;
- no persistence before explicit Apply;
- exact confirmation-summary counts;
- metadata success, partial failure, complete failure, and retry;
- atomic rollback for every destination form;
- retained preview state after retryable persistence failure;
- successful new-Workspace opening and current-Workspace reload.

### Desktop tests

Automated tests cover:

- Home and Workspace entry points;
- source-choice distinction;
- paste and file paths for supported text sources;
- Steam Collection single-identity validation;
- multiple Steam-item input;
- destination choice and defaults;
- absence of the existing-Collection option for a new-Workspace import;
- stage navigation and state invalidation;
- preview filters and independent Library/Membership outcomes;
- Needs Attention resolution;
- Apply command eligibility;
- success, cancellation, and error presentation;
- focus targets and accessible labels;
- minimum-window layout bindings.

### Full verification

The implementation plan must include:

- formatter verification;
- Release build;
- complete automated test suite;
- local Markdown-link validation;
- `git diff --check`;
- a real Desktop smoke test for the supported source and destination combinations;
- manual Windows Narrator, High Contrast, text scaling, file-picker, cancellation, and real Steam metadata checks.

## Non-Goals

This slice does not implement:

- Mod or Collection editing outside import;
- deletion, replacement, or synchronisation;
- JSON backup or restore;
- managed tags or categories;
- Profiles;
- Dependency or Compatibility Claim editing;
- health scoring;
- installer or portable packaging work;
- additional import adapters;
- import of `.pack` files or game save data.

Markdown category proposals remain preview data only; this slice does not decide or implement the later managed Category domain.

## Documentation

Implementation updates:

- [CHANGELOG.md](../../../CHANGELOG.md);
- [ROADMAP.md](../../../ROADMAP.md);
- [docs/project-history.md](../../project-history.md);
- [docs/architecture/import-export.md](../../architecture/import-export.md);
- [docs/architecture/ui.md](../../architecture/ui.md);
- [docs/development.md](../../development.md) when new manual verification steps are introduced.

The older local Workspace catalog persistence design remains accurate as a record of that slice, but current architecture documentation must state that the mandatory Collection target was superseded by the explicit library-only or Collection destination choice.

## Completion Criteria

The slice is complete when:

1. a user can start import from Home or an open Workspace;
2. Markdown, one Steam Collection, and multiple Steam items accept the approved paste/file forms;
3. source and destination remain independent;
4. library-only import creates or enriches Mods without Collections or Memberships;
5. existing-Collection and new-Collection imports create only additive Membership changes;
6. the full preview remains visible while every blocking item is resolved;
7. Apply is explicit and unavailable for an invalid plan;
8. confirmed operations commit atomically and failures clearly report no partial change;
9. successful imports refresh and navigate to the appropriate persisted result;
10. automated, accessibility, smoke, documentation, and independent `REV` gates pass.
