# TWW3 Companion

**Organise, understand, and maintain large Total War: Warhammer III mod collections.**

TWW3 Companion is an open-source desktop application for players who run complex mod lists. It focuses on **knowledge management** — helping you document what you have, why it is there, how mods relate to each other, and whether your collection is healthy — rather than replacing the game's mod launcher or workshop tools.

---

## Vision

Players who invest dozens or hundreds of hours curating Warhammer III mod collections deserve better than scattered spreadsheets, forum bookmarks, and half-remembered load-order notes. TWW3 Companion aims to be the single place where collection knowledge lives: structured, searchable, portable, and honest about what the tool can and cannot do.

---

## Why This Exists

Total War: Warhammer III modding is rich and active. A serious collection might include hundreds of workshop items across factions, UI overhauls, balance packs, and compatibility patches. Keeping track of:

- what is installed and in what order,
- which mods depend on which frameworks,
- known incompatibilities and workarounds,
- personal notes and categorisation,

…quickly outgrows ad-hoc methods. TWW3 Companion grew from exactly that problem: reviewing a large personal collection and realising the real need was not another mod manager, but a **companion for understanding and maintaining** what you already use.

---

## Goals

- **Import** mod lists from common sources (markdown notes, Steam Workshop IDs).
- **Persist** collections locally with a clear, portable data model.
- **Organise** mods with categories, tags, and free-form notes.
- **Search** across your collection quickly.
- **Track** dependencies, compatibility notes, and a collection **health score**.
- **Export** and share collection knowledge in open, documented formats.
- Remain **offline-first** and respectful of player privacy.

---

## Non-Goals

TWW3 Companion is **not**:

- A replacement for the Steam Workshop, the game's mod launcher, or tools like RPFM, Kaedrin's Mod Manager, or WH3 Mod Manager.
- A load-order solver or automatic conflict resolver.
- A mod downloader, installer, or patch generator.
- A multiplayer or anti-cheat bypass tool.
- A cloud service that stores your collection on our servers by default.

We document and organise. We do not modify game files or automate risky changes to your install.

---

## Philosophy

1. **Knowledge over management** — The app helps you *understand* your collection; it does not pretend to manage the game for you.
2. **Player-owned data** — Your collection files belong to you. Formats should be human-readable where practical.
3. **Honest scope** — Features are added when they serve documentation and organisation, not when they duplicate existing specialised tools.
4. **Small, reviewable changes** — Issues and RFCs before large work; small pull requests preferred.
5. **Living design** — [README.md](README.md) and [docs/](docs/) evolve with the project. Major decisions are recorded in [DECISIONS.md](DECISIONS.md).

---

## Roadmap

High-level milestones are maintained in [ROADMAP.md](ROADMAP.md).

| Version | Focus |
|---------|--------|
| **v0.0.1** | Repository bootstrap, documentation, standards |
| **v0.1** | Markdown importer, Workshop ID importer, collection persistence |
| **v0.2** | Search, tags, categories, notes |
| **v0.5** | Dependency tracking, compatibility notes, health score |
| **v1.0** | Stable collection manager suitable for everyday use |

See [CHANGELOG.md](CHANGELOG.md) for release history.

---

## Architecture

Design documents live under [docs/architecture/](docs/architecture/):

- [Overview](docs/architecture/overview.md)
- [Data model](docs/architecture/data-model.md)
- [UI](docs/architecture/ui.md)
- [Import & export](docs/architecture/import-export.md)
- [Future considerations](docs/architecture/future.md)

Project vision and problem framing: [RFC/RFC-0001.md](RFC/RFC-0001.md).

---

## Contributing

We welcome contributions. Please read [CONTRIBUTING.md](CONTRIBUTING.md) before opening a pull request.

- **Issues first** for bugs and feature ideas.
- **RFCs** for significant design or architectural changes.
- **Small PRs** are easier to review and merge.

All participants are expected to follow our [Code of Conduct](CODE_OF_CONDUCT.md).

---

## Project Roles

Agent and human collaboration guidelines: [AGENTS.md](AGENTS.md).

---

## License

[MIT](LICENSE) — see [LICENSE](LICENSE) for full text.

---

## Glossary

Terms used throughout the project are defined in [docs/glossary.md](docs/glossary.md).
