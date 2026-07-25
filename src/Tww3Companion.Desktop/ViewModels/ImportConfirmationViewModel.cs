namespace Tww3Companion.Desktop.ViewModels;

public sealed class ImportConfirmationViewModel : ViewModelBase
{
  private ImportConfirmationSummary? summary;
  private bool isFinalizing;
  private Func<Task>? applyHandler;

  public ImportConfirmationSummary? Summary
  {
    get => summary;
    private set
    {
      summary = value;
      OnPropertyChanged();
    }
  }

  public bool IsFinalizing
  {
    get => isFinalizing;
    internal set
    {
      if (isFinalizing == value)
      {
        return;
      }

      isFinalizing = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanApply));
      ApplyCommand.RaiseCanExecuteChanged();
    }
  }

  public bool CanApply => Summary is not null && !IsFinalizing;

  public ViewModelCommand ApplyCommand { get; }

  public ImportConfirmationViewModel()
  {
    ApplyCommand = new ViewModelCommand(
        () => _ = ApplyAsync(),
        () => CanApply);
  }

  internal void SetSummary(ImportConfirmationSummary value) => Summary = value;

  internal void ClearSummary() => Summary = null;

  internal void SetApplyHandler(Func<Task> handler) => applyHandler = handler;

  private async Task ApplyAsync()
  {
    if (!CanApply || applyHandler is null)
    {
      return;
    }

    await applyHandler();
  }
}
