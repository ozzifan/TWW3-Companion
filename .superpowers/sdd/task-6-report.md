# Task 6 Report: Add the full-page Avalonia task and wire shell/composition

## Status

DONE

## TDD Evidence

### RED (Step 1)

Added shell navigation, layout, and composition tests before implementation. Build failed until `ShellScreen.Import`, `ImportWorkspaceView`, coordinator wiring, and view assets existed.

### GREEN (Step 6)

```text
ShellViewModelTests + MainWindowLayoutTests + ApplicationCompositionTests + Import*: 61 passed
git diff --check: clean (committed files)
```

## Commits

| Subject | Scope |
|---------|-------|
| `feat: add complete import workspace UI` | ImportWorkspaceView, shell import navigation, success handling, production Steam/coordinator wiring |

## Files Changed

| File | Change |
|------|--------|
| `ImportWorkspaceView.axaml` / `.axaml.cs` | Full-page staged import UI with accessible controls, preview + Needs Attention rows, live-region status |
| `MainWindow.axaml` | Hosts import view when `IsImportVisible` |
| `ShellViewModel.cs` | `ShellScreen.Import`, launch context, enter/leave/cancel, completion reload + workspace navigation; removed direct `IShellImportService` flow |
| `ApplicationComposition.cs` | Shared `HttpClient`, `SteamWebApiMetadataClient`, `ImportTaskCoordinator`, `ImportSourceFileService`; runtime disposes client |
| `App.axaml` | Global focus-visible styles for TextBox/RadioButton |
| `ImportWorkspaceViewModel.cs` | `LaunchContext`, stage visibility, navigation/cancel commands |
| `ImportSourceViewModel.cs` | Source kind select commands |
| `ImportDestinationViewModel.cs` | `CanChooseExistingCollection`, destination select commands, `SelectedCollection` |
| `ImportPreviewViewModel.cs` | `FilterChoices` for filter ComboBox |
| `ImportResolutionViewModel.cs` | Resolution action commands |
| `ShellViewModelTests.cs` | Import entry, completion reload, failed-apply retention |
| `MainWindowLayoutTests.cs` | Import view layout/accessibility assertions |
| `ApplicationCompositionTests.cs` | Production coordinator/Steam wiring assertion |

## Implementation Notes

- Shell owns only import task lifecycle; session state remains in `ImportWorkspaceViewModel`.
- Successful apply uses `ImportOutcome.TargetContext` (`CurrentWorkspace` after new-workspace commit) to set active workspace, reload library, select collection or Mod Library, and return to workspace screen.
- Failed apply leaves the import task on Preview with preview/resolutions intact.
- Preview and Needs Attention render in stacked rows for 1024×640 rather than side-by-side columns.
- No runtime test hooks added; completion/reload tests drive the real staged flow through `ImportWorkspaceViewModel.ApplyAsync`.

## Self-Review

- Full preview list stays visible during resolution (separate Preview and Needs Attention panels).
- Views/ViewModels do not perform SQL, file I/O, or Steam HTTP.
- `.superpowers/` and `.orchestrator-work-packet.json` excluded from commit.
- Task 1 compile-fix leftovers in `ShellViewModel` fully superseded by proper import navigation.

## Concerns

1. **Manual UI verification deferred** — layout tests assert AXAML structure; Task 7 covers manual 1024×640 / High Contrast / Narrator checklist.
2. **Collection ComboBox selection** — uses `SelectedCollection` wrapper; worth exercising in manual QA when collections list is long.

## Fix Evidence (Important review findings)

### 1. Existing Collection radio keyboard/command activation

- `SelectExistingCollectionCommand` is now parameterless (`SelectExistingCollectionFromRadio`), matching LibraryOnly/NewCollection.
- Radio activation uses current/suggested/first collection ID when no `CommandParameter` is supplied.

### 2. Discard confirmation completes dismiss

- `ConfirmDiscard()` sets the confirmed flag, refreshes `RequiresDiscardConfirmation`, and invokes the shell cancel handler.
- One Confirm discard action now dismisses the import task via the existing `CancelImport` flow.

### 3. New-workspace successful completion shell test

- Added `Successful_new_workspace_import_completion_sets_active_workspace_and_enters_workspace`.
- Asserts workspace screen, library reload, Mod Library selection, and import hidden after apply from Home entry.

## Test Summary (post-fix)

```text
Passed: 64 (ShellViewModelTests: 11, ImportWorkspaceViewModelTests: 10, full import filter)
Failed: 0
```

Commit: `fix: wire import discard and existing-collection selection`
