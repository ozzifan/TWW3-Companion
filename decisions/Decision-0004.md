# Decision-0004: Workspace-Centred Domain Model

**Status:** Accepted
**Date:** 2026-07-16

## Decision

TWW3 Companion uses a **Workspace** as its aggregate root. A Workspace owns one shared Mod Library and one or more Collections. Mods are defined once in the library; Collections reference them through Collection Memberships that hold collection-specific organisation and rationale.

Relationships may retain unresolved targets, compatibility knowledge is recorded as versioned observations with evidence, and imports distinguish user-authored, imported, and derived information.

The complete domain model, lifecycle rules, invariants, alternatives, and deferred decisions are defined in [RFC-0002](../RFC/RFC-0002.md).

## Rationale

- Shared Mod records prevent duplicated notes and identities across Collections.
- Collection Memberships preserve context that legitimately differs between play contexts.
- Source-neutral identities avoid coupling the domain to Steam Workshop.
- Unresolved targets and retained evidence preserve useful incomplete or conflicting knowledge.
- Explicit deletion and import rules protect user-authored data.

## Consequences

- Architecture, glossary, import/export, and UI documents use Workspace, Mod Library, Collection, and Collection Membership consistently.
- Storage design must preserve the ownership boundaries and invariants in RFC-0002 without assuming that a Workspace is one file.
- Importers match or create library Mods separately from creating Collection Memberships.
- Removing a Collection Membership is distinct from confirmed library deletion.
- Profile behaviour, persistence format, import grammar, and detailed UI remain deferred to later architecture work.
