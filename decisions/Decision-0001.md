# Decision-0001: README is a Living Design Document

**Status:** Accepted  
**Date:** 2026-07-13

## Decision

[README.md](../README.md) is treated as a **living design document**, not a static marketing page. It describes project vision, goals, non-goals, philosophy, roadmap summary, and contribution expectations. When those aspects change, the README is updated in the same change set (or immediately after) as the work that motivated the change.

## Rationale

- New contributors and users often read only the README. Keeping vision and scope there reduces drift between "what we say" and "what we build."
- A single, prominent document lowers the barrier to understanding *why* the project exists, which matters for an open-source tool with a deliberately narrow scope.
- Treating the README as authoritative design text avoids scattering high-level intent across issues and chat logs.

## Consequences

- **Positive:** Faster onboarding; clearer boundaries (especially non-goals); roadmap and philosophy stay visible.
- **Positive:** Pull request reviewers can ask "does this need a README update?" for user-facing or scope-affecting changes.
- **Negative:** The README will grow over time; care is needed to keep it scannable (link out to `docs/` for depth).
- **Process:** All major architectural decisions recorded in [decisions/](../decisions/) or RFCs should explicitly consider whether [README.md](../README.md) also requires updating. If a decision changes goals, non-goals, philosophy, or roadmap, the README must be updated.
