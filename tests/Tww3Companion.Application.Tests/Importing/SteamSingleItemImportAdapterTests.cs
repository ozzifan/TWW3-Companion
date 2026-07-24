using Tww3Companion.Application.Importing;
using Xunit;

namespace Tww3Companion.Application.Tests.Importing;

public sealed class SteamSingleItemImportAdapterTests
{
  [Fact]
  public async Task ParseSteamSingleItems_accepts_multiple_ids_and_urls_in_one_paste()
  {
    var result = await SteamSingleItemImportAdapter.ParseAsync("""
        123456789
        https://steamcommunity.com/sharedfiles/filedetails/?id=987654321
        """, new FakeSteamMetadataClient(), TestContext.Current.CancellationToken);

    Assert.True(result.Candidates.Count >= 2);
  }

  [Fact]
  public async Task ParseSteamSingleItems_accepts_urls_without_the_trailing_slash()
  {
    var result = await SteamSingleItemImportAdapter.ParseAsync("""
        https://steamcommunity.com/sharedfiles/filedetails?id=987654321
        """, new FakeSteamMetadataClient(), TestContext.Current.CancellationToken);

    Assert.Single(result.Candidates);
    Assert.Equal("Mod 987654321", result.Candidates[0].DisplayName);
  }

  [Fact]
  public async Task ParseSteamSingleItems_rejects_empty_query_ids()
  {
    var result = await SteamSingleItemImportAdapter.ParseAsync("""
        https://steamcommunity.com/sharedfiles/filedetails/?id=
        """, new FakeSteamMetadataClient(), TestContext.Current.CancellationToken);

    Assert.Contains(result.Diagnostics, diagnostic => diagnostic.IsLookupFailure);
    Assert.Empty(result.Candidates);
  }

  [Fact]
  public async Task ParseSteamSingleItems_reports_failed_lookups_per_item_without_stopping_the_batch()
  {
    var result = await SteamSingleItemImportAdapter.ParseAsync("""
        123456789
        bad input
        987654321
        """, new FakeSteamMetadataClient("987654321"), TestContext.Current.CancellationToken);

    Assert.Contains(result.Diagnostics, diagnostic => diagnostic.IsLookupFailure);
    Assert.Single(result.Candidates);
  }

  private sealed class FakeSteamMetadataClient : ISteamMetadataClient
  {
    private readonly string? failingWorkshopItemId;

    public FakeSteamMetadataClient(string? failingWorkshopItemId = null)
    {
      this.failingWorkshopItemId = failingWorkshopItemId;
    }

    public Task<SteamCollectionMetadata> GetCollectionAsync(string collectionId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<SteamWorkshopItemMetadata> GetWorkshopItemAsync(string workshopItemId, CancellationToken cancellationToken = default)
    {
      if (workshopItemId == failingWorkshopItemId) throw new InvalidOperationException("Fixture lookup failure.");

      return Task.FromResult(new SteamWorkshopItemMetadata(workshopItemId, $"Mod {workshopItemId}"));
    }
  }
}
