# Collection Library Overlay Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show the full global mod library inside the workspace shell, with clear membership markers for the current workspace and collection summaries driven from the same snapshot.

**Architecture:** The application exposes one active workspace at a time, but the current workspace query returns an overlay view over the global mod catalog. The desktop shell owns the library and collection panels, receives a ready-to-render snapshot, and maps it into the existing view models without joining data in the UI. Selection stays local to the desktop layer; persistence and membership assembly stay in the application/data layer.

**Tech Stack:** C# / .NET 10, Avalonia, xUnit, existing workspace query and shell composition layers.

## Global Constraints

- The user works in one workspace at a time.
- Global mod data must remain available so all mods can be shown to the active workspace.
- The desktop must render a full library view and mark membership for the selected workspace.
- Do not duplicate the global catalog into workspace-local storage just for the UI.
- Keep error handling boring: if the query fails, show the existing workspace error surface and leave the panels empty.

---

### Task 1: Define the overlay snapshot contract and failing tests

**Files:**
- Create: `tests/Tww3Companion.Application.Tests/Workspaces/WorkspaceLibrarySnapshotTests.cs`
- Modify: `tests/Tww3Companion.Application.Tests/Workspaces/WorkspaceQueryTests.cs`
- Modify: `tests/Tww3Companion.Desktop.Tests/ViewModels/ModLibraryViewModelTests.cs`
- Modify: `tests/Tww3Companion.Desktop.Tests/ViewModels/HomeCompositionTests.cs`

**Interfaces:**
- Consumes: `IWorkspaceQuery`, `WorkspaceLibrarySnapshot`, `WorkspaceLibraryMod`, `WorkspaceCollection`, `WorkspaceCollectionMembership`, `ModLibraryViewModel`, `CollectionDetailViewModel`, `ShellViewModel`
- Produces: the exact snapshot shape and desktop-facing methods the later tasks implement

- [ ] **Step 1: Write the failing overlay-snapshot test**

Add a test that proves the snapshot is the right container for the global catalog plus workspace membership rows:

```csharp
[Fact]
public void SnapshotCarriesGlobalModsCollectionsAndMemberships()
{
    var snapshot = new WorkspaceLibrarySnapshot(
        [
            new WorkspaceLibraryMod("mod-1", "Alpha Mod"),
            new WorkspaceLibraryMod("mod-2", "Beta Mod")
        ],
        [
            new WorkspaceCollection("collection-1", "Core Collection")
        ],
        [
            new WorkspaceCollectionMembership("collection-1", "mod-1")
        ]);

    Assert.Equal(2, snapshot.Mods.Count);
    Assert.Equal(1, snapshot.Collections.Count);
    Assert.Single(snapshot.Memberships);
    Assert.Equal("mod-1", snapshot.Memberships[0].ModId);
}
```

Add a query contract test that confirms the workspace query returns `Task<WorkspaceLibrarySnapshot>`.

Add desktop tests that describe the desired overlay behavior:

```csharp
[Fact]
public async Task LoadAsyncPopulatesModsCollectionsAndMembershipsFromWorkspaceSnapshot()
{
    var query = new FakeWorkspaceQuery(
        new WorkspaceLibrarySnapshot(
            [
                new WorkspaceLibraryMod("mod-1", "Alpha Mod"),
                new WorkspaceLibraryMod("mod-2", "Beta Mod")
            ],
            [
                new WorkspaceCollection("collection-1", "Core Collection"),
                new WorkspaceCollection("collection-2", "Other Collection")
            ],
            [
                new WorkspaceCollectionMembership("collection-1", "mod-1")
            ]));

    var subject = new ModLibraryViewModel(query);

    await subject.LoadAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, subject.Mods.Count);
    Assert.Equal("Alpha Mod", subject.Mods[0].DisplayName);
    Assert.Equal(["Core Collection"], subject.Mods[0].CollectionNames);
    Assert.Empty(subject.Mods[1].CollectionNames);
    Assert.Equal(2, subject.Collections.Count);
}
```

Add a shell-level test that the library and collection view models exist and start empty when no query is available.

