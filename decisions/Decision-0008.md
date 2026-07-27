# Decision-0008: Design Scale Constraint for Agent Roles

**Status:** Accepted
**Date:** 2026-07-27

## Decision

Agent instructions state the project's operating scale as fact rather than relying on general simplicity preferences. TWW3 Companion has one maintainer. There is no team, no auditor, no compliance or regulatory requirement, and no on-call rotation.

The constraint governs design, not only code. Agents do not propose roles, policy layers, audit machinery, or configurability that a single maintainer will never exercise, and they state the operating cost of any mechanism they add. If a mechanism only pays off at team or enterprise scale, they say so and offer the smaller alternative.

Because the project is open source and may gain real users, product quality, correctness, and accessibility remain full requirements. The constraint applies to process and governance machinery only.

This is recorded as rule 3 under Hard rules for agents in [AGENTS.md](../AGENTS.md), and mirrored in the AI Dev Orchestrator's `ARCH` and `REV` role instructions.

## Rationale

- Existing agent instructions already required simplicity and did not prevent enterprise-scale design. Those rules were scoped to code — minimum lines, surgical edits, matching surrounding file style — and did not govern proportionality of system design.
- A stated preference can be reasoned around. "Do not add unrequested flexibility" permits a delegation policy to be defended as a correctness requirement for a multi-actor system. A stated fact removes the premise rather than arguing with the conclusion.
- The Architecture Partner role is held by a general-purpose model whose default output targets organisations with teams and auditors. The constraint must be supplied as input because it cannot be inferred from the codebase.
- Recording this as a Decision rather than only a role-configuration change keeps it visible to future contributors and to agents that read repository governance before starting work.

## Consequences

- Proposals that add governance machinery must justify their operating cost at single-maintainer scale or offer a smaller alternative.
- Review findings that only matter at team or enterprise scale — missing audit logging, retry policy, configurability — are not defects unless the approved plan called for them.
- User-facing quality requirements are unaffected. The accessibility, keyboard operation, High Contrast, text scaling, and cancellation contracts from [Decision-0007](Decision-0007.md) remain release requirements; this Decision does not license lower product quality.
- Machinery built before this Decision is left in place. The constraint applies to new design work rather than triggering retrospective simplification.
- If the project gains additional maintainers, this Decision requires revision rather than silent drift. Product Owner transfer governance in [AGENTS.md](../AGENTS.md) is the trigger to revisit it.
