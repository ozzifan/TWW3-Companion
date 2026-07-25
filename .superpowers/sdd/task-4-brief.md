### Task 4: Build the stateless Desktop import coordinator and file service

**Files:**
- Create: `src/Tww3Companion.Desktop/Services/IImportSourceFileService.cs`
- Create: `src/Tww3Companion.Desktop/Services/ImportSourceFileService.cs`
- Create: `src/Tww3Companion.Desktop/Services/IImportTaskCoordinator.cs`
- Create: `src/Tww3Companion.Desktop/Services/ImportTaskCoordinator.cs`
- Create: `src/Tww3Companion.Desktop/ViewModels/ImportTaskModels.cs`
- Create: `tests/Tww3Companion.Desktop.Tests/Services/ImportSourceFileServiceTests.cs`
- Create: `tests/Tww3Companion.Desktop.Tests/Services/ImportTaskCoordinatorTests.cs`

**Interfaces:**
- Consumes: `ImportTextDecoder`, source adapters, `ISteamMetadataClient`, and Task 1 `IImportEngine`
- Produces: typed source load, preview, resolution, and Apply façade for ViewModels

- [ ] **Step 1: Define the presentation contracts in failing tests**

Use exact models:

```csharp
public enum ImportSourceKind
{
  Markdown,
  SteamCollection,
  SteamItems
}

public sealed record ImportSourceDocument(string Name, string Text);

public sealed record ImportTaskDiagnostic(
    string Code,
    string Message,
    bool IsBlocking,
    string SafeNextAction);

public sealed record ImportSourceLoadResult(
    IReadOnlyList<object> Candidates,
    IReadOnlyList<ImportTaskDiagnostic> Diagnostics,
    IReadOnlyList<string> DisclosedWorkshopIds);
```

Test:

```csharp
[Fact]
public async Task LoadSourceAsync_does_not_contact_Steam_until_requestMetadata_is_true()
{
  var metadata = new RecordingSteamMetadataClient();
  var coordinator = CreateCoordinator(metadata);
  var request = new ImportSourceRequest(
      ImportSourceKind.Markdown,
      "- 123456789",
      "notes.md",
      RequestMetadata: false);

  var result = await coordinator.LoadSourceAsync(
      request,
      TestContext.Current.CancellationToken);

  Assert.Equal(["123456789"], result.DisclosedWorkshopIds);
  Assert.Empty(metadata.RequestedItemIds);
}
```

Add tests for collection single-ID validation, multiple items, Markdown enrichment, partial metadata failure retaining unresolved identity, and coordinator Apply delegation.

- [ ] **Step 2: Implement the source file service**

Interface:

```csharp
public interface IImportSourceFileService
{
  Task<ImportSourceDocument?> ChooseTextFileAsync(
      CancellationToken cancellationToken = default);
}
```

`ImportSourceFileService` uses `TopLevel.StorageProvider.OpenFilePickerAsync` with one file, `.md` / `.txt` filters, a bounded read, and `ImportTextDecoder.Decode`. Set a 4 MiB v0.1 maximum before allocation. Return only `Path.GetFileName(storageFile.Name)` plus decoded text; never return or log the full path.

- [ ] **Step 3: Implement the coordinator interface**

```csharp
public interface IImportTaskCoordinator
{
  Task<ImportSourceLoadResult> LoadSourceAsync(
      ImportSourceRequest request,
      CancellationToken cancellationToken = default);

  Task<ImportPreview> BuildPreviewAsync(
      ImportTargetContext targetContext,
      IReadOnlyList<object> candidates,
      CancellationToken cancellationToken = default);

  Task<ImportPreview> ResolveAsync(
      ImportPreview preview,
      ImportCandidate resolvedCandidate,
      CancellationToken cancellationToken = default);

  Task<ImportOutcome> ApplyAsync(
      ImportPreview preview,
      CancellationToken cancellationToken = default);
}
```

The implementation is stateless. It never stores source text, destination, preview, or resolutions in fields.

- [ ] **Step 4: Implement source loading and metadata disclosure**

Rules:

- Markdown: parse locally first, collect recognised Workshop IDs, and request item metadata only when `RequestMetadata` is true.
- Steam Collection: validate exactly one ID/URL, disclose the Collection identity, and call the collection adapter only when metadata is requested.
- Steam items: normalize/disclose valid IDs, preserve invalid tokens as diagnostics, and call the item adapter only when requested.
- A failed item lookup produces an unresolved `ImportCandidate` with its canonical Steam Source Reference plus a non-blocking lookup diagnostic; it is not dropped.
- Never invent a display name from an ID or URL.
- `ApplyAsync` always delegates as `engine.ApplyAsync(preview, confirm: true, cancellationToken)`.

- [ ] **Step 5: Run Desktop service tests**

```powershell
& 'C:\Users\steve\.dotnet\dotnet.exe' test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj --filter "FullyQualifiedName~ImportSourceFileServiceTests|FullyQualifiedName~ImportTaskCoordinatorTests" -v minimal
```

Expected: all file, disclosure, metadata, diagnostics, and delegation tests pass without real UI or network.

- [ ] **Step 6: Review and commit Task 4**

```powershell
git diff --check
git add src/Tww3Companion.Desktop/Services src/Tww3Companion.Desktop/ViewModels/ImportTaskModels.cs tests/Tww3Companion.Desktop.Tests/Services
git commit -m "feat: coordinate import sources and previews"
```

Review must confirm coordinator statelessness and no source/full-path logging.

---
