using Tww3Companion.Application.Importing;
using Xunit;

namespace Tww3Companion.Application.Tests.Importing;

public sealed class SteamCollectionImportAdapterTests
{
  [Fact]
  public async Task ParseSteamCollection_expands_collection_into_member_candidates()
  {
    var result = await SteamCollectionImportAdapter.ParseAsync("123456789", new FakeSteamMetadataClient(), TestContext.Current.CancellationToken);

    Assert.Collection(
        result.Candidates,
        candidate =>
        {
          Assert.Equal("https://steamcommunity.com/sharedfiles/filedetails/?id=111", candidate.SourceReference);
          Assert.Equal("First mod", candidate.DisplayName);
        },
        candidate =>
        {
          Assert.Equal("222", candidate.SourceReference);
          Assert.Null(candidate.DisplayName);
        });
  }

  [Fact]
  public async Task ParseSteamCollection_reports_failed_member_lookups_without_blocking_successful_items()
  {
    var result = await SteamCollectionImportAdapter.ParseAsync("123456789", new FakeSteamMetadataClient(), TestContext.Current.CancellationToken);

    Assert.Equal(2, result.Candidates.Count);
    Assert.Contains(result.Diagnostics, diagnostic => diagnostic.SourceReference == "222" && diagnostic.IsLookupFailure);
    Assert.Null(result.Candidates.Single(candidate => candidate.SourceReference == "222").DisplayName);
  }

  [Fact]
  public async Task ParseAsync_retains_member_identity_when_title_lookup_fails()
  {
    var client = new StubSteamMetadataClient(
        collectionMembers: ["111", "222"],
        itemResults: new Dictionary<string, object>
        {
          ["111"] = new SteamWorkshopItemMetadata("111", "Resolved title"),
          ["222"] = new SteamMetadataException("lookup unavailable")
        });

    var result = await SteamCollectionImportAdapter.ParseAsync(
        "900",
        client,
        TestContext.Current.CancellationToken);

    Assert.Equal(["111", "222"], result.Candidates.Select(x => x.SourceReference));
    Assert.Null(result.Candidates.Single(x => x.SourceReference == "222").DisplayName);
    Assert.Contains(result.Diagnostics, x =>
        x.SourceReference == "222" && x.IsWarning);
  }

  [Fact]
  public async Task ParseSteamCollection_uses_injected_metadata_client()
  {
    var client = new FakeSteamMetadataClient();

    var result = await SteamCollectionImportAdapter.ParseAsync("123456789", client, TestContext.Current.CancellationToken);

    Assert.True(client.CollectionWasRequested);
    Assert.Contains("111", client.RequestedMemberIds);
    Assert.NotEmpty(result.Candidates);
  }

  private sealed class StubSteamMetadataClient(
      IReadOnlyList<string> collectionMembers,
      IReadOnlyDictionary<string, object> itemResults) : ISteamMetadataClient
  {
    public Task<SteamCollectionMetadata> GetCollectionAsync(string collectionId, CancellationToken cancellationToken = default)
    {
      return Task.FromResult(new SteamCollectionMetadata(
          collectionId,
          collectionMembers.Select(id => new SteamWorkshopItemReference(id)).ToArray()));
    }

    public Task<SteamWorkshopItemMetadata> GetWorkshopItemAsync(string workshopItemId, CancellationToken cancellationToken = default)
    {
      if (!itemResults.TryGetValue(workshopItemId, out var result))
      {
        throw new InvalidOperationException($"No fixture result for workshop item {workshopItemId}.");
      }

      return result switch
      {
        SteamWorkshopItemMetadata metadata => Task.FromResult(metadata),
        SteamMetadataException exception => Task.FromException<SteamWorkshopItemMetadata>(exception),
        _ => throw new InvalidOperationException($"Unsupported fixture result type for workshop item {workshopItemId}.")
      };
    }
  }

  private sealed class FakeSteamMetadataClient : ISteamMetadataClient
  {
    public bool CollectionWasRequested { get; private set; }
    public List<string> RequestedMemberIds { get; } = [];

    public Task<SteamCollectionMetadata> GetCollectionAsync(string collectionId, CancellationToken cancellationToken = default)
    {
      CollectionWasRequested = true;
      return Task.FromResult(new SteamCollectionMetadata(collectionId, [
          new SteamWorkshopItemReference("111", "https://steamcommunity.com/sharedfiles/filedetails/?id=111"),
          new SteamWorkshopItemReference("222")
      ]));
    }

    public Task<SteamWorkshopItemMetadata> GetWorkshopItemAsync(string workshopItemId, CancellationToken cancellationToken = default)
    {
      RequestedMemberIds.Add(workshopItemId);
      if (workshopItemId == "222") throw new InvalidOperationException("Fixture lookup failure.");

      return Task.FromResult(new SteamWorkshopItemMetadata(workshopItemId, "First mod"));
    }
  }
}
