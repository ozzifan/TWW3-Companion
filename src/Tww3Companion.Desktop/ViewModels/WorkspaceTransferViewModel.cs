using Tww3Companion.Application.Common;
using Tww3Companion.Application.Workspaces.Transfer;
using Tww3Companion.Desktop.Services;
using Tww3Companion.Domain.Workspaces;

namespace Tww3Companion.Desktop.ViewModels;

public sealed record WorkspaceTransferCompletedEvent(
    WorkspaceRestoreDestination Destination,
    bool Applied,
    string? WorkspacePath,
    string? WorkspaceId,
    string? Message);

public sealed class WorkspaceTransferViewModel : ViewModelBase
{
  private const string FinalizingMessage = "Finalizing — please wait";

  private readonly IWorkspaceTransferCoordinator coordinator;
  private readonly WorkspaceRestoreDestination destination;
  private readonly string? openWorkspacePath;
  private readonly string? openWorkspaceName;
  private InspectedWorkspaceRestore? inspected;
  private string statusMessage = string.Empty;
  private string errorMessage = string.Empty;
  private bool isBusy;
  private bool isFinalizing;
  private bool persistentChangeCommitted;

  public WorkspaceTransferViewModel(
      WorkspaceRestoreDestination destination,
      IWorkspaceTransferCoordinator coordinator,
      string? openWorkspacePath = null,
      string? openWorkspaceName = null)
  {
    this.destination = destination;
    this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    this.openWorkspacePath = openWorkspacePath;
    this.openWorkspaceName = openWorkspaceName;

    SelectExportCommand = new ViewModelCommand(() => _ = InspectAsync(), () => !isBusy);
    RestoreCommand = new ViewModelCommand(() => _ = RestoreAsync(), () => inspected is not null && !isBusy);
    BackCommand = new ViewModelCommand(() => Completed?.Invoke(this, new WorkspaceTransferCompletedEvent(
        destination,
        Applied: false,
        WorkspacePath: null,
        WorkspaceId: null,
        Message: null)));
    CancelCommand = new ViewModelCommand(() => Cancel(), () => CanCancel);
  }

  public event EventHandler<WorkspaceTransferCompletedEvent>? Completed;

  public WorkspaceRestoreDestination Destination => destination;

  public bool IsNewWorkspaceRestore => destination == WorkspaceRestoreDestination.NewWorkspace;

  public string DestinationActionDescription =>
      IsNewWorkspaceRestore
          ? "creates a new Workspace; never merges"
          : "replaces the complete open Workspace; never merges";

  public string? OpenWorkspaceName => openWorkspaceName;

  public bool HasSummary => inspected is not null;

  public string? WorkspaceDisplayName => inspected?.Summary.DisplayName;

  public string? Format => inspected?.Summary.Format;

  public int ModCount => inspected?.Summary.ModCount ?? 0;

  public int CollectionCount => inspected?.Summary.CollectionCount ?? 0;

  public int MembershipCount => inspected?.Summary.MembershipCount ?? 0;

  public string StatusMessage
  {
    get => statusMessage;
    private set
    {
      if (statusMessage == value)
      {
        return;
      }

      statusMessage = value;
      OnPropertyChanged();
    }
  }

