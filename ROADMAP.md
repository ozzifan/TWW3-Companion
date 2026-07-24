# Roadmap

This document tracks planned milestones for TWW3 Companion. Dates are intentionally omitted until release cadence is established. For shipped changes, see [CHANGELOG.md](CHANGELOG.md).

**Implementation gate:** No application code in `src/` until **v0.0.2 — Architecture complete** is finished and the Product Owner has approved the relevant architecture. See [AGENTS.md](AGENTS.md).

---

## v0.0.1 — Repository bootstrap

**Status:** Complete

Establish the project foundation:

- Repository structure, documentation, and standards
- Architecture placeholders and glossary
- Contribution workflow, RFC process, and agent roles
- Issue and pull request templates
- MIT license and Contributor Covenant

No application code in this milestone.

---

## v0.0.2 — Architecture complete

**Status:** Complete

Implementation must not begin until the following are approved by the Product Owner:

- Core domain model approved ([RFC-0002](RFC/RFC-0002.md))
- Import architecture approved ([RFC-0004](RFC/RFC-0004.md))
- Storage architecture approved ([RFC-0003](RFC/RFC-0003.md))
- Initial UI architecture approved ([RFC-0005](RFC/RFC-0005.md))

No application code in this milestone.

---

## v0.1 — First working prototype

**Status:** In Progress

*Requires v0.0.2 approval.*

The initial release uses C#/.NET, Avalonia, and MVVM on Windows 10 or later x64 through a self-contained one-step installer and portable ZIP. Other platforms are deferred. The minimum supported display is 1280 × 720 at 100% Windows scaling, or enough effective space for a 1024 × 640 logical-pixel window at increased scaling.

v0.1 is delivered through reviewed vertical slices rather than one undivided implementation. The first slice is the [Workspace foundation](docs/superpowers/specs/2026-07-18-workspace-foundation-design.md): startup, Home, empty Workspace creation/opening, SQLite schema and migration infrastructure, and the milestone-bounded shell. Later v0.1 slices add the accepted import, complete Home actions, domain tables, JSON backup/restore, and packaging workflows.

Workspace foundation slice:

- Home composition and startup wiring complete
- Later v0.1 slices still add the accepted import, complete Home actions, domain tables, JSON backup/restore, and packaging workflows

- Markdown importer
- Workshop ID importer

Completed v0.1 slices:

- Workspace foundation
- Markdown importer
- Workshop ID importer
- Shared import engine
- Home and Workspace shell wiring to the shared engine
- Local Workspace catalog persistence

Success: a user can import a list into a Collection, persist its Workspace locally, and reload it reliably.

---

## v0.2 — Organisation and discovery

**Status:** Planned

Make collections usable day to day:

- **Search** — find mods by name, ID, tag, category, or note content
- **Tags** — flexible labelling
- **Categories** — structured grouping
- **Notes** — per-mod and collection-level annotations

Success: a user can organise and quickly locate mods in a large collection.

---

## v0.3 — Profiles

**Status:** Planned

- Profiles
- Per-Collection configurations
- Campaign presets

Success: a user can document multiple play configurations within their owning Collections without duplicating shared Mod knowledge.

---

## v0.5 — Relationships and health

**Status:** Planned

Surface collection quality and risk:

- **Dependency tracking** — record which mods require or recommend others
- **Compatibility notes** — document known conflicts and workarounds
- **Health score** — a summary indicator of collection completeness and known issues

Success: a user can assess whether a collection is likely to be coherent before launching the game.

---

## v1.0 — Stable collection manager

**Status:** Planned

A **stable collection manager suitable for everyday use**:

- Reliable import/export round-trips
- Polished UI for browse, search, and edit workflows
- Documented data formats and migration path from earlier versions
- Clear non-goals enforced in product behaviour (no mod installation or load-order automation)

Success: players trust TWW3 Companion as their primary place to document and maintain Warhammer III mod collections.

---

## Beyond v1.0

Ideas under consideration (not committed) are noted in [docs/architecture/future.md](docs/architecture/future.md). Significant new directions should go through the RFC process.
