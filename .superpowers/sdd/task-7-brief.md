### Task 7: Align documentation and verify the complete vertical slice

**Files:**
- Modify: `CHANGELOG.md`
- Modify: `ROADMAP.md`
- Modify: `docs/project-history.md`
- Modify: `docs/architecture/import-export.md`
- Modify: `docs/architecture/ui.md`
- Modify: `docs/development.md`
- Modify: `RFC/RFC-0005.md` only if its maintained flow text is updated directly; do not change accepted decision semantics
- Test: all solution projects

**Interfaces:**
- Consumes: complete Tasks 1–6
- Produces: aligned maintained documentation and release-ready verification evidence

- [ ] **Step 1: Update maintained architecture**

Document:

- Source → Destination → Preview/Resolve → Confirm/Apply;
- library-only, existing-Collection, and new-Collection destination forms;
- source/destination independence;
- Steam metadata disclosure and explicit request;
- warning count as “warnings remaining”;
- no persistence before Apply;
- the prior mandatory-Collection import rule is superseded.

Keep RFC-0002's domain semantics and RFC-0004's additive/no-synchronisation rules unchanged.

- [ ] **Step 2: Update milestone documents**

Add an Unreleased changelog entry for the complete import UI. Mark the v0.1 import workflow complete in `ROADMAP.md` without marking v0.1 itself complete until backup/restore and packaging are assessed. Add a dated project-history entry only if this is the first fully user-operable import milestone.

- [ ] **Step 3: Add exact manual verification instructions**

In `docs/development.md`, record:

- Markdown paste and file;
- one Steam Collection;
- multiple Steam items paste and file;
- library-only new/current Workspace;
- existing/new Collection;
- metadata partial failure;
- Back unchanged and changed destination;
- blocking resolution and Skip;
- failed Apply retaining preview;
- successful reload;
- 1024 × 640, text scaling, High Contrast, keyboard, and Narrator.

Do not add a new executable smoke command.

- [ ] **Step 4: Run formatter, build, and all tests**

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' format Tww3Companion.sln --verify-no-changes
& 'C:\Users\steve\.dotnet\dotnet.exe' build Tww3Companion.sln -c Release --no-restore
& 'C:\Users\steve\.dotnet\dotnet.exe' test Tww3Companion.sln -c Release --no-build
git diff --check
```

Expected: every command exits 0.

- [ ] **Step 5: Validate local Markdown links**

Run this exact relative-link check over every tracked Markdown file:

```powershell
$repoRoot = (Resolve-Path '.').Path
$missingLinks = [System.Collections.Generic.List[string]]::new()
git ls-files '*.md' | ForEach-Object {
  $markdownPath = Join-Path $repoRoot $_
  $markdownDirectory = Split-Path -Parent $markdownPath
  $content = Get-Content -LiteralPath $markdownPath -Raw
  [regex]::Matches($content, '(?<!\!)\[[^\]]+\]\((?<target>[^)]+)\)') | ForEach-Object {
    $target = $_.Groups['target'].Value.Trim().Trim('<', '>')
    if ($target -notmatch '^(?:https?://|mailto:|#)' -and $target -notmatch '^[A-Za-z]:[\\/]') {
      $relativeTarget = [uri]::UnescapeDataString(($target -split '#', 2)[0])
      if ($relativeTarget -and -not (Test-Path -LiteralPath (Join-Path $markdownDirectory $relativeTarget))) {
        $missingLinks.Add("$markdownPath -> $target")
      }
    }
  }
}
if ($missingLinks.Count -gt 0) {
  $missingLinks | ForEach-Object { Write-Error $_ }
  throw "$($missingLinks.Count) missing local Markdown link(s)."
}
```

Expected: the command exits without errors. Record the exact result in `.superpowers/sdd/task-7-report.md`; keep the report out of the commit.

- [ ] **Step 6: Perform manual Desktop verification**

Use a disposable Workspace outside the repository. Verify every source/destination combination listed in Step 3. Use a real public Steam Collection and item IDs only after the UI discloses the request. Confirm no source or full Workspace path appears in the application log.

Record pass/fail evidence, Windows version, display scale, and any skipped accessibility check in the uncommitted Task 7 report. Do not claim a skipped manual check passed.

- [ ] **Step 7: Commit documentation**

```powershell
git add CHANGELOG.md ROADMAP.md docs/project-history.md docs/architecture/import-export.md docs/architecture/ui.md docs/development.md RFC/RFC-0005.md
git commit -m "docs: record complete import workspace workflow"
```

If `RFC/RFC-0005.md` did not change, omit it from `git add`.

- [ ] **Step 8: Final IMP self-review**

Review the complete branch against every completion criterion in the approved spec. Confirm:

- old mandatory-Collection target factories are absent;
- all three destinations persist correctly;
- Steam production composition uses the real Infrastructure adapter;
- metadata is not requested before explicit user action;
- library and Membership outcomes remain separate;
- warnings are not described as accepted;
- preview/resolution are non-persistent;
- Apply is atomic;
- import state remains outside `ShellViewModel`;
- no new executable test hook exists;
- maintained docs agree.

Fix any issue, rerun the relevant focused tests, then rerun the full verification gate.

---
