namespace Tww3Companion.Application.Importing;

public static class SteamSingleItemImportAdapter
{
  public static Task<SteamImportResult> ParseAsync(string pastedIdsAndUrls, CancellationToken cancellationToken = default)
  {
    return Task.FromResult(SteamImportAdapter.ParseSingleItemInput(pastedIdsAndUrls, cancellationToken));
  }
}
