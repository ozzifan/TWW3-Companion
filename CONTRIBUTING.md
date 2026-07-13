# Contributing to TWW3 Companion

Thank you for your interest in contributing. This project values clear communication, small changes, and documentation that keeps pace with the code.

---

## Before You Start

1. Read [README.md](README.md) for vision, goals, and non-goals.
2. Skim [docs/architecture/](docs/architecture/) for the current design direction.
3. Review [AGENTS.md](AGENTS.md) if you use AI assistants — architecture is approved before implementation.

---

## Issue-First Workflow

**Open an issue before starting significant work.**

| Change type | Issue template |
|-------------|----------------|
| Bug | [Bug report](.github/ISSUE_TEMPLATE/bug_report.md) |
| Feature | [Feature request](.github/ISSUE_TEMPLATE/feature_request.md) |
| Large design change | [RFC](.github/ISSUE_TEMPLATE/rfc.md) |

### Guidelines

- Search existing issues to avoid duplicates.
- Describe the problem, not only your preferred solution.
- For bugs: include steps to reproduce, expected vs actual behaviour, and environment details when relevant.
- Link issues in pull requests (`Fixes #123`).

Trivial fixes (typos, obvious one-line corrections) may skip a dedicated issue if the PR description is sufficient.

---

## RFC Process

Use an RFC when a change:

- Affects architecture, data formats, or public APIs
- Introduces a new major feature or changes project scope
- Has multiple valid approaches with meaningful trade-offs
- Needs consensus before implementation

### Steps

1. Open an issue using the RFC template, or add a document under [RFC/](RFC/) (e.g. `RFC-0002.md`).
2. Describe problem, proposed solution, alternatives, and impact — **no implementation detail required** at first, but enough for reviewers to decide.
3. Discuss in the issue or PR until the Product Owner approves or requests revision.
4. On acceptance: update [docs/architecture/](docs/architecture/), add a [decisions/](decisions/) record (and [DECISIONS.md](DECISIONS.md) index row), or [README.md](README.md) as needed, then implement.

[RFC-0001](RFC/RFC-0001.md) is the baseline vision document.

---

## Pull Requests

### Principles

- **Small pull requests** — easier to review, test, and revert. Prefer a series of focused PRs over one large dump.
- **One concern per PR** — mix documentation-only and behaviour changes only when they are inseparable.
- **Tests with behaviour** — when the test stack exists, include tests for new logic.
- **Docs with features** — update user-facing or architectural docs in the same PR when behaviour or scope changes.

### Checklist

Use the [pull request template](.github/pull_request_template.md).

### Review

Maintainers may request changes, ask for an RFC, or suggest splitting the PR. Patient, constructive review is expected from everyone.

---

## Coding Standards

*Detailed language-specific standards will be added when the implementation stack is chosen. Until then:*

- Match existing style in the files you touch.
- Prefer clear names over abbreviations.
- Keep functions and modules focused; avoid premature abstraction.
- Handle errors explicitly; do not swallow failures silently.
- No secrets, API keys, or personal paths in commits.
- Follow [.editorconfig](.editorconfig) (UTF-8, LF, spaces, trim trailing whitespace).

When stack-specific formatters and linters are introduced, run them before pushing.

---

## Documentation Expectations

Documentation is part of the deliverable, not an afterthought.

| Change | Update |
|--------|--------|
| New feature | Architecture or user docs as appropriate; [CHANGELOG.md](CHANGELOG.md) under Unreleased |
| Scope / vision shift | [README.md](README.md), possibly [RFC/](RFC/) or [decisions/](decisions/) |
| New domain term | [docs/glossary.md](docs/glossary.md) |
| Breaking data format | [docs/architecture/data-model.md](docs/architecture/data-model.md), [CHANGELOG.md](CHANGELOG.md) |

Write in clear Markdown. Use British English spelling in project docs for consistency (e.g. *organise*, *behaviour*) unless a file already establishes a different convention.

---

## Code of Conduct

This project adopts the [Contributor Covenant](CODE_OF_CONDUCT.md). By participating, you agree to uphold it.

---

## Questions

Open a [GitHub Discussion](https://github.com/) *(link to be added when repository is published)* or an issue labelled `question` if you are unsure how to proceed.

We appreciate thoughtful contributions of all sizes.
