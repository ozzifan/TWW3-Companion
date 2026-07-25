using Tww3Companion.Application.Importing;
using Tww3Companion.Desktop.Services;

namespace Tww3Companion.Desktop.ViewModels;

public sealed class ImportResolutionViewModel : ViewModelBase
{
  private readonly IImportTaskCoordinator coordinator;
  private readonly ImportPreviewViewModel previewViewModel;
  private ImportPreviewRowViewModel? activeRow;
  private string? selectedScalarValue;
  private string draftDisplayName = string.Empty;

  public ImportResolutionViewModel(
      IImportTaskCoordinator coordinator,
      ImportPreviewViewModel previewViewModel)
  {
    this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    this.previewViewModel = previewViewModel ?? throw new ArgumentNullException(nameof(previewViewModel));
    LinkToOwnerCommand = new ViewModelCommand(
        () => _ = LinkToModAsync(SourceOwnerModId ?? string.Empty, CancellationToken.None),
        () => CanLinkToOwner);
    CreateCommand = new ViewModelCommand(
        () => _ = CreateWithDisplayNameAsync(DraftDisplayName, CancellationToken.None),
        () => CanCreate && !string.IsNullOrWhiteSpace(DraftDisplayName));
    SkipCommand = new ViewModelCommand(
        () => _ = SkipAsync(CancellationToken.None),
        () => CanSkip);
    ApplyScalarCommand = new ViewModelCommand(
        () => _ = ApplyScalarSelectionAsync(CancellationToken.None),
        () => CanApplyResolution);
    SyncActiveRow();
  }

  public string? ActiveCandidateId => activeRow?.CandidateId;

  public string? SourceOwnerModId =>
      activeRow is null ? null : ExtractOwnerModId(activeRow.Operation.Issues);

  public IReadOnlyList<string> CompetingScalarValues =>
      activeRow is null ? [] : ParseCompetingValues(activeRow.Operation.Issues);

