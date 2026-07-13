# Decision-0003: User Owns Their Data

**Status:** Accepted  
**Date:** 2026-07-13

## Decision

**The user owns their collection data.** TWW3 Companion stores collections locally under the user's control. Formats should be human-inspectable where practical, versioned for migration, and exportable without vendor lock-in. The application does not upload collections to project-operated servers by default, and does not require a cloud account.

## Rationale

- Mod lists, notes, and compatibility knowledge are personal creative and organisational work; players should be able to back up, diff, move, and archive that data freely.
- Trust in an open-source companion depends on transparency — users must be able to read and edit their files outside the app if needed.
- Avoiding mandatory cloud sync reduces cost, privacy risk, and operational burden on maintainers.

## Consequences

- **Positive:** Portable collections across machines; git-friendly exports become possible; aligns with open-source values.
- **Positive:** Clear boundary: we document mods, we do not claim custody of player data.
- **Negative:** Sync and collaboration are the user's problem unless a future optional feature is RFC-approved.
- **Process:** Persistence and export format changes require documentation in [docs/architecture/data-model.md](../docs/architecture/data-model.md) and an entry in [CHANGELOG.md](../CHANGELOG.md). Breaking changes need a migration path.
