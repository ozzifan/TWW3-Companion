# Decision-0006: Deterministic Staged Import

**Status:** Accepted
**Date:** 2026-07-16

## Decision

TWW3 Companion imports informal Markdown notes and Steam Workshop ID lists through deterministic source adapters that produce a common candidate model. Parsing, normalisation, identity matching, preview resolution, domain planning, and persistence remain separate stages.

Every import is previewed before mutation and applied additively through one atomic transaction. Exact Source References may match automatically; names and aliases only suggest matches. Source-neutral Mods are supported, but ambiguous candidates must be linked, created, or skipped before application.

The complete parsing, matching, conflict, privacy, network, and verification contract is defined in [RFC-0004](../RFC/RFC-0004.md).

## Rationale

- Informal input accepts users' existing notes without imposing a proprietary Markdown template.
- Deterministic stages are explainable, testable, offline-capable, and reusable across source adapters.
- Explicit identity resolution prevents accidental merging of personal, local, forked, or edited Mods.
- Additive-only application prevents omission from becoming destructive.
- Preview and atomic persistence protect user-authored knowledge.

## Consequences

- No source adapter writes directly to the Workspace.
- v0.1 imports Markdown and Workshop IDs without inferring relationships from prose.
- Optional Workshop metadata is user-initiated and never required for identity import.
- Re-import idempotency is guaranteed only where stable Source References identify existing Mods; name-only candidates require resolution again.
- Heading-derived category proposals normalise whitespace and reuse case-insensitive existing matches without deciding the domain's future Category hierarchy.
- Replace/synchronise imports, resumable sessions, exact resource limits, and single-Collection export remain deferred.
