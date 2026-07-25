using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using Tww3Companion.Application.Importing;

namespace Tww3Companion.Desktop.ViewModels;

public sealed class ImportPreviewRowViewModel
{
  internal ImportPreviewRowViewModel(
      ImportCandidate candidate,
      ImportPreviewOperation operation,
      IReadOnlyList<ImportValidationIssue> validationIssues)
  {
    Candidate = candidate;
    Operation = operation;
    CandidateId = candidate.CandidateId;
    DisplayName = candidate.DisplayName
        ?? candidate.LinkedModId
        ?? candidate.CandidateId;
    LibraryAction = operation.LibraryAction;
    MembershipAction = operation.MembershipAction;
    HasWarning = validationIssues.Any(issue =>
        string.Equals(issue.CandidateId, candidate.CandidateId, StringComparison.Ordinal) &&
        issue.Code.Contains("warning", StringComparison.OrdinalIgnoreCase));
    IsBlocking = ImportPreviewRules.IsBlocking(candidate, operation, validationIssues);
    IsSkipped = candidate.IsSkipped || operation.LibraryAction == ImportLibraryAction.Skip;
  }

  internal ImportCandidate Candidate { get; }

  internal ImportPreviewOperation Operation { get; }

  public string CandidateId { get; }

  public string DisplayName { get; }

  public ImportLibraryAction LibraryAction { get; }

  public ImportMembershipAction MembershipAction { get; }

  public bool HasWarning { get; }

  public bool IsBlocking { get; }

  public bool IsSkipped { get; }
}

public sealed class ImportPreviewViewModel : ViewModelBase
{
  private ImportPreview? preview;
  private ImportPreviewFilter selectedFilter = ImportPreviewFilter.All;
  private readonly ObservableCollection<ImportPreviewRowViewModel> rows = [];

  public ImportPreview? Preview => preview;

  public IReadOnlyList<ImportPreviewRowViewModel> Rows => rows;

  public ImportPreviewFilter SelectedFilter
  {
    get => selectedFilter;
    set
    {
      if (selectedFilter == value)
      {
        return;
      }

      selectedFilter = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(FilteredRows));
    }
  }

  public IReadOnlyList<ImportPreviewRowViewModel> FilteredRows =>
      rows.Where(MatchesSelectedFilter).ToArray();

  public bool CanContinue =>
      preview is not null &&
      rows.All(row => !row.IsBlocking);

  internal event Action<ImportPreview>? Loaded;

  public void Load(ImportPreview value)
  {
    preview = value;
    rows.Clear();

    var operations = value.Operations ?? [];
    var validationIssues = value.ValidationIssues ?? [];
    var operationsById = operations.ToDictionary(
        operation => operation.CandidateId,
        StringComparer.Ordinal);

    foreach (var candidate in value.Candidates)
    {
      if (!operationsById.TryGetValue(candidate.CandidateId, out var operation))
      {
        operation = new ImportPreviewOperation(
            candidate.CandidateId,
            ImportPreviewRules.InferLibraryAction(candidate),
            ImportPreviewRules.InferMembershipAction(candidate, value.TargetContext),
            []);
      }

      rows.Add(new ImportPreviewRowViewModel(candidate, operation, validationIssues));
    }

    OnPropertyChanged(nameof(Preview));
    OnPropertyChanged(nameof(Rows));
    OnPropertyChanged(nameof(FilteredRows));
    OnPropertyChanged(nameof(CanContinue));
    Loaded?.Invoke(value);
  }

  public ImportConfirmationSummary BuildConfirmationSummary()
  {
    if (preview is null)
    {
      return new ImportConfirmationSummary(0, 0, 0, 0, 0, 0, 0, 0);
    }

    var creates = rows.Count(row => row.LibraryAction == ImportLibraryAction.Create && !row.IsSkipped);
    var enrichments = rows.Count(row => row.LibraryAction == ImportLibraryAction.Enrich && !row.IsSkipped);
    var existing = rows.Count(row => row.LibraryAction == ImportLibraryAction.Existing && !row.IsSkipped);
    var skipped = rows.Count(row => row.IsSkipped);
    var membershipsAdded = rows.Count(row =>
        !row.IsSkipped && row.MembershipAction == ImportMembershipAction.Add);
    var membershipsExisting = rows.Count(row =>
        !row.IsSkipped && row.MembershipAction == ImportMembershipAction.Existing);
    var collectionsCreated = preview.TargetContext switch
    {
      ImportTargetContext.NewWorkspace { MembershipDestination: ImportMembershipDestination.NewCollection } => 1,
      ImportTargetContext.CurrentWorkspace { MembershipDestination: ImportMembershipDestination.NewCollection } => 1,
      _ => 0
    };

    return new ImportConfirmationSummary(
        creates,
        enrichments,
        existing,
        collectionsCreated,
        membershipsAdded,
        membershipsExisting,
        skipped,
        preview.WarningCount);
  }

  internal ImportPreviewRowViewModel? GetNextBlockingRow(string? afterCandidateId = null)
  {
    var blockingRows = rows.Where(row => row.IsBlocking).ToArray();
    if (blockingRows.Length == 0)
    {
      return null;
    }

    if (string.IsNullOrWhiteSpace(afterCandidateId))
    {
      return blockingRows[0];
    }

    var index = Array.FindIndex(
        blockingRows,
        row => string.Equals(row.CandidateId, afterCandidateId, StringComparison.Ordinal));
    return index >= 0 && index + 1 < blockingRows.Length
        ? blockingRows[index + 1]
        : null;
  }

  internal ImportPreviewRowViewModel? GetActiveBlockingRow() =>
      rows.FirstOrDefault(row => row.IsBlocking);

  private bool MatchesSelectedFilter(ImportPreviewRowViewModel row) =>
      selectedFilter switch
      {
        ImportPreviewFilter.All => true,
        ImportPreviewFilter.Additions => row.LibraryAction == ImportLibraryAction.Create,
        ImportPreviewFilter.Enrichments => row.LibraryAction == ImportLibraryAction.Enrich,
        ImportPreviewFilter.Existing => row.LibraryAction == ImportLibraryAction.Existing,
        ImportPreviewFilter.SuggestedMatches => row.LibraryAction == ImportLibraryAction.SuggestedMatch,
        ImportPreviewFilter.Conflicts =>
            row.LibraryAction == ImportLibraryAction.Conflict || row.IsBlocking,
        ImportPreviewFilter.Warnings => row.HasWarning,
        ImportPreviewFilter.Skipped => row.IsSkipped,
        _ => true
      };
}

