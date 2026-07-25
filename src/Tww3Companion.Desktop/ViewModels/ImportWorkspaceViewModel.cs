using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Tww3Companion.Application.Importing;
using Tww3Companion.Desktop.Services;

namespace Tww3Companion.Desktop.ViewModels;

public sealed class ImportWorkspaceViewModel : ViewModelBase
{
  private readonly IImportTaskCoordinator coordinator;
  private readonly ILogger<ImportWorkspaceViewModel> logger;
  private readonly ImportLaunchContext launchContext;
  private ImportTaskStage stage = ImportTaskStage.Source;
  private ImportPreview? cachedPreview;
  private ImportPreviewFingerprint? cachedFingerprint;
  private IReadOnlyList<object>? cachedCandidates;
  private bool requiresDiscardConfirmation;
  private bool discardConfirmed;

  public ImportWorkspaceViewModel(
      ImportLaunchContext launchContext,
      IImportTaskCoordinator coordinator,
      IImportSourceFileService fileService,
      ILogger<ImportWorkspaceViewModel>? logger = null)
  {
    this.launchContext = launchContext ?? throw new ArgumentNullException(nameof(launchContext));
    this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    this.logger = logger ?? NullLogger<ImportWorkspaceViewModel>.Instance;

    Source = new ImportSourceViewModel(fileService);
    Destination = new ImportDestinationViewModel(launchContext);
    Preview = new ImportPreviewViewModel();
    Resolution = new ImportResolutionViewModel(coordinator, Preview);
    Confirmation = new ImportConfirmationViewModel();

    Source.SetContinueHandler(() => HandleSourceContinueAsync(CancellationToken.None));
    Confirmation.SetApplyHandler(() => ApplyAsync(CancellationToken.None));
  }

  public event EventHandler<ImportTaskCompletedEvent>? Completed;

  public ImportTaskStage Stage
  {
    get => stage;
    private set
    {
      if (stage == value)
      {
        return;
      }

      stage = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanGoBack));
      OnPropertyChanged(nameof(CanCancel));
    }
  }

  public ImportSourceViewModel Source { get; }

  public ImportDestinationViewModel Destination { get; }

  public ImportPreviewViewModel Preview { get; }

  public ImportResolutionViewModel Resolution { get; }

  public ImportConfirmationViewModel Confirmation { get; }

  public bool RequiresDiscardConfirmation
  {
    get => requiresDiscardConfirmation && !discardConfirmed;
    private set
    {
      if (requiresDiscardConfirmation == value)
      {
        return;
      }

      requiresDiscardConfirmation = value;
      OnPropertyChanged();
    }
  }

  public bool CanGoBack =>
      stage is ImportTaskStage.Destination or ImportTaskStage.Preview or ImportTaskStage.Confirmation;

  public bool CanCancel =>
      stage is not ImportTaskStage.Finalizing and not ImportTaskStage.Complete;

  public void OpenDestination()
  {
    if (stage != ImportTaskStage.Source)
    {
      return;
    }

    Destination.ApplySuggestionIfNeeded(Source.SelectedKind);
    Stage = ImportTaskStage.Destination;
  }

  public void GoBack()
  {
    Stage = stage switch
    {
      ImportTaskStage.Confirmation => ImportTaskStage.Preview,
      ImportTaskStage.Preview => ImportTaskStage.Destination,
      ImportTaskStage.Destination => ImportTaskStage.Source,
      _ => stage
    };
  }

  public async Task ContinueFromDestinationAsync(CancellationToken cancellationToken = default)
  {
    if (stage != ImportTaskStage.Destination || !Destination.CanContinue)
    {
      return;
    }

    var fingerprint = BuildFingerprint();
    if (cachedFingerprint is not null &&
        cachedPreview is not null &&
        fingerprint.Equals(cachedFingerprint))
    {
      Preview.Load(cachedPreview);
      Resolution.SyncActiveRow();
      InvalidateConfirmation();
      Stage = ImportTaskStage.Preview;
      return;
    }

    var loadResult = await coordinator.LoadSourceAsync(
        new ImportSourceRequest(
            Source.SelectedKind,
            Source.InputText,
            Source.SelectedDocumentName,
            RequestMetadata: true),
        cancellationToken);
    Source.ApplyLoadResult(loadResult);

    if (loadResult.Diagnostics.Any(diagnostic => diagnostic.IsBlocking))
    {
      return;
    }

    var preview = await coordinator.BuildPreviewAsync(
        Destination.BuildTargetContext(),
        loadResult.Candidates,
        cancellationToken);

    cachedPreview = preview;
    cachedFingerprint = fingerprint;
    cachedCandidates = loadResult.Candidates;
    Preview.Load(preview);
    Resolution.SyncActiveRow();
    InvalidateConfirmation();
    Stage = ImportTaskStage.Preview;
  }

  public Task ContinueFromPreviewAsync(CancellationToken cancellationToken = default)
  {
    if (stage != ImportTaskStage.Preview || !Preview.CanContinue)
    {
      return Task.CompletedTask;
    }

    Confirmation.SetSummary(Preview.BuildConfirmationSummary());
    Stage = ImportTaskStage.Confirmation;
    return Task.CompletedTask;
  }

  public async Task ApplyAsync(CancellationToken cancellationToken = default)
  {
    if (stage != ImportTaskStage.Confirmation || Preview.Preview is null)
    {
      return;
    }

    Stage = ImportTaskStage.Finalizing;
    Confirmation.IsFinalizing = true;
    OnPropertyChanged(nameof(CanGoBack));
    OnPropertyChanged(nameof(CanCancel));

    try
    {
      var outcome = await coordinator.ApplyAsync(Preview.Preview, cancellationToken);
      Stage = ImportTaskStage.Complete;
      Completed?.Invoke(this, new ImportTaskCompletedEvent(outcome));
    }
    catch (Exception exception)
    {
      logger.LogError(
          exception,
          "Import apply failed for workspace task stage {Stage}.",
          stage);
      Confirmation.IsFinalizing = false;
      Stage = ImportTaskStage.Preview;
      OnPropertyChanged(nameof(CanGoBack));
      OnPropertyChanged(nameof(CanCancel));
      throw;
    }
  }

  public bool TryDismiss()
  {
    if (cachedPreview is null)
    {
      return true;
    }

    if (discardConfirmed)
    {
      ClearSession();
      discardConfirmed = false;
      return true;
    }

    RequiresDiscardConfirmation = true;
    return false;
  }

  public void ConfirmDiscard() => discardConfirmed = true;

  private Task HandleSourceContinueAsync(CancellationToken cancellationToken)
  {
    OpenDestination();
    return Task.CompletedTask;
  }

  private ImportPreviewFingerprint BuildFingerprint() =>
      new(
          Source.SelectedKind,
          ImportPreviewRules.ComputeSourceDigest(
              Source.SelectedKind,
              Source.InputText,
              Source.SelectedDocumentName),
          Destination.BuildTargetContext());

  private void InvalidateConfirmation() => Confirmation.ClearSummary();

  private void ClearSession()
  {
    cachedPreview = null;
    cachedFingerprint = null;
    cachedCandidates = null;
    RequiresDiscardConfirmation = false;
    discardConfirmed = false;
    Preview.Load(new ImportPreview(
        Destination.BuildTargetContext(),
        [],
        Applied: false));
    Resolution.SyncActiveRow();
    InvalidateConfirmation();
  }
}
