namespace Tww3Companion.Application.Importing;

internal static class SteamImportAdapter
{
  internal static async Task<SteamImportResult> ParseSingleItemInputAsync(
      string pastedIdsAndUrls,
      ISteamMetadataClient metadataClient,
      CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(pastedIdsAndUrls);
    cancellationToken.ThrowIfCancellationRequested();

    var candidates = new List<SteamImportCandidate>();
    var diagnostics = new List<SteamImportDiagnostic>();
    foreach (var sourceReference in pastedIdsAndUrls.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
      cancellationToken.ThrowIfCancellationRequested();
      if (!TryGetWorkshopItemId(sourceReference, out var workshopItemId))
      {
        diagnostics.Add(new SteamImportDiagnostic(sourceReference, "Steam item input must be a numeric Workshop ID or Workshop URL.", true));
        continue;
      }

      try
      {
        var metadata = await metadataClient.GetWorkshopItemAsync(workshopItemId, cancellationToken);
        candidates.Add(new SteamImportCandidate(sourceReference, metadata.DisplayName));
      }
      catch (Exception exception) when (exception is not OperationCanceledException)
      {
        diagnostics.Add(new SteamImportDiagnostic(sourceReference, exception.Message, true));
      }
    }

    return new SteamImportResult(candidates, diagnostics);
  }

  private static bool TryGetWorkshopItemId(string sourceReference, out string workshopItemId)
  {
    if (sourceReference.All(char.IsAsciiDigit))
    {
      workshopItemId = sourceReference;
      return true;
    }

    if (Uri.TryCreate(sourceReference, UriKind.Absolute, out var uri) &&
        (uri.Host.Equals("steamcommunity.com", StringComparison.OrdinalIgnoreCase) ||
         uri.Host.Equals("www.steamcommunity.com", StringComparison.OrdinalIgnoreCase)) &&
        (uri.AbsolutePath.Equals("/sharedfiles/filedetails/", StringComparison.OrdinalIgnoreCase) ||
         uri.AbsolutePath.Equals("/sharedfiles/filedetails", StringComparison.OrdinalIgnoreCase)))
    {
      var id = uri.Query.TrimStart('?').Split('&')
          .Select(parameter => parameter.Split('=', 2))
          .FirstOrDefault(parameter => parameter.Length == 2 && parameter[0].Equals("id", StringComparison.OrdinalIgnoreCase))?[1];
      if (!string.IsNullOrWhiteSpace(id) && id.All(char.IsAsciiDigit))
      {
        workshopItemId = id;
        return true;
      }
    }

    workshopItemId = string.Empty;
    return false;
  }
}
