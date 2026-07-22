# Steam Import Slice Design

## Goal

Add the Steam import slice for TWW3 Companion with two distinct user actions:

- Steam Collection import, which accepts one Steam collection ID and expands that collection into member Workshop items; and
- Steam Single Item import, which accepts multiple pasted Workshop IDs or URLs in one action.

Both actions enrich metadata during import so users do not have to discover a separate metadata workflow later.

## Scope

This slice covers Steam Workshop input only.

It must support:

- one collection ID as the primary input for collection import;
- multiple pasted Workshop IDs/URLs for single-item import;
- immediate metadata enrichment for every item the importer can resolve;
- partial success when some lookups fail;
- clear per-item diagnostics for failures;
- preservation of exact source references where available.

It must not:

- mix collection import with pasted single-item import in one UI action;
- write to Steam Workshop or game data folders;
- require the user to defer metadata enrichment to a later step;
- treat lossless Workspace JSON as import input;
- infer relationships or compatibility from prose;
- replace or synchronise collections;
- require all items in a batch to succeed before importing any valid items.

## User Experience

The UI should make the distinction obvious:

- Steam Collection import is one action for one Steam collection ID.
- Steam Single Item import is a separate action for pasted Workshop IDs/URLs, and it may contain multiple items.

The import flow is preview-first and metadata-aware:

1. user chooses the collection action or the single-item action;
2. the importer resolves the collection or individual Workshop identities;
3. metadata enrichment happens during import, not as a later manual step;
4. successful items are imported;
5. failed items are reported with source-aware diagnostics; and
6. the user reviews the preview before confirming the validated handoff.

## Architecture

The slice uses two Steam-facing adapters that feed the shared import candidate model.

```text
Steam collection ID
→ collection adapter
→ collection member expansion
→ metadata enrichment per member
→ candidates + diagnostics
→ preview / resolution
→ validated handoff

Pasted Workshop IDs / URLs
→ single-item adapter
→ normalize multiple inputs
→ metadata enrichment per item
→ candidates + diagnostics
→ preview / resolution
→ validated handoff
```

The adapters are responsible only for translation and enrichment. They do not own persistence. The later handoff path remains the same application-layer boundary used by the Markdown slice.

## Candidate Rules

- Steam collection import always starts from one collection ID.
- Steam single-item import may contain multiple IDs/URLs in one paste.
- Enrichment is part of import, not a deferred follow-up.
- If metadata lookup fails for one item in a batch, the importer keeps the other resolved items.
- Exact source references remain exact when the source supplied one.
- Item-level failures produce diagnostics without blocking unrelated valid items.
- Imported metadata should populate the shared candidate model so the preview can show what the user is about to confirm.

## Error Handling

Parsing and enrichment should be item-scoped where possible.

- Invalid collection IDs fail the collection action.
- Invalid individual Workshop IDs/URLs fail only those items, unless the entire input is malformed.
- Steam metadata lookup failure on one item does not stop other successfully resolved items in the same batch.
- The validated handoff rejects unresolved blocking items, but partial successful enrichment remains visible in preview.
- Network failure should be represented as a diagnostic for the affected item, not as a whole-slice failure when other items can still be resolved.

## Testing

The Steam slice should be covered by tests for:

- one collection ID driving collection import;
- multiple Workshop IDs/URLs in the single-item action;
- metadata enrichment happening during import;
- collection-member expansion producing per-item candidates;
- partial success when one item fails lookup;
- item-scoped diagnostics for failed lookups;
- distinct UI-facing action boundaries between collection import and single-item import;
- preservation of exact source references where supplied.

## Non-Goals

- Markdown note parsing;
- persistence-layer transaction wiring;
- Steam Workshop publishing;
- writing into game folders;
- replacing or synchronising collections;
- automatic download/install into the game;
- relationship inference from prose;
- resumable import sessions.

## Open Questions Deferred to Later Work

- exact Steam metadata API client implementation;
- caching strategy for repeated Workshop lookups;
- UI presentation details for failed items versus imported items;
- whether collection import should later expose a separate review step for each member.

## Success Criteria

This slice is done when a user can choose either Steam Collection import or Steam Single Item import, metadata is enriched during the import action itself, valid items import successfully even if some lookups fail, and the UI clearly distinguishes the two workflows.
