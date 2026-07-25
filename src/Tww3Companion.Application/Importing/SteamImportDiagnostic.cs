namespace Tww3Companion.Application.Importing;

public sealed record SteamImportDiagnostic(string SourceReference, string Message, bool IsLookupFailure = false)
{
  public bool IsWarning => IsLookupFailure;
}
