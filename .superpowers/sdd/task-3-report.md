# Task 3: Markdown preview and apply report

## Status

Completed and reviewed.

## Delivered scope

- Added `MarkdownImportService` as the application-layer preview/apply entry point.
- Preview always returns a non-applied copy of the parsed Markdown result.
- Confirmed apply validates every candidate before it marks the result as applied.
- Name-only candidates are rejected with `ImportValidationException`, keeping them pending for future explicit resolution rather than applying them implicitly.
- Added focused tests for non-writing preview and validation failure.

## Test-first evidence

The required focused test command initially failed to compile because `MarkdownImportService` and `ImportValidationException` did not exist. After the minimal service contract was added, the same command passed 2/2 tests.

## Verification

- Focused import behavior:
  `dotnet test tests/Tww3Companion.Application.Tests --filter "MarkdownImport_preview_does_not_write_until_confirmed|MarkdownImport_rolls_back_when_validation_fails" -v normal`
  Result: 2 passed, 0 warnings, 0 errors.
- Full application test project:
  `dotnet test tests/Tww3Companion.Application.Tests -v normal`
  Result: 20 passed, 0 warnings, 0 errors.
- `git diff --check` completed without whitespace errors.

## Review

Independent review found no functional issue in the preview/apply contract. It identified a pre-existing unrelated rewrite to `task-2-report.md`; that file was preserved and excluded from this Task 3 commit.

## Concern

The current baseline contains no domain import planner or persistence applier. Accordingly, `Applied` represents completion of the validated application-layer handoff, not a SQLite write; a later import slice must connect this contract to RFC-0003's atomic transaction boundary.

## Commit

`feat: wire markdown import through preview and apply`
