using Tww3Companion.Application.Importing;
using Tww3Companion.Desktop.Services;
using Tww3Companion.Desktop.ViewModels;
using Xunit;

namespace Tww3Companion.Desktop.Tests.Services;

public sealed class ImportTaskCoordinatorTests
{
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

  [Fact]
  public async Task LoadSourceAsync_enriches_markdown_workshop_ids_when_metadata_is_requested()
  {
    var metadata = new RecordingSteamMetadataClient();
    var coordinator = CreateCoordinator(metadata);
    var request = new ImportSourceRequest(
        ImportSourceKind.Markdown,
        "- 123456789",
        "notes.md",
        RequestMetadata: true);

    var result = await coordinator.LoadSourceAsync(
        request,
        TestContext.Current.CancellationToken);

    Assert.Equal(["123456789"], metadata.RequestedItemIds);
    var candidate = Assert.Single(result.Candidates);
    var markdownCandidate = Assert.IsType<MarkdownImportCandidate>(candidate);
    Assert.Equal("Mod 123456789", markdownCandidate.Value);
  }

  [Fact]
  public async Task LoadSourceAsync_retains_unresolved_identity_when_markdown_metadata_lookup_fails()
  {
    var metadata = new RecordingSteamMetadataClient(failingItemIds: ["123456789"]);
    var coordinator = CreateCoordinator(metadata);
    var request = new ImportSourceRequest(
        ImportSourceKind.Markdown,
        "- 123456789",
        "notes.md",
        RequestMetadata: true);

    var result = await coordinator.LoadSourceAsync(
        request,
        TestContext.Current.CancellationToken);

    var candidate = Assert.IsType<ImportCandidate>(Assert.Single(result.Candidates));
    Assert.Equal(ImportSourceReference.SteamWorkshop("123456789"), candidate.SourceReference);
    Assert.Null(candidate.DisplayName);
    var diagnostic = Assert.Single(result.Diagnostics, diagnostic => diagnostic.Code == "import.source.steam.lookup.failed");
    Assert.False(diagnostic.IsBlocking);
  }

  [Fact]
  public async Task LoadSourceAsync_rejects_steam_collection_input_with_multiple_ids()
  {
    var metadata = new RecordingSteamMetadataClient();
    var coordinator = CreateCoordinator(metadata);
    var request = new ImportSourceRequest(
        ImportSourceKind.SteamCollection,
        "123456789 987654321",
        DocumentName: null,
        RequestMetadata: false);

    var result = await coordinator.LoadSourceAsync(
        request,
        TestContext.Current.CancellationToken);

    Assert.Empty(result.Candidates);
    Assert.Empty(result.DisclosedWorkshopIds);
    Assert.Contains(result.Diagnostics, diagnostic =>
        diagnostic.IsBlocking &&
        diagnostic.Code == "import.source.steam.collection.invalid");
    Assert.Empty(metadata.RequestedCollectionIds);
  }

  [Fact]
  public async Task LoadSourceAsync_discloses_single_steam_collection_without_calling_metadata()
  {
    var metadata = new RecordingSteamMetadataClient();
    var coordinator = CreateCoordinator(metadata);
    var request = new ImportSourceRequest(
        ImportSourceKind.SteamCollection,
        "123456789",
        DocumentName: null,
        RequestMetadata: false);

    var result = await coordinator.LoadSourceAsync(
        request,
        TestContext.Current.CancellationToken);

    Assert.Equal(["123456789"], result.DisclosedWorkshopIds);
    Assert.Empty(metadata.RequestedCollectionIds);
    var candidate = Assert.IsType<SteamImportCandidate>(Assert.Single(result.Candidates));
    Assert.Equal("123456789", candidate.SourceReference);
    Assert.Null(candidate.DisplayName);
  }

