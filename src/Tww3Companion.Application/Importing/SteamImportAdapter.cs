namespace Tww3Companion.Application.Importing;

internal static class SteamImportAdapter
{
  internal static SteamImportResult ParseSingleItemInput(string pastedIdsAndUrls, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(pastedIdsAndUrls);
    cancellationToken.ThrowIfCancellationRequested();

    var input = pastedIdsAndUrls.Trim();
    if (input.Length == 0) return new SteamImportResult([], []);

    return new SteamImportResult(
        [new SteamImportCandidate(input)],
        [new SteamImportDiagnostic(input, "Steam input read.")]);
  }
}
