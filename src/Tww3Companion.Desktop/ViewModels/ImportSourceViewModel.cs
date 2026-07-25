using Tww3Companion.Application.Importing;
using Tww3Companion.Desktop.Services;

namespace Tww3Companion.Desktop.ViewModels;

public sealed class ImportSourceViewModel : ViewModelBase
{
  private readonly IImportSourceFileService fileService;
  private ImportSourceKind selectedKind = ImportSourceKind.Markdown;
  private string inputText = string.Empty;
  private string? selectedDocumentName;
  private IReadOnlyList<string> disclosedWorkshopIds = [];
  private IReadOnlyList<ImportTaskDiagnostic> diagnostics = [];
  private bool isBusy;
  private Func<Task>? continueHandler;

  public ImportSourceViewModel(IImportSourceFileService fileService)
  {
    this.fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
    ChooseFileCommand = new ViewModelCommand(() => _ = ChooseFileAsync());
    ContinueCommand = new ViewModelCommand(
        () => _ = ContinueAsync(),
        () => CanContinue);
    SelectMarkdownCommand = new ViewModelCommand(() => Select(ImportSourceKind.Markdown));
    SelectSteamCollectionCommand = new ViewModelCommand(() => Select(ImportSourceKind.SteamCollection));
    SelectSteamItemsCommand = new ViewModelCommand(() => Select(ImportSourceKind.SteamItems));
  }

  public ImportSourceKind SelectedKind
  {
    get => selectedKind;
    private set
    {
      if (selectedKind == value)
      {
        return;
      }

      selectedKind = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanContinue));
      RaiseContinueCommandChanged();
    }
  }

  public string InputText
  {
    get => inputText;
    set
    {
      if (string.Equals(inputText, value, StringComparison.Ordinal))
      {
        return;
      }

      inputText = value ?? string.Empty;
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanContinue));
      RaiseContinueCommandChanged();
    }
  }

  public string? SelectedDocumentName
  {
    get => selectedDocumentName;
    private set
    {
      if (string.Equals(selectedDocumentName, value, StringComparison.Ordinal))
      {
        return;
      }

      selectedDocumentName = value;
      OnPropertyChanged();
    }
  }

  public IReadOnlyList<string> DisclosedWorkshopIds
  {
    get => disclosedWorkshopIds;
    private set
    {
      disclosedWorkshopIds = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(HasDisclosedWorkshopIds));
    }
  }

  public bool HasDisclosedWorkshopIds => disclosedWorkshopIds.Count > 0;

  public IReadOnlyList<ImportTaskDiagnostic> Diagnostics
  {
    get => diagnostics;
    private set
    {
      diagnostics = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanContinue));
      RaiseContinueCommandChanged();
    }
  }

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
      OnPropertyChanged(nameof(CanContinue));
      RaiseContinueCommandChanged();
    }
  }

  public bool CanContinue =>
      !IsBusy &&
      !string.IsNullOrWhiteSpace(InputText) &&
      !Diagnostics.Any(diagnostic => diagnostic.IsBlocking);

  public ViewModelCommand ChooseFileCommand { get; }

  public ViewModelCommand ContinueCommand { get; }

  public ViewModelCommand SelectMarkdownCommand { get; }

  public ViewModelCommand SelectSteamCollectionCommand { get; }

  public ViewModelCommand SelectSteamItemsCommand { get; }

  internal void Select(ImportSourceKind kind) => SelectedKind = kind;

  internal void SetContinueHandler(Func<Task> handler) => continueHandler = handler;

  internal void ApplyLoadResult(ImportSourceLoadResult result)
  {
    DisclosedWorkshopIds = result.DisclosedWorkshopIds;
    Diagnostics = result.Diagnostics;
  }

  internal void ResetDiagnostics()
  {
    DisclosedWorkshopIds = [];
    Diagnostics = [];
  }

  private async Task ChooseFileAsync()
  {
    if (IsBusy)
    {
      return;
    }

    IsBusy = true;
    try
    {
      var document = await fileService.ChooseTextFileAsync(CancellationToken.None);
      if (document is null)
      {
        return;
      }

      SelectedDocumentName = document.Name;
      InputText = document.Text;
    }
    finally
    {
      IsBusy = false;
    }
  }

  private async Task ContinueAsync()
  {
    if (!CanContinue || continueHandler is null)
    {
      return;
    }

    await continueHandler();
  }

  private void RaiseContinueCommandChanged() => ContinueCommand.RaiseCanExecuteChanged();
}
