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
        });
  }

  [Fact]
  public async Task ParseSteamCollection_reports_failed_member_lookups_without_blocking_successful_items()
  {
    var result = await SteamCollectionImportAdapter.ParseAsync("123456789", new FakeSteamMetadataClient(), TestContext.Current.CancellationToken);

    Assert.Contains(result.Diagnostics, diagnostic => diagnostic.SourceReference == "222" && diagnostic.IsLookupFailure);
    Assert.Single(result.Candidates);
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
