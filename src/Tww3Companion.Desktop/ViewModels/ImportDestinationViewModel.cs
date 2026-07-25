using Tww3Companion.Application.Importing;

namespace Tww3Companion.Desktop.ViewModels;

public sealed class ImportDestinationViewModel : ViewModelBase
{
  private readonly ImportLaunchContext launchContext;
  private string workspaceDisplayName = "Imported Workspace";
  private string workspacePath = string.Empty;
  private string? selectedCollectionId;
  private string newCollectionName = "Imported Collection";
  private bool isLibraryOnly;
  private bool isExistingCollection;
  private bool isNewCollection;
  private bool destinationInitialized;
  private bool userSelectedDestination;

  public ImportDestinationViewModel(ImportLaunchContext launchContext)
  {
    this.launchContext = launchContext ?? throw new ArgumentNullException(nameof(launchContext));
    workspacePath = launchContext.WorkspacePath ?? string.Empty;
  }

  public bool ShowsWorkspaceDetails => launchContext.IsNewWorkspace;

  public bool HasExistingCollectionOption => !launchContext.IsNewWorkspace;

  public IReadOnlyList<CollectionSummary> Collections => launchContext.Collections;

  public string WorkspaceDisplayName
  {
    get => workspaceDisplayName;
    set
    {
      if (string.Equals(workspaceDisplayName, value, StringComparison.Ordinal))
      {
        return;
      }

      workspaceDisplayName = value ?? string.Empty;
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanContinue));
    }
  }

  public string WorkspacePath
  {
    get => workspacePath;
    set
    {
      if (string.Equals(workspacePath, value, StringComparison.Ordinal))
      {
        return;
      }

      workspacePath = value ?? string.Empty;
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanContinue));
    }
  }

  public string? SelectedCollectionId
  {
    get => selectedCollectionId;
    private set
    {
      if (string.Equals(selectedCollectionId, value, StringComparison.Ordinal))
      {
        return;
      }

      selectedCollectionId = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanContinue));
    }
  }

  public string NewCollectionName
  {
    get => newCollectionName;
    set
    {
      if (string.Equals(newCollectionName, value, StringComparison.Ordinal))
      {
        return;
      }

      newCollectionName = value ?? string.Empty;
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanContinue));
    }
  }

  public bool IsLibraryOnly
  {
    get => isLibraryOnly;
    private set
    {
      if (isLibraryOnly == value)
      {
        return;
      }

      isLibraryOnly = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanContinue));
    }
  }

  public bool IsExistingCollection
  {
    get => isExistingCollection;
    private set
    {
      if (isExistingCollection == value)
      {
        return;
      }

      isExistingCollection = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanContinue));
    }
  }

  public bool IsNewCollection
  {
    get => isNewCollection;
    private set
    {
      if (isNewCollection == value)
      {
        return;
      }

      isNewCollection = value;
      OnPropertyChanged();
      OnPropertyChanged(nameof(CanContinue));
    }
  }

  public bool CanContinue =>
      launchContext.IsNewWorkspace
          ? !string.IsNullOrWhiteSpace(WorkspaceDisplayName) &&
            !string.IsNullOrWhiteSpace(WorkspacePath) &&
            HasValidMembershipSelection()
          : HasValidMembershipSelection();

  internal void InitializeSuggestion(ImportSourceKind sourceKind)
  {
    if (destinationInitialized)
    {
      return;
    }

    destinationInitialized = true;

    if (!string.IsNullOrWhiteSpace(launchContext.SelectedCollectionId) && HasExistingCollectionOption)
    {
      SelectExistingCollection(launchContext.SelectedCollectionId, isSuggestion: true);
      return;
    }

    if (sourceKind == ImportSourceKind.SteamCollection)
    {
      SelectNewCollection(NewCollectionName, isSuggestion: true);
    }
  }

  internal void SelectLibraryOnly()
  {
    userSelectedDestination = true;
    IsLibraryOnly = true;
    IsExistingCollection = false;
    IsNewCollection = false;
    SelectedCollectionId = null;
  }

  internal void SelectExistingCollection(string collectionId, bool isSuggestion = false)
  {
    if (!HasExistingCollectionOption)
    {
      return;
    }

    if (!isSuggestion)
    {
      userSelectedDestination = true;
    }

    IsLibraryOnly = false;
    IsExistingCollection = true;
    IsNewCollection = false;
    SelectedCollectionId = collectionId;
  }

  internal void SelectNewCollection(string? displayName = null, bool isSuggestion = false)
  {
    if (!isSuggestion)
    {
      userSelectedDestination = true;
    }

    if (!string.IsNullOrWhiteSpace(displayName))
    {
      NewCollectionName = displayName;
    }

    IsLibraryOnly = false;
    IsExistingCollection = false;
    IsNewCollection = true;
    SelectedCollectionId = null;
  }

  internal void ApplySuggestionIfNeeded(ImportSourceKind sourceKind)
  {
    if (userSelectedDestination)
    {
      return;
    }

    InitializeSuggestion(sourceKind);
  }

  public ImportMembershipDestination BuildMembershipDestination()
  {
    if (IsLibraryOnly)
    {
      return ImportMembershipDestination.ForLibraryOnly();
    }

    if (IsExistingCollection)
    {
      return ImportMembershipDestination.ForExistingCollection(SelectedCollectionId ?? string.Empty);
    }

    if (IsNewCollection)
    {
      return ImportMembershipDestination.ForNewCollection(NewCollectionName);
    }

    throw new InvalidOperationException("A destination membership choice is required.");
  }

  public ImportTargetContext BuildTargetContext()
  {
    var membershipDestination = BuildMembershipDestination();
    return launchContext.IsNewWorkspace
        ? ImportTargetContext.ForNewWorkspace(
            WorkspaceDisplayName,
            WorkspacePath,
            membershipDestination)
        : ImportTargetContext.ForCurrentWorkspace(
            launchContext.WorkspaceId ?? string.Empty,
            launchContext.WorkspacePath ?? string.Empty,
            membershipDestination);
  }

  private bool HasValidMembershipSelection()
  {
    if (IsLibraryOnly)
    {
      return true;
    }

    if (IsExistingCollection)
    {
      return HasExistingCollectionOption &&
             !string.IsNullOrWhiteSpace(SelectedCollectionId);
    }

    if (IsNewCollection)
    {
      return !string.IsNullOrWhiteSpace(NewCollectionName);
    }

    return false;
  }
}
