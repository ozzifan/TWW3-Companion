# Markdown Import Slice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the first v0.1 import slice for Markdown notes so pasted or file-based Markdown can be parsed into import candidates and previewed with diagnostics, with an explicit confirmation gate for the validated handoff.

**Architecture:** One Markdown adapter translates note text into the shared import candidate model. The adapter stays pure: it parses, normalizes, and records source locations, while the existing application layer handles preview and validation for the handoff. The actual workspace write/atomic transaction boundary is deferred to a later slice. Workshop ID extraction is limited to exact source references inside Markdown; Steam Workshop-only import behavior remains a separate slice.

**Tech Stack:** .NET 10, existing TWW3 Companion domain/application layers, existing import pipeline and test projects, Markdown parsing utilities already present in the repo if any, xUnit.

## Global Constraints

- The Markdown adapter reads one Markdown document and produces a normalized import candidate set plus diagnostics.
- It must recognize headings, bullet lists, pasted Workshop links and raw Workshop IDs, and free-form prose.
- It must not access persistence, make network calls, infer dependencies/compatibility/ordering from prose, delete or replace existing data, auto-apply unresolved names, or treat lossless Workspace JSON as an import input.
- Imports are additive-only. Omission never removes a Mod or Membership.
- Scalar conflicts require an explicit user choice.
- Failed validation rolls back the entire confirmed import.
- The adapter must preserve source locations and diagnostics.

---

### Task 1: Locate the import entry points and define the Markdown adapter boundary

**Files:**
- Create: `src/Tww3Companion.Application/Importing/MarkdownImportAdapter.cs`
- Create: `src/Tww3Companion.Application/Importing/MarkdownImportResult.cs`
- Create: `src/Tww3Companion.Application/Importing/MarkdownImportCandidate.cs`
- Create: `src/Tww3Companion.Application/Importing/MarkdownImportDiagnostic.cs`
- Modify: `tests/Tww3Companion.Application.Tests/Importing/MarkdownImportAdapterTests.cs`

**Interfaces:**
- Consumes: the existing workspace/import conventions in `src/Tww3Companion.Application/Common/OperationResult.cs` and the import pipeline entry points used by the application layer
- Produces: a Markdown-specific adapter contract that can translate `string` Markdown input into candidates plus diagnostics

- [ ] **Step 1: Write the failing test**

Add a focused application test that describes the public Markdown import contract:

```csharp
[Fact]
public void ParseMarkdown_returns_candidates_and_diagnostics_for_input_text()
{
    var result = MarkdownImportAdapter.Parse("""
# My Collection

- https://steamcommunity.com/sharedfiles/filedetails/?id=123456789
- Keep these notes with the collection
""");

    Assert.NotEmpty(result.Candidates);
    Assert.Contains(result.Diagnostics, d => d.SourceLine == 1);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/Tww3Companion.Application.Tests --filter ParseMarkdown_returns_candidates_and_diagnostics_for_input_text -v normal`

Expected: fail because `MarkdownImportAdapter` does not exist yet.

- [ ] **Step 3: Add the minimal adapter boundary**

Implement the smallest public contract needed by the rest of the slice. Keep the adapter pure and input-driven:

```csharp
public static class MarkdownImportAdapter
{
    public static MarkdownImportResult Parse(string markdown)
    {
        throw new NotImplementedException();
    }
}

public sealed record MarkdownImportResult(
    IReadOnlyList<ImportCandidate> Candidates,
    IReadOnlyList<ImportDiagnostic> Diagnostics);
```

If the repository already has a candidate/diagnostic shape, reuse it rather than introducing parallel types.

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/Tww3Companion.Application.Tests --filter ParseMarkdown_returns_candidates_and_diagnostics_for_input_text -v normal`

Expected: pass after the minimal adapter exists.

- [ ] **Step 5: Commit**

```bash
git add src/Tww3Companion.Application/Importing tests/Tww3Companion.Application.Tests/Importing
git commit -m "feat: add markdown import adapter boundary"
```

### Task 2: Parse headings, bullets, prose, and Workshop references from Markdown

**Files:**
- Modify: `src/Tww3Companion.Application/Importing/MarkdownImportAdapter.cs`
- Modify: `src/Tww3Companion.Application/Importing/MarkdownImportResult.cs`
- Modify: `src/Tww3Companion.Application/Importing/MarkdownImportCandidate.cs`
- Modify: `src/Tww3Companion.Application/Importing/MarkdownImportDiagnostic.cs`
- Modify: `tests/Tww3Companion.Application.Tests/Importing/MarkdownImportAdapterTests.cs`

**Interfaces:**
- Consumes: `MarkdownImportAdapter.Parse(string markdown)` and the result/diagnostic types from Task 1
- Produces: parsed candidates where headings become category hints, bullets become candidate entries, Workshop IDs/URLs become exact source references, and prose is preserved as notes

- [ ] **Step 1: Write the failing tests**

Add tests that lock the Markdown behavior to the spec:

```csharp
[Fact]
public void ParseMarkdown_treats_heading_as_category_hint()
{
    var result = MarkdownImportAdapter.Parse("""
# Skaven

- Clanrats
""");

    Assert.Contains(result.Candidates, c => c.Kind == ImportCandidateKind.CategoryHint && c.Value == "Skaven");
}

