# Import Slice Design

**Status:** Draft
**Date:** 2026-07-19
**Scope:** First import vertical slice for v0.1

## Goal

Add the first accepted import workflow on top of the approved workspace foundation. The slice lets a user import informal Markdown or Workshop ID input either into a brand-new Workspace or into the currently open Workspace, while preserving the import rules already accepted in RFC-0004.

This slice does not implement export, relationship inference from prose, Workshop metadata scraping, replayable import sessions, or later roadmap features.

## Approved Dependencies

This slice keeps the workspace foundation stack intact:

- .NET SDK 10.0.302
- `net10.0`
- Avalonia 12.1.0
- `Microsoft.Data.Sqlite.Core` 10.0.10
- `SQLitePCLRaw.bundle_winsqlite3` 2.1.11
- Microsoft logging abstractions and Serilog as already approved in the foundation slice

No additional storage provider, ORM, or parser framework is introduced.

## Slice Shape

The slice has one shared import core and two user entry points:

- Home exposes `Import into new Workspace`.
- The Workspace shell exposes `Import into current Workspace`.

Both entry points use the same import engine. The engine receives a target context so it can either create a fresh Workspace and import into it, or import into the currently open Workspace without changing the Workspace shell structure.

This keeps the UI direct for the user while avoiding two separate import rule sets.

## Import Pipeline

The accepted RFC-0004 pipeline remains the contract:

```text
Input
→ source adapter
→ candidates
→ normalisation
→ exact identity matching
→ suggested name matches
→ editable preview
→ required resolutions
→ domain validation
→ one atomic transaction
```

The slice implements the first usable version of that pipeline for the two accepted sources:

- informal Markdown notes;
- Steam Workshop IDs and supported Workshop URLs.

The adapters stay source-format-specific, but the candidate model, preview rules, matching rules, and atomic application logic are shared.

## Target Contexts

### Import into new Workspace

Home starts the import flow by asking the user for a new Workspace display name and a destination path, then imports the parsed content into that new Workspace before opening it.

This path behaves like a creation flow with import attached to the confirmation step. It must still validate the display name, create the Workspace safely, and keep the imported content isolated from any already-open Workspace.

### Import into current Workspace

The Workspace shell starts the import flow against the currently open Workspace.

The current Workspace remains open throughout preview and resolution. Only the confirmed atomic transaction changes the Workspace contents. Cancellation before confirmation leaves the Workspace unchanged.

## Core Rules

The slice keeps the RFC-0004 import rules intact:

- imports are additive-only;
- source position may propose documented ordering information on Collection Memberships;
- headings propose one editable category value;
- free-form prose remains notes;
- exact Source References may match automatically;
- source-neutral candidates must be linked to an existing Mod, created with a display name, or skipped before application;
- scalar conflicts require an explicit user choice;
- failed validation or persistence rolls back the entire confirmed import.

The slice does not infer dependencies, compatibility claims, or ordering rules from prose.

## UI Responsibilities

The UI is intentionally split by task, not by rule set:

- Home contains the entry point for creating a new Workspace and importing into it.
- The Workspace shell contains the entry point for importing into the open Workspace.
- The preview experience stays shared so the same candidate and resolution rules apply regardless of target.

This keeps the app from presenting two subtly different import semantics.

## Error Handling

The slice must make failure states obvious:

- parse failures stay on the import screen and list diagnostics;
- validation failures identify which entries need user action;
- cancellation before confirmation leaves no persistent change;
- a failure during the confirmed transaction rolls back the entire import;
- target-Workspace failures do not silently change the active screen.

Workspace import never removes existing Mods or Memberships when the source omits them.

## Data and Persistence

The slice may add whatever persistence support is necessary to store imported Mods, Source References, Collections, and Memberships, but it must remain within the approved SQLite boundary and follow the existing direct-SQL pattern.

The Workspace database remains the source of truth. The slice does not add a second import cache or a hidden side database.

## Testing

The slice is complete only when tests cover both entry points and the shared import core:

- Home can begin an import into a new Workspace;
- the Workspace shell can begin an import into the current Workspace;
- Markdown and Workshop ID input both produce the expected candidate and preview behaviour;
- additive-only rules hold;
- the confirmed import is atomic;
- failures roll back;
- the Workspace shell remains stable after import.

Desktop tests should continue to exercise ViewModels and composition seams. Infrastructure tests should cover the parser and persistence rules with isolated files.

## Completion Criteria

The slice is complete when:

- both import entry points exist;
- the shared import pipeline accepts informal Markdown and Workshop IDs;
- the accepted RFC-0004 rules are implemented without prose-based inference;
- successful imports persist to the target Workspace;
- failures are reversible and visible;
- the repo documentation and release history are updated to reflect the new slice.