  public string? SelectedScalarValue
  {
    get => selectedScalarValue;
    set
    {
      if (string.Equals(selectedScalarValue, value, StringComparison.Ordinal))
      {
        return;
      }

      selectedScalarValue = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanApplyResolution));
    }
  }

  public bool CanLinkToOwner =>
      activeRow is not null &&
      IsSourceOwnerConflict(activeRow) &&
      !string.IsNullOrWhiteSpace(SourceOwnerModId);

  public bool CanCreate =>
      activeRow is not null &&
      !IsSourceOwnerConflict(activeRow) &&
      !IsScalarConflict(activeRow);

  public bool CanSkip => activeRow is not null;

  public bool CanChooseScalarValue =>
      activeRow is not null && IsScalarConflict(activeRow);

  public bool CanApplyResolution =>
      CanChooseScalarValue && !string.IsNullOrWhiteSpace(SelectedScalarValue);

  public string DraftDisplayName
  {
    get => draftDisplayName;
    set
    {
      if (string.Equals(draftDisplayName, value, StringComparison.Ordinal))
      {
        return;
      }

      draftDisplayName = value ?? string.Empty;
      OnPropertyChanged();
      CreateCommand.RaiseCanExecuteChanged();
    }
  }

  public ViewModelCommand LinkToOwnerCommand { get; }

  public ViewModelCommand CreateCommand { get; }

  public ViewModelCommand SkipCommand { get; }

  public ViewModelCommand ApplyScalarCommand { get; }

  internal void SyncActiveRow()
  {
    activeRow = previewViewModel.GetActiveBlockingRow();
    selectedScalarValue = null;
    draftDisplayName = string.Empty;
    OnPropertyChanged(nameof(ActiveCandidateId));
    OnPropertyChanged(nameof(SourceOwnerModId));
    OnPropertyChanged(nameof(CompetingScalarValues));
    OnPropertyChanged(nameof(CanLinkToOwner));
    OnPropertyChanged(nameof(CanCreate));
    OnPropertyChanged(nameof(CanSkip));
    OnPropertyChanged(nameof(CanChooseScalarValue));
    OnPropertyChanged(nameof(CanApplyResolution));
    OnPropertyChanged(nameof(DraftDisplayName));
    LinkToOwnerCommand.RaiseCanExecuteChanged();
    CreateCommand.RaiseCanExecuteChanged();
    SkipCommand.RaiseCanExecuteChanged();
    ApplyScalarCommand.RaiseCanExecuteChanged();
  }

  public Task LinkToModAsync(string modId, CancellationToken cancellationToken = default)
  {
    if (activeRow is null)
    {
      return Task.CompletedTask;
    }

    var resolved = ImportCandidate.Linked(
        activeRow.CandidateId,
        modId,
        activeRow.Candidate.SourceReference);
    return ResolveAndAdvanceAsync(resolved, cancellationToken);
  }

  public Task CreateWithDisplayNameAsync(string displayName, CancellationToken cancellationToken = default)
  {
    if (activeRow is null)
    {
      return Task.CompletedTask;
    }

    var resolved = ImportCandidate.CreateWithDisplayName(
        activeRow.CandidateId,
        displayName,
        activeRow.Candidate.SourceReference);
    return ResolveAndAdvanceAsync(resolved, cancellationToken);
  }

  public Task SkipAsync(CancellationToken cancellationToken = default)
  {
    if (activeRow is null)
    {
      return Task.CompletedTask;
    }

    return ResolveAndAdvanceAsync(
        ImportCandidate.Skipped(activeRow.CandidateId),
        cancellationToken);
  }

  public Task ApplyScalarSelectionAsync(CancellationToken cancellationToken = default)
  {
    if (activeRow is null || string.IsNullOrWhiteSpace(SelectedScalarValue))
    {
      return Task.CompletedTask;
    }

    var resolved = ImportCandidate.CreateWithDisplayName(
        activeRow.CandidateId,
        SelectedScalarValue,
        activeRow.Candidate.SourceReference);
    return ResolveAndAdvanceAsync(resolved, cancellationToken);
  }

  private async Task ResolveAndAdvanceAsync(
      ImportCandidate resolvedCandidate,
      CancellationToken cancellationToken)
  {
    if (previewViewModel.Preview is null || activeRow is null)
    {
      return;
    }

    var currentCandidateId = activeRow.CandidateId;
    var updatedPreview = await coordinator.ResolveAsync(
        previewViewModel.Preview,
        resolvedCandidate,
        cancellationToken);

    previewViewModel.Load(updatedPreview);
    activeRow = previewViewModel.GetNextBlockingRow(currentCandidateId)
        ?? previewViewModel.GetActiveBlockingRow();
    selectedScalarValue = null;
    draftDisplayName = string.Empty;

    OnPropertyChanged(nameof(ActiveCandidateId));
    OnPropertyChanged(nameof(SourceOwnerModId));
    OnPropertyChanged(nameof(CompetingScalarValues));
    OnPropertyChanged(nameof(CanLinkToOwner));
    OnPropertyChanged(nameof(CanCreate));
    OnPropertyChanged(nameof(CanSkip));
    OnPropertyChanged(nameof(CanChooseScalarValue));
    OnPropertyChanged(nameof(CanApplyResolution));
    OnPropertyChanged(nameof(DraftDisplayName));
    LinkToOwnerCommand.RaiseCanExecuteChanged();
    CreateCommand.RaiseCanExecuteChanged();
    SkipCommand.RaiseCanExecuteChanged();
    ApplyScalarCommand.RaiseCanExecuteChanged();
  }

  private static bool IsSourceOwnerConflict(ImportPreviewRowViewModel row) =>
      row.Operation.Issues.Any(issue =>
          string.Equals(issue.Code, "import.source.owner.conflict", StringComparison.Ordinal));

  private static bool IsScalarConflict(ImportPreviewRowViewModel row) =>
      row.Operation.Issues.Any(issue =>
          string.Equals(issue.Code, "import.scalar.conflict", StringComparison.Ordinal));

  private static string? ExtractOwnerModId(IReadOnlyList<ImportValidationIssue> issues)
  {
    var issue = issues.FirstOrDefault(candidateIssue =>
        string.Equals(candidateIssue.Code, "import.source.owner.conflict", StringComparison.Ordinal));
    if (issue is null)
    {
      return null;
    }

    const string marker = "Mod ";
    var index = issue.Message.LastIndexOf(marker, StringComparison.Ordinal);
    if (index < 0)
    {
      return null;
    }

    var ownerModId = issue.Message[(index + marker.Length)..].Trim().TrimEnd('.');
    return string.IsNullOrWhiteSpace(ownerModId) ? null : ownerModId;
  }

  private static IReadOnlyList<string> ParseCompetingValues(IReadOnlyList<ImportValidationIssue> issues)
  {
    var issue = issues.FirstOrDefault(candidateIssue =>
        string.Equals(candidateIssue.Code, "import.scalar.conflict", StringComparison.Ordinal));
    if (issue is null || string.IsNullOrWhiteSpace(issue.Message))
    {
      return [];
    }

    return issue.Message
        .Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
  }
}
