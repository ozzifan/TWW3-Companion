namespace Tww3Companion.Application.Importing;

public static class SteamSingleItemImportAdapter
{
  public static Task<SteamImportResult> ParseAsync(string pastedIdsAndUrls, CancellationToken cancellationToken = default)
  {
    return ParseAsync(pastedIdsAndUrls, SteamImportAdapter.DefaultMetadataClient, cancellationToken);
  }

  public static Task<SteamImportResult> ParseAsync(
      string pastedIdsAndUrls,
      ISteamMetadataClient metadataClient,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(metadataClient);
    return SteamImportAdapter.ParseSingleItemInputAsync(pastedIdsAndUrls, metadataClient, cancellationToken);
  }
}
