using Tww3Companion.Application.Importing;
using Xunit;

namespace Tww3Companion.Application.Tests.Importing;

public sealed class SteamImportServiceTests
{
  [Fact]
  public async Task SteamCollection_preview_uses_the_collection_action()
  {
    var result = await SteamCollectionImportAdapter.ParseAsync("123456789", new SteamCollectionFixtureClient(), TestContext.Current.CancellationToken);
    var preview = await SteamImportService.BuildPreviewAsync(result);

    Assert.NotNull(preview);
    Assert.False(preview.Applied);
  }

  [Fact]
  public async Task SteamSingleItem_preview_accepts_multiple_items()
  {
    var result = await SteamSingleItemImportAdapter.ParseAsync("""
        123456789
        987654321
        """, new SteamSingleItemFixtureClient(), TestContext.Current.CancellationToken);
    var preview = await SteamImportService.BuildPreviewAsync(result);

    Assert.NotNull(preview);
    Assert.False(preview.Applied);
  }

  [Fact]
  public async Task SteamImport_apply_marks_the_preview_as_applied_when_confirmed()
  {
    var result = new SteamImportResult([
        new SteamImportCandidate("123456789", "Fixture mod")
    ], []);

    var applied = await SteamImportService.ApplyAsync(result, confirm: true);

    Assert.True(applied.Applied);
  }

  private sealed class SteamCollectionFixtureClient : ISteamMetadataClient
  {
    public Task<SteamCollectionMetadata> GetCollectionAsync(string collectionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SteamCollectionMetadata(collectionId, [
            new SteamWorkshopItemReference("111", "https://steamcommunity.com/sharedfiles/filedetails/?id=111"),
            new SteamWorkshopItemReference("222", "https://steamcommunity.com/sharedfiles/filedetails/?id=222")
        ]));

    public Task<SteamWorkshopItemMetadata> GetWorkshopItemAsync(string workshopItemId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SteamWorkshopItemMetadata(workshopItemId, $"Mod {workshopItemId}"));
  }

  private sealed class SteamSingleItemFixtureClient : ISteamMetadataClient
  {
    public Task<SteamCollectionMetadata> GetCollectionAsync(string collectionId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<SteamWorkshopItemMetadata> GetWorkshopItemAsync(string workshopItemId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SteamWorkshopItemMetadata(workshopItemId, $"Mod {workshopItemId}"));
  }
}
