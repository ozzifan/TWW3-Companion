namespace Tww3Companion.Desktop.ViewModels;

public enum ImportSourceKind
{
  Markdown,
  SteamCollection,
  SteamItems
}

public sealed record ImportSourceDocument(string Name, string Text);

public sealed record ImportSourceRequest(
    ImportSourceKind Kind,
    string InputText,
    string? DocumentName,
    bool RequestMetadata);

public sealed record ImportTaskDiagnostic(
    string Code,
    string Message,
    bool IsBlocking,
    string SafeNextAction);

public sealed record ImportSourceLoadResult(
    IReadOnlyList<object> Candidates,
    IReadOnlyList<ImportTaskDiagnostic> Diagnostics,
    IReadOnlyList<string> DisclosedWorkshopIds);