  [Fact]
  public async Task LoadSourceAsync_accepts_steam_collection_url_without_calling_metadata()
  {
    var metadata = new RecordingSteamMetadataClient();
    var coordinator = CreateCoordinator(metadata);
    const string collectionUrl =
        "https://steamcommunity.com/sharedfiles/filedetails/?id=123456789";
    var request = new ImportSourceRequest(
        ImportSourceKind.SteamCollection,
        collectionUrl,
        DocumentName: null,
        RequestMetadata: false);

    var result = await coordinator.LoadSourceAsync(
        request,
        TestContext.Current.CancellationToken);

    Assert.Equal(["123456789"], result.DisclosedWorkshopIds);
    Assert.Empty(metadata.RequestedCollectionIds);
    var candidate = Assert.IsType<SteamImportCandidate>(Assert.Single(result.Candidates));
    Assert.Equal("123456789", candidate.SourceReference);
  }

  [Fact]
  public async Task LoadSourceAsync_expands_steam_collection_when_metadata_is_requested()
  {
    var metadata = new RecordingSteamMetadataClient();
    var coordinator = CreateCoordinator(metadata);
    var request = new ImportSourceRequest(
        ImportSourceKind.SteamCollection,
        "123456789",
        DocumentName: null,
        RequestMetadata: true);

    var result = await coordinator.LoadSourceAsync(
        request,
        TestContext.Current.CancellationToken);

    Assert.Equal(["123456789"], metadata.RequestedCollectionIds);
    Assert.Equal(2, result.Candidates.Count);
    Assert.All(result.Candidates, candidate => Assert.IsType<SteamImportCandidate>(candidate));
  }

  [Fact]
  public async Task LoadSourceAsync_discloses_multiple_steam_items_and_preserves_invalid_tokens()
  {
    var metadata = new RecordingSteamMetadataClient();
    var coordinator = CreateCoordinator(metadata);
    var request = new ImportSourceRequest(
        ImportSourceKind.SteamItems,
        """
        123456789
        bad input
        987654321
        """,
        DocumentName: null,
        RequestMetadata: false);

    var result = await coordinator.LoadSourceAsync(
        request,
        TestContext.Current.CancellationToken);

    Assert.Equal(["123456789", "987654321"], result.DisclosedWorkshopIds);
    Assert.Empty(metadata.RequestedItemIds);
    Assert.Equal(2, result.Candidates.Count);
    Assert.Contains(result.Diagnostics, diagnostic =>
        diagnostic.IsBlocking &&
        diagnostic.Code == "import.source.steam.item.invalid");
  }

  [Fact]
  public async Task LoadSourceAsync_requests_metadata_for_multiple_steam_items()
  {
    var metadata = new RecordingSteamMetadataClient();
    var coordinator = CreateCoordinator(metadata);
    var request = new ImportSourceRequest(
        ImportSourceKind.SteamItems,
        """
        123456789
        987654321
        """,
        DocumentName: null,
        RequestMetadata: true);

    var result = await coordinator.LoadSourceAsync(
        request,
        TestContext.Current.CancellationToken);

    Assert.Equal(["123456789", "987654321"], metadata.RequestedItemIds);
    Assert.Equal(2, result.Candidates.Count);
    Assert.All(result.Candidates, candidate =>
    {
      var steamCandidate = Assert.IsType<SteamImportCandidate>(candidate);
      Assert.NotNull(steamCandidate.DisplayName);
    });
  }

  [Fact]
  public async Task ApplyAsync_delegates_to_engine_with_confirm_true()
  {
    var engine = new RecordingImportEngine();
    var coordinator = new ImportTaskCoordinator(engine, new RecordingSteamMetadataClient());
    var preview = new ImportPreview(
        ImportTargetContext.ForCurrentWorkspace(
            "workspace-1",
            @"C:\Data\workspace.tww3c",
            ImportMembershipDestination.ForLibraryOnly()),
        [],
        Applied: false);

    var outcome = await coordinator.ApplyAsync(preview, TestContext.Current.CancellationToken);

    Assert.True(engine.LastConfirm);
    Assert.Same(preview, engine.LastPreview);
    Assert.True(outcome.Applied);
  }

