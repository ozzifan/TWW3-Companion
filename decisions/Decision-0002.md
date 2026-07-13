# Decision-0002: Offline First

**Status:** Accepted  
**Date:** 2026-07-13

## Decision

TWW3 Companion is **offline-first**. All core workflows — opening a collection, browsing mods, editing notes and tags, searching, and exporting — must work without a network connection. Network access is optional and limited to explicit, user-initiated enrichment (e.g. fetching public workshop metadata by ID), never required for day-to-day use.

## Rationale

- Players often manage mod collections without reliable internet, or prefer not to expose usage patterns to remote services.
- Collection knowledge is local and personal; tying core functionality to online APIs creates fragility when Steam or workshop endpoints change, rate-limit, or are unavailable.
- Offline-first aligns with the project's role as a documentation companion, not a live workshop client.

## Consequences

- **Positive:** Predictable behaviour; no silent failures when offline; lower privacy surface.
- **Positive:** Forces durable local persistence and documented export formats ([Decision-0003](Decision-0003.md)).
- **Negative:** Workshop titles, update timestamps, and descriptions may be stale unless the user opts in to fetch.
- **Process:** New features must declare their offline behaviour in PRs and architecture docs. Core paths cannot depend on network calls without an RFC and Product Owner approval.
