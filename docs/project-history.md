# Project History

## Origins

TWW3 Companion did not begin as a product roadmap or a framework choice. It began with a practical problem: **reviewing a large Total War: Warhammer III mod collection** that had grown organically over months of workshop browsing, faction experiments, and compatibility patching.

What started as a simple audit — *what do I actually have installed, and does it still make sense?* — exposed how poorly suited ad-hoc tools are for long-term collection knowledge:

- Workshop subscriptions show what is available, not what you intended to run together.
- Load-order tools focus on ordering and enabling, not on *why* a mod is in the list or what breaks if you remove it.
- Notes scattered across markdown files, browser bookmarks, and forum posts do not stay in sync with a living collection.

## Shift in Focus

Early thinking leaned toward "another mod manager." That framing was abandoned quickly. The Warhammer III ecosystem already has capable install and load-order utilities. The gap was different: **maintaining an understandable, searchable record of the collection as a system** — dependencies, categories, personal rationale, compatibility caveats, and health over time.

The project reframed around **knowledge management**:

- Document what is in the collection and how pieces relate.
- Import from formats players already use (markdown mod lists, Workshop IDs).
- Persist locally so the player owns their data.
- Surface health and risk without pretending to auto-fix the game.

## Open Source

Making the project open source followed naturally from the problem domain. Mod communities thrive on shared knowledge. A companion tool for organising that knowledge should be inspectable, forkable, and improvable by players who understand the same pain.

## Current Phase

**v0.0.1** (repository bootstrap) is complete. The project is in **v0.0.2 — Architecture complete**. [RFC-0002](../RFC/RFC-0002.md) established the Workspace-centred domain model, and [RFC-0003](../RFC/RFC-0003.md) established embedded SQLite storage and the initial Windows distribution target. Import and initial UI architecture remain to be approved before any code lands in `src/`. Milestones are described in [ROADMAP.md](../ROADMAP.md); vision and scope are captured in [RFC-0001](../RFC/RFC-0001.md) and [README.md](../README.md).

## Looking Ahead

Future history will be recorded here at major milestones: first import, first persisted collection, first public release. Contributors are welcome to add dated entries when those events occur.
