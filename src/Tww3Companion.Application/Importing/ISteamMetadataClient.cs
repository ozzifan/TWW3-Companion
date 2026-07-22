namespace Tww3Companion.Application.Importing;

public interface ISteamMetadataClient
{
  Task<SteamCollectionMetadata> GetCollectionAsync(string collectionId, CancellationToken cancellationToken = default);

  Task<SteamWorkshopItemMetadata> GetWorkshopItemAsync(string workshopItemId, CancellationToken cancellationToken = default);
}

public sealed record SteamCollectionMetadata(
    string SourceReference,
    IReadOnlyList<SteamWorkshopItemReference> Members);

public sealed record SteamWorkshopItemReference(string WorkshopItemId, string? SourceReference = null);

public sealed record SteamWorkshopItemMetadata(string WorkshopItemId, string DisplayName);
