namespace Tww3Companion.Application.Importing;

public static class SteamCollectionImportAdapter
{
  public static async Task<SteamImportResult> ParseAsync(
      string collectionId,
      ISteamMetadataClient metadataClient,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(collectionId);
    ArgumentNullException.ThrowIfNull(metadataClient);
    cancellationToken.ThrowIfCancellationRequested();

    var sourceReference = collectionId.Trim();
    if (!SteamImportAdapter.TryGetCollectionId(sourceReference, out var normalizedCollectionId))
    {
      return new SteamImportResult([], [new SteamImportDiagnostic(collectionId, "Steam collection input must contain exactly one numeric collection ID or supported URL.")]);
    }

    SteamCollectionMetadata collection;
    try
    {
      collection = await metadataClient.GetCollectionAsync(normalizedCollectionId, cancellationToken);
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
      return new SteamImportResult([], [new SteamImportDiagnostic(normalizedCollectionId, exception.Message, true)]);
    }

    var candidates = new List<SteamImportCandidate>();
    var diagnostics = new List<SteamImportDiagnostic>();
    foreach (var member in collection.Members)
    {
      cancellationToken.ThrowIfCancellationRequested();
      var memberSourceReference = member.SourceReference ?? member.WorkshopItemId;

      try
      {
        var metadata = await metadataClient.GetWorkshopItemAsync(member.WorkshopItemId, cancellationToken);
        candidates.Add(new SteamImportCandidate(memberSourceReference, metadata.DisplayName));
      }
      catch (Exception exception) when (exception is not OperationCanceledException)
      {
        candidates.Add(new SteamImportCandidate(memberSourceReference));
        diagnostics.Add(new SteamImportDiagnostic(memberSourceReference, exception.Message, true));
      }
    }

    return new SteamImportResult(candidates, diagnostics);
  }
}
