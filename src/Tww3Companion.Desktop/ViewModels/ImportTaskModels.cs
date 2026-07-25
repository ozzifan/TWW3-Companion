using Tww3Companion.Application.Importing;

namespace Tww3Companion.Desktop.ViewModels;

public enum ImportTaskStage
{
  Source,
  Destination,
  Preview,
  Confirmation,
  Finalizing,
  Complete
}

public sealed record ImportLaunchContext(
    bool IsNewWorkspace,
    string? WorkspaceId,
    string? WorkspacePath,
    IReadOnlyList<CollectionSummary> Collections,
    string? SelectedCollectionId);

public sealed record ImportPreviewFingerprint(
    ImportSourceKind SourceKind,
    string SourceDigest,
    ImportTargetContext TargetContext);

public enum ImportPreviewFilter
{
  All,
  Additions,
  Enrichments,
  Existing,
  SuggestedMatches,
  Conflicts,
  Warnings,
  Skipped
}

public sealed record ImportConfirmationSummary(
    int ModsCreated,
    int ModsEnriched,
    int ExistingModsUnchanged,
    int CollectionsCreated,
    int MembershipsAdded,
    int ExistingMembershipsUnchanged,
    int CandidatesSkipped,
    int WarningsRemaining);

public sealed record ImportTaskCompletedEvent(ImportOutcome Outcome);

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