  [Fact]
  public async Task BuildPreviewAsync_and_ResolveAsync_delegate_to_engine()
  {
    var engine = new RecordingImportEngine();
    var coordinator = new ImportTaskCoordinator(engine, new RecordingSteamMetadataClient());
    var targetContext = ImportTargetContext.ForCurrentWorkspace(
        "workspace-1",
        @"C:\Data\workspace.tww3c",
        ImportMembershipDestination.ForLibraryOnly());
    var candidates = new object[] { new SteamImportCandidate("123456789", "Example") };

    var preview = await coordinator.BuildPreviewAsync(
        targetContext,
        candidates,
        TestContext.Current.CancellationToken);
    var resolvedCandidate = ImportCandidate.CreateWithDisplayName(
        "candidate-1",
        "Resolved",
        ImportSourceReference.SteamWorkshop("123"));
    var resolved = await coordinator.ResolveAsync(
        preview,
        resolvedCandidate,
        TestContext.Current.CancellationToken);

    Assert.Same(targetContext, engine.LastTargetContext);
    Assert.Same(candidates, engine.LastCandidates);
    Assert.Same(preview, engine.LastResolvePreview);
    Assert.Same(resolvedCandidate, engine.LastResolvedCandidate);
    Assert.Same(engine.ResolveResult, resolved);
  }

  private static ImportTaskCoordinator CreateCoordinator(RecordingSteamMetadataClient metadata) =>
      new(new RecordingImportEngine(), metadata);

  private sealed class RecordingImportEngine : IImportEngine
  {
    public ImportTargetContext? LastTargetContext { get; private set; }
    public IReadOnlyList<object>? LastCandidates { get; private set; }
    public ImportPreview? LastResolvePreview { get; private set; }
    public ImportCandidate? LastResolvedCandidate { get; private set; }
    public ImportPreview? LastPreview { get; private set; }
    public bool? LastConfirm { get; private set; }
    public ImportPreview ResolveResult { get; } = new(
        ImportTargetContext.ForCurrentWorkspace(
            "workspace-1",
            @"C:\Data\workspace.tww3c",
            ImportMembershipDestination.ForLibraryOnly()),
        [],
        Applied: false);

    public Task<ImportPreview> BuildPreviewAsync(
        ImportTargetContext targetContext,
        IReadOnlyList<object> candidates,
        CancellationToken cancellationToken = default)
    {
      LastTargetContext = targetContext;
      LastCandidates = candidates;
      return Task.FromResult(new ImportPreview(targetContext, [], Applied: false));
    }

    public Task<ImportPreview> ResolveAsync(
        ImportPreview preview,
        ImportCandidate resolvedCandidate,
        CancellationToken cancellationToken = default)
    {
      LastResolvePreview = preview;
      LastResolvedCandidate = resolvedCandidate;
      return Task.FromResult(ResolveResult);
    }

    public Task<ImportOutcome> ApplyAsync(
        ImportPreview preview,
        bool confirm,
        CancellationToken cancellationToken = default)
    {
      LastPreview = preview;
      LastConfirm = confirm;
      return Task.FromResult(new ImportOutcome(preview.TargetContext, [], Applied: confirm));
    }
  }

  private sealed class RecordingSteamMetadataClient : ISteamMetadataClient
  {
    private readonly HashSet<string> failingItemIds;

    public RecordingSteamMetadataClient(IEnumerable<string>? failingItemIds = null)
    {
      this.failingItemIds = failingItemIds?.ToHashSet(StringComparer.Ordinal) ?? [];
    }

    public List<string> RequestedCollectionIds { get; } = [];
    public List<string> RequestedItemIds { get; } = [];

    public Task<SteamCollectionMetadata> GetCollectionAsync(string collectionId, CancellationToken cancellationToken = default)
    {
      RequestedCollectionIds.Add(collectionId);
      return Task.FromResult(new SteamCollectionMetadata(collectionId, [
          new SteamWorkshopItemReference("111"),
          new SteamWorkshopItemReference("222")
      ]));
    }

    public Task<SteamWorkshopItemMetadata> GetWorkshopItemAsync(string workshopItemId, CancellationToken cancellationToken = default)
    {
      RequestedItemIds.Add(workshopItemId);
      if (failingItemIds.Contains(workshopItemId))
      {
        return Task.FromException<SteamWorkshopItemMetadata>(
            new SteamMetadataException("lookup unavailable"));
      }

      return Task.FromResult(new SteamWorkshopItemMetadata(workshopItemId, $"Mod {workshopItemId}"));
    }
  }
}
