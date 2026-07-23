# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Markdown import slice: staged preview/apply flow for informal markdown mod lists, including source parsing, candidate normalization, and user-confirmed import into a Workspace
- Steam import slice: Workshop ID and collection import flows with metadata enrichment, staged preview/apply, and import hardening for shared shell workflows

### Changed

- Import architecture progressed toward the shared import engine and target-context model used by the Desktop shell wiring slice

## [0.0.2] - 2026-07-19

### Added

- RFC-0002 and [Decision-0004](decisions/Decision-0004.md) establishing the Workspace-centred domain model
- RFC-0003 and [Decision-0005](decisions/Decision-0005.md) establishing embedded SQLite storage, lossless JSON export, and Windows distribution
- RFC-0004 and [Decision-0006](decisions/Decision-0006.md) establishing deterministic staged import for informal Markdown and Workshop IDs
- RFC-0005 and [Decision-0007](decisions/Decision-0007.md) establishing the C#/.NET, Avalonia, and MVVM user-interface architecture
- Workspace foundation slice completed with Home composition, startup wiring, smoke-test hooks, and publish guidance
- `docs/development.md` with exact build, test, publish, and smoke-test commands
- `.github/workflows/ci.yml` for restore, format, build, test, publish, and smoke verification
- `scripts/smoke-test-portable.ps1` for installed and portable smoke verification

### Changed

- [ROADMAP.md](ROADMAP.md) — v0.1 renamed to first working prototype; v0.0.1 marked complete
- `README.md`, `ROADMAP.md`, and `docs/project-history.md` aligned with the completed Workspace foundation slice and developer guide

## [0.0.1] - 2026-07-13

### Added

- Initial repository structure and documentation bootstrap
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
- `schemas/` directory for future JSON schemas
- Repository folder README files (`examples/`, `schemas/`, `tests/`)
- Non-affiliation disclaimer in [README.md](README.md)