  public string ErrorMessage
  {
    get => errorMessage;
    private set
    {
      if (errorMessage == value)
      {
        return;
      }

      errorMessage = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasError));
    }
  }

  public bool HasError => !string.IsNullOrWhiteSpace(errorMessage);

  public bool IsBusy
  {
    get => isBusy;
    private set
    {
      if (isBusy == value)
      {
        return;
      }

      isBusy = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanCancel));
      RaiseCommandStateChanged();
    }
  }

  public bool IsFinalizing
  {
    get => isFinalizing;
    private set
    {
      if (isFinalizing == value)
      {
        return;
      }

      isFinalizing = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanCancel));
      RaiseCommandStateChanged();
    }
  }

  public bool CanCancel => isBusy && !isFinalizing;

  public bool PersistentChangeCommitted => persistentChangeCommitted;

  public ViewModelCommand SelectExportCommand { get; }

  public ViewModelCommand RestoreCommand { get; }

  public ViewModelCommand BackCommand { get; }

  public ViewModelCommand CancelCommand { get; }

  public void BeginInspect() => _ = InspectAsync();

  public void SetInspectedForTest(InspectedWorkspaceRestore value)
  {
    inspected = value;
    OnPropertyChanged(nameof(HasSummary));
    OnPropertyChanged(nameof(WorkspaceDisplayName));
    OnPropertyChanged(nameof(Format));
    OnPropertyChanged(nameof(ModCount));
    OnPropertyChanged(nameof(CollectionCount));
    OnPropertyChanged(nameof(MembershipCount));
    RestoreCommand.RaiseCanExecuteChanged();
  }

  private async Task InspectAsync()
  {
    if (isBusy)
    {
      return;
    }

    ClearInspectedState();
    IsBusy = true;
    StatusMessage = "Inspecting backup…";
    try
    {
      var result = await coordinator.InspectRestoreAsync(CancellationToken.None);
      if (result is OperationResult<InspectedWorkspaceRestore>.Failure failure)
      {
        if (failure.Error.Code == "workspace.transfer.cancelled")
        {
          StatusMessage = string.Empty;
          return;
        }

        ErrorMessage = failure.Error.Message;
        StatusMessage = string.Empty;
        return;
      }

      inspected = ((OperationResult<InspectedWorkspaceRestore>.Success)result).Value;
      OnPropertyChanged(nameof(HasSummary));
      OnPropertyChanged(nameof(WorkspaceDisplayName));
      OnPropertyChanged(nameof(Format));
      OnPropertyChanged(nameof(ModCount));
      OnPropertyChanged(nameof(CollectionCount));
      OnPropertyChanged(nameof(MembershipCount));
      StatusMessage = "Backup inspected. Review the summary and choose Restore.";
      ErrorMessage = string.Empty;
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
      ErrorMessage = exception.Message;
      StatusMessage = string.Empty;
    }
    finally
    {
      IsBusy = false;
    }
  }

  private async Task RestoreAsync()
  {
    if (inspected is null || isBusy)
    {
      return;
    }

    IsBusy = true;
    IsFinalizing = true;
    StatusMessage = FinalizingMessage;
    try
    {
      OperationResult<Workspace> result = destination == WorkspaceRestoreDestination.NewWorkspace
          ? await coordinator.RestoreNewAsync(inspected, CancellationToken.None)
          : await coordinator.ReplaceOpenAsync(
              inspected,
              openWorkspacePath ?? throw new InvalidOperationException("Open Workspace path is required."),
              CancellationToken.None);

      if (result is OperationResult<Workspace>.Failure failure)
      {
        if (failure.Error.Code is "workspace.transfer.cancelled" or "workspace.restore.unconfirmed")
        {
          StatusMessage = inspected is null ? string.Empty : "Backup inspected. Review the summary and choose Restore.";
          return;
        }

        persistentChangeCommitted = failure.Error.PersistentChangeCommitted;
        ErrorMessage = failure.Error.Message;
        StatusMessage = failure.Error.PersistentChangeCommitted
            ? "Restore failed after changes were committed."
            : "Restore failed. No changes were made.";
        return;
      }

      var workspace = ((OperationResult<Workspace>.Success)result).Value;
      StatusMessage = IsNewWorkspaceRestore
          ? "Workspace restored successfully."
          : "Open Workspace replaced successfully.";
      ErrorMessage = string.Empty;
      Completed?.Invoke(this, new WorkspaceTransferCompletedEvent(
          destination,
          Applied: true,
          WorkspacePath: destination == WorkspaceRestoreDestination.NewWorkspace
              ? coordinator.LastRestoreDestinationPath
              : openWorkspacePath,
          WorkspaceId: workspace.Id.ToString(),
          Message: StatusMessage));
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
      ErrorMessage = exception.Message;
      StatusMessage = "Restore failed. No changes were made.";
    }
    finally
    {
      IsBusy = false;
      IsFinalizing = false;
    }
  }

  private void Cancel()
  {
    if (!CanCancel)
    {
      return;
    }

    IsBusy = false;
    StatusMessage = string.Empty;
  }

  private void ClearInspectedState()
  {
    inspected = null;
    OnPropertyChanged(nameof(HasSummary));
    OnPropertyChanged(nameof(WorkspaceDisplayName));
    OnPropertyChanged(nameof(Format));
    OnPropertyChanged(nameof(ModCount));
    OnPropertyChanged(nameof(CollectionCount));
    OnPropertyChanged(nameof(MembershipCount));
    ErrorMessage = string.Empty;
    RestoreCommand.RaiseCanExecuteChanged();
  }

  private void RaiseCommandStateChanged()
  {
    SelectExportCommand.RaiseCanExecuteChanged();
    RestoreCommand.RaiseCanExecuteChanged();
    CancelCommand.RaiseCanExecuteChanged();
  }
}
