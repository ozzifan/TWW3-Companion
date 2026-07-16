# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Initial repository structure and documentation bootstrap (v0.0.1)
- README as living design document ([Decision-0001](decisions/Decision-0001.md))
- Offline-first architecture ([Decision-0002](decisions/Decision-0002.md))
- User-owned collection data ([Decision-0003](decisions/Decision-0003.md))
- Architectural decisions index with per-decision files under `decisions/`
- Architecture placeholder documents under `docs/architecture/`
- RFC-0001: Project Vision
- Contribution guidelines, agent roles, roadmap, and glossary
- GitHub issue and pull request templates
- MIT license and Contributor Covenant Code of Conduct
- `.gitignore` and `.editorconfig` for cross-platform development
- v0.0.2 architecture milestone and implementation gate ([ROADMAP.md](ROADMAP.md))
- v0.3 profiles milestone (profiles, multiple collections, campaign presets)
- `schemas/` directory for future JSON schemas
- Repository folder README files (`examples/`, `schemas/`, `tests/`)
- Non-affiliation disclaimer in [README.md](README.md)
- RFC-0002 and [Decision-0004](decisions/Decision-0004.md) establishing the Workspace-centred domain model
- RFC-0003 and [Decision-0005](decisions/Decision-0005.md) establishing embedded SQLite storage, lossless JSON export, and Windows distribution
- RFC-0004 and [Decision-0006](decisions/Decision-0006.md) establishing deterministic staged import for informal Markdown and Workshop IDs

### Changed

- [AGENTS.md](AGENTS.md) — role-first headings; Product Owner wording for single-maintainer reality ([Issue #2](https://github.com/ozzifan/TWW3-Companion/issues/2))
- [ROADMAP.md](ROADMAP.md) — v0.1 renamed to first working prototype; v0.0.1 marked complete
- [.gitignore](.gitignore) — limited to Git, OS, editors, Node, and Python patterns
- [CONTRIBUTING.md](CONTRIBUTING.md) — GitHub Issues and Discussions contact paths
- [docs/architecture/data-model.md](docs/architecture/data-model.md) — repository folders clarified; no in-repo user storage
- Architecture, glossary, roadmap, and project history aligned with the accepted Workspace-centred domain model
- README, roadmap, and architecture documents aligned with the accepted storage architecture and initial Windows target
- Architecture, roadmap, project history, and examples guidance aligned with the accepted import architecture

### Removed

- Repository `data/` directory
