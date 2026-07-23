using Tww3Companion.Application.Importing;
using Xunit;

namespace Tww3Companion.Application.Tests.Importing;

public sealed class SteamImportAdapterTests
{
  [Fact]
  public async Task ParseSteamCollection_returns_candidates_and_diagnostics_for_collection_id()
  {
    var result = await SteamCollectionImportAdapter.ParseAsync("123456789", new FakeSteamMetadataClient(), TestContext.Current.CancellationToken);

    Assert.NotNull(result);
  }

  [Fact]
  public async Task ParseSteamSingleItems_returns_candidates_for_multiple_pasted_ids_and_urls()
  {
    var result = await SteamSingleItemImportAdapter.ParseAsync("""
        123456789
        https://steamcommunity.com/sharedfiles/filedetails/?id=987654321
        """, new FakeSteamMetadataClient(), TestContext.Current.CancellationToken);

    Assert.NotEmpty(result.Candidates);
  }

  private sealed class FakeSteamMetadataClient : ISteamMetadataClient
  {
    public Task<SteamCollectionMetadata> GetCollectionAsync(string collectionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SteamCollectionMetadata(collectionId, [
            new SteamWorkshopItemReference("111", "https://steamcommunity.com/sharedfiles/filedetails/?id=111")
        ]));

    public Task<SteamWorkshopItemMetadata> GetWorkshopItemAsync(string workshopItemId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SteamWorkshopItemMetadata(workshopItemId, "Fixture mod"));
  }
}
