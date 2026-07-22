namespace Tww3Companion.Application.Importing;

public static class SteamCollectionImportAdapter
{
  public static Task<SteamImportResult> ParseAsync(string collectionId, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(collectionId);
    cancellationToken.ThrowIfCancellationRequested();

    return Task.FromResult(new SteamImportResult([], [new SteamImportDiagnostic(collectionId, "Steam collection input read.")]));
  }
}
