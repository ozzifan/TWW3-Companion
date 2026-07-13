# Agent and Contributor Roles

TWW3 Companion is developed collaboratively by humans and AI assistants. This document defines **responsibilities** for each role so work stays aligned with project vision and architecture.

> The named assistants reflect the workflow used during the project's creation. Future contributors may fulfil these roles using different tools or entirely human contributors provided the responsibilities assigned to each role are respected.

This document describes responsibilities rather than mandating particular products.

---

## Product Owner

**Accountable human** for project direction.

The Product Owner role is initially held by the repository founder and may be transferred through normal project governance.

### Responsibilities

- Own vision, priorities, and release milestones
- Approve or reject RFCs and major architectural changes
- Resolve disputes on scope, goals, and non-goals
- Accept or defer work that affects [README.md](README.md), [ROADMAP.md](ROADMAP.md), and [DECISIONS.md](DECISIONS.md)
- Ensure the project remains true to its philosophy (knowledge management, not mod management)

### Authority

Final say on what ships and what is out of scope.

---

## ChatGPT

**Design and documentation partner.**

### Responsibilities

- Draft and refine high-level design: vision, problem statements, user-facing copy
- Propose README structure, glossary entries, and roadmap language
- Help articulate goals, non-goals, and philosophy before implementation
- Review documentation for clarity and consistency
- Suggest RFC outlines when new features are proposed

### Boundaries

- Does not merge code or commit without human review unless explicitly delegated
- Implementation details defer to approved architecture docs and RFCs

---

## Cursor

**Implementation environment and coding agent host.**

### Responsibilities

- Apply approved designs in the repository (code, tests, config) once architecture is signed off
- Follow [CONTRIBUTING.md](CONTRIBUTING.md): issue-first workflow, small PRs, coding standards
- Update documentation alongside code when behaviour or scope changes
- Run local tooling, linters, and tests as the stack is introduced
- Propose concrete file and module structure that fits [docs/architecture/](docs/architecture/)

### Boundaries

- Does not redefine product vision or non-goals without Product Owner approval
- Does not implement features that lack an approved issue or RFC (as appropriate to change size)

---

## Claude

**Review, analysis, and long-context reasoning.**

### Responsibilities

- Deep review of RFCs, architecture docs, and large diffs
- Analyse trade-offs for data model, import/export, and UI decisions
- Help maintain consistency across documentation sets
- Summarise workshop feedback, issue threads, and decision history
- Support security and privacy review as the application handles local user data

### Boundaries

- Recommendations are advisory until Product Owner approves
- Does not bypass the RFC process for architectural changes

---

## Architecture Before Implementation

**No feature implementation begins until architecture for that feature is approved.**

The [ROADMAP.md](ROADMAP.md) milestone **v0.0.2 — Architecture complete** must be finished before code lands in `src/` for v0.1 features.

Approval means one or more of:

1. Existing [docs/architecture/](docs/architecture/) documents cover the feature and remain current, **or**
2. An RFC (e.g. under [RFC/](RFC/)) has been reviewed and accepted by the Product Owner, **or**
3. A recorded decision in [decisions/](decisions/) explicitly authorises the approach.

Agents and contributors should:

- Read [README.md](README.md), relevant architecture docs, and [RFC/RFC-0001.md](RFC/RFC-0001.md) before substantial work
- Open or update an RFC when design is unclear or cross-cutting
- Update architecture docs when implementation reveals a better model — via PR, not silent drift

---

## Workflow Summary

| Step | Owner |
|------|--------|
| Vision / scope question | Product Owner |
| Design draft | ChatGPT, humans, or RFC author |
| Architecture approval | Product Owner |
| Implementation | Cursor (or human developers) |
| Deep review | Claude, human reviewers |
| Merge | Human with repository access |

---

## Related Documents

- [CONTRIBUTING.md](CONTRIBUTING.md) — issue and PR workflow
- [RFC/RFC-0001.md](RFC/RFC-0001.md) — project vision
- [DECISIONS.md](DECISIONS.md) — decisions index; [decisions/](decisions/) — individual records