internal static class ImportPreviewRules
{
  public static bool IsBlocking(
      ImportCandidate candidate,
      ImportPreviewOperation operation,
      IReadOnlyList<ImportValidationIssue> validationIssues)
  {
    if (candidate.IsSkipped)
    {
      return false;
    }

    if (validationIssues.Any(issue =>
            string.Equals(issue.CandidateId, candidate.CandidateId, StringComparison.Ordinal) &&
            string.Equals(issue.Code, "import.source.owner.conflict", StringComparison.Ordinal)))
    {
      return true;
    }

    if (operation.LibraryAction == ImportLibraryAction.Conflict)
    {
      return true;
    }

    return string.IsNullOrWhiteSpace(candidate.LinkedModId) &&
        string.IsNullOrWhiteSpace(candidate.DisplayName);
  }

  public static ImportLibraryAction InferLibraryAction(ImportCandidate candidate)
  {
    if (candidate.IsSkipped)
    {
      return ImportLibraryAction.Skip;
    }

    if (!string.IsNullOrWhiteSpace(candidate.LinkedModId))
    {
      return ImportLibraryAction.Existing;
    }

    if (!string.IsNullOrWhiteSpace(candidate.SuggestedModId))
    {
      return ImportLibraryAction.SuggestedMatch;
    }

    return ImportLibraryAction.Create;
  }

  public static ImportMembershipAction InferMembershipAction(
      ImportCandidate candidate,
      ImportTargetContext targetContext)
  {
    if (candidate.IsSkipped)
    {
      return ImportMembershipAction.Skip;
    }

    return targetContext switch
    {
      ImportTargetContext.NewWorkspace { MembershipDestination: ImportMembershipDestination.LibraryOnly } =>
          ImportMembershipAction.None,
      ImportTargetContext.CurrentWorkspace { MembershipDestination: ImportMembershipDestination.LibraryOnly } =>
          ImportMembershipAction.None,
      _ => ImportMembershipAction.Add
    };
  }

  public static string ComputeSourceDigest(
      ImportSourceKind kind,
      string inputText,
      string? documentName)
  {
    var payload = $"{kind}\0{documentName ?? string.Empty}\0{inputText}";
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
    return Convert.ToHexString(hash);
  }
}
