# Phase 6 attempts persistence, work-packet exclusion, and ARCH CLI proof

Fixture architecture plan for work item `ARCH-TWW3-0004` (orchestrator issue 4, task `ORCH-TWW3-0004`).

## Goal

Prove the AI Dev Orchestrator can:

1. persist worker attempt history across retries,
2. keep `.orchestrator-work-packet.json` out of repository pull requests, and
3. drive the ARCH planning worker through the live Cursor CLI adapter.

## Background

Phase 5 (`ARCH-TWW3-0003` / `IMPL-TWW3-0003`) verified live Cursor IMPL and Claude REV adapters with a documentation-only proof artifact at `docs/orchestrator/IMPL-TWW3-0003-phase-5-cli-proof.md`. Phase 6 extends orchestrator verification to planning retries, checkout hygiene, and the ARCH role.

## Scope

### This plan (ARCH phase)

- Add or update this plan at `docs/superpowers/plans/2026-07-22-phase-6-attempts-work-packet-arch-cli.md`
- Keep the ARCH pull request documentation-only (no product or application code)
- Reference work item `ARCH-TWW3-0004` and orchestrator issue 4
- Produce the plan through the Cursor ARCH adapter in a live orchestrator run

### Implementation phase (`IMPL-TWW3-0004`)

- Create `docs/orchestrator/IMPL-TWW3-0004-phase-6-attempts-work-packet-arch-cli.md`
- Follow the proof-artifact shape used in Phase 5 (`docs/orchestrator/IMPL-TWW3-0003-phase-5-cli-proof.md`)
- Record that adapters were **Cursor (ARCH + IMPL)** and **Claude (REV)**, matching the live Phase 6 run
- Document all three Phase 6 verification proofs (see Approach)
- Include orchestrator verification marker: `Phase 6 attempts persistence, work-packet exclusion, and ARCH CLI proof complete`

## Approach

### 1. Attempts persistence

Run at least one deliberate worker failure and retry during the Phase 6 orchestrator run. Confirm:

- the orchestrator records the failed attempt in persisted state, and
- the retried worker resumes from that state without losing task context or work-item identity.

Record the observed attempt count and retry outcome in the IMPL proof artifact.

### 2. Work-packet exclusion

Confirm `.orchestrator-work-packet.json`:

- remains present in the checkout for worker instructions during ARCH, IMPL, and REV, but
- is absent from the ARCH and IMPL pull request file lists and diffs.

The IMPL proof artifact should note that neither PR included the work packet.

### 3. Cursor ARCH CLI

Produce this plan through the Cursor ARCH adapter in a live orchestrator run, then carry the approved plan forward to the IMPL worker unchanged. The IMPL proof artifact should reference this plan path and state that ARCH ran through Cursor CLI.

## IMPL proof artifact outline

The implementation worker should create `docs/orchestrator/IMPL-TWW3-0004-phase-6-attempts-work-packet-arch-cli.md` with at least:

```markdown
# Phase 6 attempts persistence, work-packet exclusion, and ARCH CLI proof

Documentation-only artifact for work item `IMPL-TWW3-0004`, proving the AI Dev Orchestrator Phase 6 behaviours for TWW3 Companion.

## Adapters

- **ARCH:** Cursor
- **IMPL:** Cursor
- **REV:** Claude

## Approved plan

- `docs/superpowers/plans/2026-07-22-phase-6-attempts-work-packet-arch-cli.md`

## Scope

Proof markdown only. No product or application code changes in this validation pull request.

## Verification

### Attempts persistence

[Brief record of deliberate failure, retry, and persisted attempt history.]

### Work-packet exclusion

[Confirm `.orchestrator-work-packet.json` was not in ARCH or IMPL PR diffs.]

### ARCH CLI

[Confirm this plan was produced by the Cursor ARCH adapter in a live run.]

- Opened by the orchestrator implementation worker as a draft pull request
- Reviewed through the Claude REV adapter
- Orchestrator verification marker: Phase 6 attempts persistence, work-packet exclusion, and ARCH CLI proof complete
```

## Out of scope

- Feature code, CI workflow edits, dependency changes
- Adding `.orchestrator-work-packet.json` to the repository (orchestrator-local checkout state only)
- Merging the implementation pull request (orchestrator stops at `ready_to_merge`)

## Acceptance

- This plan exists under `docs/superpowers/plans/`
- The ARCH pull request diff is documentation-only
- The ARCH pull request does not include `.orchestrator-work-packet.json`
- After implementation, `docs/orchestrator/IMPL-TWW3-0004-phase-6-attempts-work-packet-arch-cli.md` exists and records all three Phase 6 proofs
- Neither ARCH nor IMPL pull requests include `.orchestrator-work-packet.json`
