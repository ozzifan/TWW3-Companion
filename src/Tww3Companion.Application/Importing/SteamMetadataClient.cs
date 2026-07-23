namespace Tww3Companion.Application.Importing;

public sealed class SteamMetadataClient : ISteamMetadataClient
{
  public Task<SteamCollectionMetadata> GetCollectionAsync(string collectionId, CancellationToken cancellationToken = default)
  {
    throw new NotSupportedException("Steam metadata lookup has not been configured.");
  }

  public Task<SteamWorkshopItemMetadata> GetWorkshopItemAsync(string workshopItemId, CancellationToken cancellationToken = default)
  {
    throw new NotSupportedException("Steam metadata lookup has not been configured.");
  }
}
