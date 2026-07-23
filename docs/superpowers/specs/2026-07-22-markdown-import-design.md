# Markdown Import Slice Design

## Goal

Add the first v0.1 import slice for TWW3 Companion: a Markdown notes adapter that turns pasted or file-based Markdown into import candidates without touching persistence.

This slice is intentionally narrow. It supports the import architecture already accepted in RFC-0004 and focuses only on Markdown notes. Steam Workshop IDs and URLs remain a separate slice.

## Scope

The Markdown adapter reads one Markdown document and produces a normalized import candidate set plus diagnostics.

It must recognize:

- headings as category hints;
- bullet lists as candidate membership or note entries;
- pasted Workshop links and raw Workshop IDs as exact source references;
- free-form prose as notes only.

It must not:

- access persistence;
- make network calls;
- infer dependencies, compatibility claims, or ordering rules from prose;
- delete or replace existing data;
- auto-apply unresolved names;
- treat lossless Workspace JSON as an import input.

## User Experience

The Markdown import flow is preview-first:

1. user supplies Markdown text or a Markdown file;
2. the adapter parses the document and emits candidates plus diagnostics;
3. exact source references match automatically when possible;
4. name-only entries remain unresolved until the user explicitly resolves or skips them;
5. the user reviews the preview and confirms the validated handoff.

Imports are additive-only. Omission never removes a Mod or Membership.

## Architecture

The slice uses one adapter with a simple internal pipeline:

```text
Markdown input
→ parse
→ normalize
→ extract source references
→ produce candidates
→ attach diagnostics and source locations
→ preview / resolution
→ validated handoff
```

The adapter is responsible only for translation from Markdown into the shared candidate model. It does not decide persistence behavior, and it does not own the domain transaction.
The actual workspace write path is deferred to a later slice.

## Candidate Rules

- Headings propose a category value, but they do not decide whether the future Category model is flat or hierarchical.
- Bullet items produce candidate entries with preserved ordering where available.
- Workshop links and IDs become exact source references.
- Free prose is preserved as notes with source metadata.
- Blank fields may be enriched later during preview.
- Distinct imported notes append rather than overwrite.
- Scalar conflicts require an explicit user choice.

## Error Handling

Parsing should be forgiving enough to keep partial progress visible.

- Structural issues become diagnostics attached to the relevant source location.
- Unsupported sections are skipped, not fatal, unless they block safe interpretation of the current document.
- If validation fails during apply, the whole confirmed handoff is rejected.
- If source enrichment or name matching fails, the import still proceeds for explicit user-entered or accepted identities.

## Testing

The first implementation should be covered by tests for:

- headings mapping to category hints;
- bullet lists mapping to candidate entries;
- Workshop IDs and URLs being extracted as exact references;
- free-form prose remaining notes only;
- diagnostics including source line information;
- preview/reporting behavior for validated handoff;
- no persistence or network access from the adapter.

## Non-Goals

- Steam Workshop ID-only adapter behavior;
- `.pack` imports;
- game save imports;
- automatic workshop download;
- replacing or synchronizing collections;
- relationship inference from prose;
- lossless Workspace JSON restore.

## Open Questions Deferred to Later Work

- exact parser library;
- name-similarity algorithm;
- additional Markdown dialect edge cases;
- import session resumability;
- scoped export or sharing behavior.

## Success Criteria

This slice is done when a Markdown note can be parsed into deterministic import candidates and previewed with source-aware diagnostics, with explicit confirmation gating the validated handoff and no persistence writes from the Markdown slice itself.
