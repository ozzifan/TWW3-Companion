# Schemas

Versioned JSON schemas for documented transfer formats live here.

## Shipped formats

| Schema | Format identifier | Scope |
|--------|-------------------|-------|
| [workspace-export-v1.schema.json](workspace-export-v1.schema.json) | `workspace-export-v1` | Complete lossless full-Workspace export for v0.1 backup and restore |

`workspace-export-v1` is the shipped complete v0.1 Workspace format. It captures the Workspace identity, every Mod and Collection, Source References, and ordered Collection Memberships with stable UUIDs.

Single-Collection snapshot schema and behaviour remain deferred.

User Workspace data is **not** stored in this repository.