[Fact]
public void ParseMarkdown_extracts_workshop_id_from_link()
{
    var result = MarkdownImportAdapter.Parse("""
- https://steamcommunity.com/sharedfiles/filedetails/?id=123456789
""");

    Assert.Contains(result.Candidates, c => c.SourceReference?.WorkshopId == "123456789");
}

[Fact]
public void ParseMarkdown_preserves_free_form_prose_as_notes()
{
    var result = MarkdownImportAdapter.Parse("""
This list is mostly for late-game testing.
""");

    Assert.Contains(result.Candidates, c => c.Kind == ImportCandidateKind.Note && c.Text.Contains("late-game testing"));
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Tww3Companion.Application.Tests --filter "ParseMarkdown_treats_heading_as_category_hint|ParseMarkdown_extracts_workshop_id_from_link|ParseMarkdown_preserves_free_form_prose_as_notes" -v normal`

Expected: fail until the parsing logic exists.

- [ ] **Step 3: Implement the parser minimally**

Teach the adapter to:

```csharp
- read line by line;
- detect heading lines that begin with "#";
- detect bullet lines that begin with "-" or "*";
- extract Workshop IDs from Steam Workshop URLs and bare numeric IDs;
- preserve unstructured text as note candidates;
- attach source line numbers to diagnostics and source references.
```

Keep the parser conservative: if a line does not clearly match one of the supported shapes, preserve it as a note rather than inferring structure.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Tww3Companion.Application.Tests --filter "ParseMarkdown_treats_heading_as_category_hint|ParseMarkdown_extracts_workshop_id_from_link|ParseMarkdown_preserves_free_form_prose_as_notes" -v normal`

Expected: pass with the minimal parser.

- [ ] **Step 5: Commit**

```bash
git add src/Tww3Companion.Application/Importing tests/Tww3Companion.Application.Tests/Importing
git commit -m "feat: parse markdown import candidates"
```

### Task 3: Wire Markdown parse results into the preview and validated handoff path

**Files:**
- Modify: `src/Tww3Companion.Application/Importing/MarkdownImportAdapter.cs`
- Modify: `src/Tww3Companion.Application/Importing/MarkdownImportService.cs`
- Modify: `src/Tww3Companion.Application/Importing/MarkdownImportResult.cs`
- Modify: `src/Tww3Companion.Application/Importing/MarkdownImportCandidate.cs`
- Modify: `src/Tww3Companion.Application/Importing/MarkdownImportDiagnostic.cs`
- Modify: `tests/Tww3Companion.Application.Tests/Importing/MarkdownImportServiceTests.cs`
- Modify: `tests/Tww3Companion.Infrastructure.Tests/*` only if the apply path currently lives there

**Interfaces:**
- Consumes: `MarkdownImportResult` from Task 2
- Produces: preview-ready import candidates that respect additive-only behavior and explicit conflict resolution, with validation gating the handoff

- [ ] **Step 1: Write the failing tests**

Add tests that prove the adapter output reaches the existing preview/handoff path without implicit writes:

```csharp
[Fact]
public async Task MarkdownImport_preview_does_not_write_until_confirmed()
{
    var result = MarkdownImportAdapter.Parse("""
# Test Collection

- https://steamcommunity.com/sharedfiles/filedetails/?id=123456789
""");

    var preview = await MarkdownImportService.BuildPreviewAsync(result);

    Assert.NotNull(preview);
    Assert.False(preview.Applied);
}

[Fact]
public async Task MarkdownImport_rolls_back_when_validation_fails()
{
    var result = MarkdownImportAdapter.Parse("""
# Test Collection

- invalid candidate text that cannot validate
""");

    await Assert.ThrowsAsync<ImportValidationException>(() => MarkdownImportService.ApplyAsync(result, confirm: true));
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Tww3Companion.Application.Tests --filter "MarkdownImport_preview_does_not_write_until_confirmed|MarkdownImport_rolls_back_when_validation_fails" -v normal`

Expected: fail until the Markdown result is connected to preview/handoff behavior.

- [ ] **Step 3: Connect the result to the existing preview/handoff pipeline**

Keep the behavior additive-only:

```csharp
- exact source references may auto-match;
- unresolved names remain pending until the user resolves or skips them;
- blank fields may be enriched later during preview;
- scalar conflicts require an explicit choice;
- apply remains a validated handoff;
- validation failure rejects the handoff.
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/Tww3Companion.Application.Tests --filter "MarkdownImport_preview_does_not_write_until_confirmed|MarkdownImport_rolls_back_when_validation_fails" -v normal`

Expected: pass with the integrated pipeline.

- [ ] **Step 5: Commit**

```bash
git add src/Tww3Companion.Application/Importing tests/Tww3Companion.Application.Tests/Importing tests/Tww3Companion.Infrastructure.Tests
git commit -m "feat: wire markdown import through preview and handoff"
```

### Task 4: Verify the full slice with format, build, tests, and diff check

**Files:**
- None expected unless the prior tasks expose a small fix

**Interfaces:**
- Consumes: the completed Markdown import slice
- Produces: a verified branch ready for review

- [ ] **Step 1: Run the full verification commands**

Run:

```powershell
dotnet format Tww3Companion.sln --verify-no-changes
dotnet build Tww3Companion.sln -c Release --no-restore
dotnet test Tww3Companion.sln -c Release --no-build
git diff --check
```

Expected: all commands succeed.

- [ ] **Step 2: Fix any small verification issues**

If formatting or test failures appear, make the smallest code change needed, then rerun the same verification commands.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat: complete markdown import slice"
```