- [ ] **Step 2: Run the focused tests and confirm they fail for the missing pieces**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj --filter "WorkspaceQueryTests|WorkspaceLibrarySnapshotTests"
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj --filter "ModLibraryViewModelTests|HomeCompositionTests"
```

Expected: failures for the new overlay assertions until the implementation is in place.

- [ ] **Step 3: Commit the failing tests**

```powershell
git add tests/Tww3Companion.Application.Tests tests/Tww3Companion.Desktop.Tests
git commit -m "test: define collection library overlay"
```

### Task 2: Implement the workspace overlay query and desktop bindings

**Files:**
- Create: `src/Tww3Companion.Application/Workspaces/WorkspaceLibraryQuery.cs`
- Modify: `src/Tww3Companion.Application/Workspaces/IWorkspaceQuery.cs`
- Modify: `src/Tww3Companion.Desktop/ViewModels/ShellViewModel.cs`
- Modify: `src/Tww3Companion.Desktop/ViewModels/ModLibraryViewModels.cs`
- Modify: `src/Tww3Companion.Desktop/Composition/ApplicationComposition.cs`
- Modify: `src/Tww3Companion.Desktop/Views/MainWindow.axaml`
- Modify: `src/Tww3Companion.Desktop/Views/MainWindow.axaml.cs` if constructor wiring needs the query instance
- Modify: `src/Tww3Companion.Infrastructure/Storage/SqliteWorkspaceStore.cs` only if a concrete read path must be added beside the existing store

**Interfaces:**
- Consumes: `WorkspaceLibrarySnapshot`, the current workspace lifecycle, and the existing shell view models
- Produces: a real `IWorkspaceQuery` implementation and the desktop bindings that consume it

- [ ] **Step 1: Add the minimal query implementation**

Create a query adapter that opens the active workspace data source, reads the global mod catalog, collection rows, and membership rows, and returns one `WorkspaceLibrarySnapshot`.

The query must stay read-only and must not mutate the workspace file. Wire it into the desktop composition root so `ShellViewModel` receives it through constructor options and constructs `ModLibraryViewModel` / `CollectionDetailViewModel` with it.

- [ ] **Step 2: Keep the desktop bindings driven by the overlay snapshot**

Ensure the shell still uses the current selection behavior:

- selecting a mod updates the inspector
- selecting a collection toggles membership markers
- the workspace panels remain empty if the query is unavailable or fails

Keep the `MainWindow` bindings pointed at the shell-owned view models instead of moving overlay logic into XAML.

- [ ] **Step 3: Run the focused desktop tests**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj --filter "ModLibraryViewModelTests|HomeCompositionTests|MainWindowLayoutTests"
```

Expected: the new query-backed overlay tests and the existing desktop composition tests all pass.

- [ ] **Step 4: Commit the implementation**

```powershell
git add src/Tww3Companion.Application src/Tww3Companion.Desktop
git commit -m "feat: add collection library overlay"
```

### Task 3: Verify the slice and update the task report

**Files:**
- Modify: `.superpowers/sdd/task-2-report.md`
- Modify: `docs/superpowers/plans/2026-07-24-collection-library-overlay.md` only if an implementation detail needs a corrected constraint

**Interfaces:**
- Consumes: the completed overlay implementation
- Produces: a verified slice ready for the next feature boundary

- [ ] **Step 1: Run the final verification set**

Run:

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj
git diff --check
```

- [ ] **Step 2: Update the task report with the result**

Document the final test status and any deliberate limitations, especially that the overlay is read-only and that the UI does not assemble the membership data itself.

- [ ] **Step 3: Commit the verification report**

```powershell
git add .superpowers/sdd/task-2-report.md
git commit -m "test: verify collection library overlay"
```

## Self-Review Checklist

- The plan keeps one active workspace at a time.
- The plan preserves a global mod catalog and overlays workspace membership on top of it.
- The desktop remains a consumer of the snapshot, not the place where the overlay is assembled.
- Error handling is explicitly fail-closed.
- The tasks are independently testable and do not mix unrelated feature work.
