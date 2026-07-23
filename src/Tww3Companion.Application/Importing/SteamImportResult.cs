namespace Tww3Companion.Application.Importing;

public sealed record SteamImportResult(
    IReadOnlyList<SteamImportCandidate> Candidates,
    IReadOnlyList<SteamImportDiagnostic> Diagnostics)
{
  public bool Applied { get; init; }
}
