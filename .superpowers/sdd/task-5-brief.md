### Task 5: Implement the staged import-session ViewModels

**Files:**
- Create: `src/Tww3Companion.Desktop/ViewModels/ImportWorkspaceViewModel.cs`
- Create: `src/Tww3Companion.Desktop/ViewModels/ImportSourceViewModel.cs`
- Create: `src/Tww3Companion.Desktop/ViewModels/ImportDestinationViewModel.cs`
- Create: `src/Tww3Companion.Desktop/ViewModels/ImportPreviewViewModel.cs`
- Create: `src/Tww3Companion.Desktop/ViewModels/ImportResolutionViewModel.cs`
- Create: `src/Tww3Companion.Desktop/ViewModels/ImportConfirmationViewModel.cs`
- Create: `tests/Tww3Companion.Desktop.Tests/ViewModels/ImportWorkspaceViewModelTests.cs`
- Create: `tests/Tww3Companion.Desktop.Tests/ViewModels/ImportPreviewViewModelTests.cs`
- Create: `tests/Tww3Companion.Desktop.Tests/ViewModels/ImportResolutionViewModelTests.cs`

**Interfaces:**
- Consumes: Task 4 `IImportTaskCoordinator`, `IImportSourceFileService`, and Task 1 typed preview contracts
- Produces: complete four-stage task state, commands, filters, resolution, immutable confirmation, and result events

- [ ] **Step 1: Write failing stage and launch-context tests**

Define:

```csharp
public enum ImportTaskStage
{
  Source,
  Destination,
  Preview,
  Confirmation,
  Finalizing,
  Complete
}

public sealed record ImportLaunchContext(
    bool IsNewWorkspace,
    string? WorkspaceId,
    string? WorkspacePath,
    IReadOnlyList<CollectionSummary> Collections,
    string? SelectedCollectionId);
```

Test:

```csharp
[Fact]
public void SteamCollection_suggests_selected_collection_but_does_not_lock_it()
{
  var subject = CreateSubject(new ImportLaunchContext(
      IsNewWorkspace: false,
      WorkspaceId: "workspace-1",
      WorkspacePath: @"C:\Data\workspace.tww3c",
      Collections: [new CollectionSummary("collection-1", "Current", 0)],
      SelectedCollectionId: "collection-1"));

  subject.Source.Select(ImportSourceKind.SteamCollection);
  subject.OpenDestination();

  Assert.Equal("collection-1", subject.Destination.SelectedCollectionId);
  subject.Destination.SelectLibraryOnly();
  Assert.IsType<ImportMembershipDestination.LibraryOnly>(
      subject.Destination.BuildMembershipDestination());
}
```

Add Home option absence, source/destination fingerprint reuse, changed fingerprint rebuild, discard confirmation, and Apply finalization tests.

- [ ] **Step 2: Implement source and destination ViewModels**

`ImportSourceViewModel` exposes:

- `SelectedKind`;
- `InputText`;
- `SelectedDocumentName`;
- `DisclosedWorkshopIds`;
- `Diagnostics`;
- `IsBusy`;
- `CanContinue`;
- `ChooseFileCommand`;
- `ContinueCommand`.

`ImportDestinationViewModel` exposes:

- Workspace name/path only for Home;
- `IsLibraryOnly`, `IsExistingCollection`, `IsNewCollection`;
- existing Collection list and selection;
- new Collection name;
- `CanContinue`;
- `BuildTargetContext()`.

Existing Collection is absent for `IsNewWorkspace`. Suggestions initialize once and never overwrite an explicit user choice.

- [ ] **Step 3: Implement preview rows, filters, and summary**

Each `ImportPreviewRowViewModel` exposes:

```csharp
string CandidateId
string DisplayName
ImportLibraryAction LibraryAction
ImportMembershipAction MembershipAction
bool HasWarning
bool IsBlocking
bool IsSkipped
```

Filters are an enum, not string comparisons:

```csharp
public enum ImportPreviewFilter
{
  All,
  Additions,
  Enrichments,
  Existing,
  SuggestedMatches,
  Conflicts,
  Warnings,
  Skipped
}
```

`ImportConfirmationSummary` contains exact integer counts and `WarningsRemaining`; it never contains “warnings accepted.”

- [ ] **Step 4: Implement Needs Attention resolution**

`ImportResolutionViewModel` exposes link/create/skip actions. Each action creates a replacement `ImportCandidate` and awaits coordinator `ResolveAsync`. Source-owner collision supports only link to owning Mod or Skip. Scalar conflict exposes the competing values and requires one explicit selected value.

After resolution:

- replace `ImportPreviewViewModel.Preview`;
- move to the next blocking row;
- retain the full preview list;
- set `CanContinue` only when no blocking operation remains.

- [ ] **Step 5: Implement parent session navigation and fingerprint rules**

`ImportWorkspaceViewModel` owns all child ViewModels and the last successful:

```csharp
public sealed record ImportPreviewFingerprint(
    ImportSourceKind SourceKind,
    string SourceDigest,
    ImportTargetContext TargetContext);
```

Hash source text in memory with SHA-256; never log or persist it. On Continue:

- same fingerprint: reuse preview/resolutions without parsing, metadata, or preview call;
- changed fingerprint: rebuild and retain a resolution only when candidate identity and available choices are unchanged;
- any change invalidates confirmation.

Apply sets `Finalizing`, disables Back/Cancel, and emits a typed completion event containing `ImportOutcome`.

- [ ] **Step 6: Run focused ViewModel tests**

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj --filter "FullyQualifiedName~ImportWorkspaceViewModelTests|FullyQualifiedName~ImportPreviewViewModelTests|FullyQualifiedName~ImportResolutionViewModelTests" -v minimal
```

Expected: all stage, default, filter, resolution, fingerprint, failure-retention, and Apply-state tests pass.

- [ ] **Step 7: Review and commit Task 5**

```powershell
git diff --check
git add src/Tww3Companion.Desktop/ViewModels tests/Tww3Companion.Desktop.Tests/ViewModels
git commit -m "feat: add staged import task view models"
```

Review must confirm import-session state did not move into `ShellViewModel`.

---
