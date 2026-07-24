using System.Diagnostics;
using System.Windows.Input;
using Tww3Companion.Application.Common;
using Tww3Companion.Application.Settings;
using Tww3Companion.Application.Workspaces;
using Tww3Companion.Desktop.Services;
using Tww3Companion.Domain.Workspaces;

namespace Tww3Companion.Desktop.ViewModels;

public enum ShellScreen { Home, Workspace, Compatibility }
public enum ThemeChoice { System, Light, Dark, HighContrast }
public enum CompatibilityAction { Exit, ContinueAnyway }
public enum WorkspaceOperationState { Idle, Busy, Finalizing }

public sealed record RecentWorkspaceItem(
    string DisplayName,
    string Path,
    bool IsMissing,
    bool IsRemovable);

public sealed record HomeShellState(
    string PrimaryActionLabel,
    string SecondaryActionLabel,
    IReadOnlyList<RecentWorkspaceItem> Recents,
    string SettingsSaveError,
    bool HasSettingsSaveError,
    bool IsBusy,
    bool IsFinalizing,
    WorkspaceOperationState OperationState,
    string OperationStatusMessage,
    IReadOnlyList<string> NavigationItems);

public sealed record WorkspaceShellState(
    string EmptyStateMessage,
    IReadOnlyList<string> WorkspaceDestinations,
    string OperationError,
    bool HasOperationError);

public sealed class ShellViewModel : ViewModelBase
{
  public const double MinimumWidth = 1024;
  public const double MinimumHeight = 640;
  private const string EmptyWorkspaceMessage = "This Workspace contains no Mods or Collections yet. No data has been added.";
  private const string FinalizingMessage = "Finalizing — please wait";
  private static readonly IReadOnlyList<string> DefaultWorkspaceDestinations = ["Mod Library", "Collections"];

  private ShellScreen _currentScreen = ShellScreen.Home;
  private ThemeChoice _storedTheme = ThemeChoice.System;
  private bool _isHighContrast;
  private ApplicationSettings settings;
  private readonly IApplicationSettingsStore settingsStore;
  private readonly IWorkspaceQuery? workspaceQuery;
  private readonly Func<CancellationToken, Task<string?>> promptCreateDisplayName;
  private readonly Func<CancellationToken, Task<string?>> promptOpenPath;
  private readonly CreateWorkspace? createWorkspace;
  private readonly OpenWorkspace? openWorkspace;
  private readonly string defaultWorkspaceDirectory;
  private readonly string settingsDirectory;
  private readonly IWorkspaceDisposalCoordinator workspaceDisposalCoordinator;
  private readonly DelegateCommand createWorkspaceCommand;
  private readonly DelegateCommand openWorkspaceCommand;
  private readonly DelegateCommand removeRecentCommand;
  private readonly DelegateCommand retrySettingsSaveCommand;
  private bool isDisposingWorkspace;

  public ShellViewModel() : this(CreateDefaultOptions())
  {
  }

  private ShellViewModel(ShellViewModelOptions options)
  {
    settingsStore = options.SettingsStore;
    settings = options.InitialSettings ?? settingsStore.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
    _storedTheme = ParseTheme(settings.Theme);
    workspaceQuery = options.WorkspaceQuery;
    promptCreateDisplayName = options.PromptCreateDisplayName;
    promptOpenPath = options.PromptOpenPath;
    createWorkspace = options.CreateWorkspace;
    openWorkspace = options.OpenWorkspace;
    defaultWorkspaceDirectory = options.DefaultWorkspaceDirectory;
    settingsDirectory = options.SettingsDirectory;
    workspaceDisposalCoordinator = options.WorkspaceDisposalCoordinator;

    // RFC-0005 keeps Home navigation in the shared shell; the next slice adds Import here.
    Home = CreateHomeState(WorkspaceOperationState.Idle, string.Empty);
    Workspace = CreateWorkspaceState(string.Empty);
    Library = new ModLibraryViewModel(workspaceQuery);
    Collections = new CollectionDetailViewModel(workspaceQuery);
    _ = LoadWorkspacePanelsAsync();
    createWorkspaceCommand = new DelegateCommand(_ => _ = RunCreateWorkspaceAsync(), _ => !Home.IsBusy);
    openWorkspaceCommand = new DelegateCommand(_ => _ = RunOpenWorkspaceAsync(), _ => !Home.IsBusy);
    removeRecentCommand = new DelegateCommand(
        parameter => _ = RemoveRecentAsync(parameter),
        parameter => parameter is RecentWorkspaceItem { IsRemovable: true });
    retrySettingsSaveCommand = new DelegateCommand(
        _ => _ = SaveSettingsAsync(),
        _ => !string.IsNullOrWhiteSpace(Home.SettingsSaveError));

    CreateWorkspaceCommand = createWorkspaceCommand;
    OpenWorkspaceCommand = openWorkspaceCommand;
    RemoveRecentCommand = removeRecentCommand;
    RetrySettingsSaveCommand = retrySettingsSaveCommand;
    OpenSettingsFolderCommand = new DelegateCommand(_ => OpenSettingsFolder());
    ReturnHomeCommand = new DelegateCommand(_ => ReturnHome());
    ContinueAnywayCommand = new DelegateCommand(_ => ContinueAnyway());
  }

  public static ShellViewModel CreateForTest(
      IWorkspaceDialogService? workspaceDialogService = null,
      IApplicationSettingsStore? settingsStore = null,
      IWorkspaceDisposalCoordinator? workspaceDisposalCoordinator = null) =>
      new(new ShellViewModelOptions
      {
        SettingsStore = settingsStore ?? new InMemoryApplicationSettingsStore(DefaultSettings()),
        PromptCreateDisplayName = cancellationToken =>
              (workspaceDialogService?.PromptForCreateDisplayNameAsync(cancellationToken))
              ?? Task.FromResult<string?>(null),
        PromptOpenPath = cancellationToken =>
              (workspaceDialogService?.PromptForOpenPathAsync(cancellationToken))
              ?? Task.FromResult<string?>(null),
        WorkspaceDisposalCoordinator = workspaceDisposalCoordinator ?? new WorkspaceDisposalCoordinator()
      });

  public static ShellViewModel Create(
      ApplicationSettings initialSettings,
      IApplicationSettingsStore settingsStore,
      IWorkspaceDialogService workspaceDialogService,
      CreateWorkspace createWorkspace,
      OpenWorkspace openWorkspace,
      string defaultWorkspaceDirectory,
      string settingsDirectory,
      IWorkspaceDisposalCoordinator workspaceDisposalCoordinator) =>
      new(new ShellViewModelOptions
      {
        InitialSettings = initialSettings,
        SettingsStore = settingsStore,
        PromptCreateDisplayName = workspaceDialogService.PromptForCreateDisplayNameAsync,
        PromptOpenPath = workspaceDialogService.PromptForOpenPathAsync,
        CreateWorkspace = createWorkspace,
        OpenWorkspace = openWorkspace,
        DefaultWorkspaceDirectory = defaultWorkspaceDirectory,
        SettingsDirectory = settingsDirectory,
        WorkspaceDisposalCoordinator = workspaceDisposalCoordinator
      });

  public ShellScreen CurrentScreen => _currentScreen;
  public bool IsHomeVisible => _currentScreen == ShellScreen.Home;
  public bool IsWorkspaceVisible => _currentScreen == ShellScreen.Workspace;
  public bool IsCompatibilityVisible => _currentScreen == ShellScreen.Compatibility;
  public IReadOnlyList<string> WorkspaceDestinations { get; } = DefaultWorkspaceDestinations;
  public IReadOnlyList<ThemeChoice> ThemeChoices { get; } = [ThemeChoice.System, ThemeChoice.Light, ThemeChoice.Dark];
  public IReadOnlyList<CompatibilityAction> CompatibilityActions { get; } = [CompatibilityAction.Exit, CompatibilityAction.ContinueAnyway];
  public string EmptyStateMessage { get; } = EmptyWorkspaceMessage;
  public HomeShellState Home { get; private set; }
  public WorkspaceShellState Workspace { get; private set; }
  public ModLibraryViewModel Library { get; }
  public CollectionDetailViewModel Collections { get; }
  public bool HasCompatibilityWarning { get; private set; }
  public ThemeChoice StoredTheme
  {
    get => _storedTheme;
    set => SetTheme(value);
  }

  public ThemeChoice EffectiveTheme => _isHighContrast ? ThemeChoice.HighContrast : _storedTheme;
  public ICommand CreateWorkspaceCommand { get; }
  public ICommand OpenWorkspaceCommand { get; }
  public ICommand RemoveRecentCommand { get; }
  public ICommand RetrySettingsSaveCommand { get; }
  public ICommand OpenSettingsFolderCommand { get; }
  public ICommand ReturnHomeCommand { get; }
  public ICommand ContinueAnywayCommand { get; }

  public void OpenWorkspace()
  {
    UpdateWorkspaceError(string.Empty);
    SetScreen(ShellScreen.Workspace);
  }

  public void ReturnHome() => _ = ReturnHomeAsync();

  public void EvaluateWorkArea(double width, double height)
  {
    if (width < MinimumWidth || height < MinimumHeight)
    {
      SetScreen(ShellScreen.Compatibility);
    }
  }

  public void ContinueAnyway()
  {
    HasCompatibilityWarning = true;
    OnPropertyChanged(nameof(HasCompatibilityWarning));
    SetScreen(ShellScreen.Home);
  }

  public void SetTheme(ThemeChoice choice)
  {
    if (choice == ThemeChoice.HighContrast || _storedTheme == choice)
    {
      return;
    }

    _storedTheme = choice;
    settings = settings with { Theme = choice.ToString() };
    OnPropertyChanged(nameof(StoredTheme));
    OnPropertyChanged(nameof(EffectiveTheme));
    _ = SaveSettingsAsync();
  }

  public void SetHighContrast(bool active)
  {
    if (_isHighContrast == active)
    {
      return;
    }

    _isHighContrast = active;
    OnPropertyChanged(nameof(EffectiveTheme));
  }

  public void BeginCreateWorkspaceForTest() => SetOperationState(WorkspaceOperationState.Busy);

  public void EnterFinalizingForTest() => SetOperationState(WorkspaceOperationState.Finalizing);

  private async Task RunCreateWorkspaceAsync()
  {
    if (Home.IsBusy)
    {
      return;
    }

    SetOperationState(WorkspaceOperationState.Busy);
    try
    {
      var displayName = await promptCreateDisplayName(CancellationToken.None);
      if (string.IsNullOrWhiteSpace(displayName))
      {
        SetOperationState(WorkspaceOperationState.Idle);
        return;
      }

      SetOperationState(WorkspaceOperationState.Finalizing);
      if (createWorkspace is not null)
      {
        var path = Path.Combine(defaultWorkspaceDirectory, $"{SafeFileName(displayName)}.tww3c");
        var result = await createWorkspace.ExecuteAsync(displayName, path, CancellationToken.None);
        if (result is OperationResult<Workspace>.Failure failure)
        {
          if (!failure.Error.PersistentChangeCommitted)
          {
            SetOperationState(WorkspaceOperationState.Idle, failure.Error.Message);
            return;
          }

          settings = await settingsStore.LoadAsync(CancellationToken.None);
          UpdateHome(failure.Error.Message);
        }
        else
        {
          settings = await settingsStore.LoadAsync(CancellationToken.None);
          UpdateHome(Home.SettingsSaveError);
        }
      }

      SetOperationState(WorkspaceOperationState.Idle);
      OpenWorkspace();
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
      SetOperationState(WorkspaceOperationState.Idle, exception.Message);
    }
    finally
    {
      if (_currentScreen != ShellScreen.Workspace && Home.OperationState != WorkspaceOperationState.Idle)
      {
        SetOperationState(WorkspaceOperationState.Idle);
      }
    }
  }

  private async Task RunOpenWorkspaceAsync()
  {
    if (Home.IsBusy)
    {
      return;
    }

    SetOperationState(WorkspaceOperationState.Busy);
    try
    {
      var path = await promptOpenPath(CancellationToken.None);
      if (string.IsNullOrWhiteSpace(path))
      {
        SetOperationState(WorkspaceOperationState.Idle);
        return;
      }

      SetOperationState(WorkspaceOperationState.Finalizing);
      if (openWorkspace is not null)
      {
        var result = await openWorkspace.ExecuteAsync(path, CancellationToken.None);
        if (result is OperationResult<Workspace>.Failure failure)
        {
          if (!failure.Error.PersistentChangeCommitted)
          {
            SetOperationState(WorkspaceOperationState.Idle, failure.Error.Message);
            return;
          }

          settings = await settingsStore.LoadAsync(CancellationToken.None);
          UpdateHome(failure.Error.Message);
        }
        else
        {
          settings = await settingsStore.LoadAsync(CancellationToken.None);
          UpdateHome(Home.SettingsSaveError);
        }
      }

      SetOperationState(WorkspaceOperationState.Idle);
      OpenWorkspace();
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
      SetOperationState(WorkspaceOperationState.Idle, exception.Message);
    }
    finally
    {
      if (_currentScreen != ShellScreen.Workspace && Home.OperationState != WorkspaceOperationState.Idle)
      {
        SetOperationState(WorkspaceOperationState.Idle);
      }
    }
  }

  private async Task RemoveRecentAsync(object? parameter)
  {
    if (parameter is not RecentWorkspaceItem item)
    {
      return;
    }

    settings = settings with
    {
      RecentWorkspaces = settings.RecentWorkspaces
            .Where(recent => !StringComparer.OrdinalIgnoreCase.Equals(recent.Path, item.Path))
            .ToArray()
    };
    await SaveSettingsAsync();
    UpdateHome(Home.SettingsSaveError);
  }

  private void OpenSettingsFolder()
  {
    if (!string.IsNullOrWhiteSpace(settingsDirectory))
    {
      Directory.CreateDirectory(settingsDirectory);
      Process.Start(new ProcessStartInfo
      {
        FileName = settingsDirectory,
        UseShellExecute = true
      });
    }
  }

  private async Task ReturnHomeAsync()
  {
    if (_currentScreen != ShellScreen.Workspace)
    {
      SetScreen(ShellScreen.Home);
      return;
    }

    if (isDisposingWorkspace)
    {
      return;
    }

    isDisposingWorkspace = true;
    try
    {
      await workspaceDisposalCoordinator.DisposeWorkspaceScopeAsync(CancellationToken.None);
      SetScreen(ShellScreen.Home);
    }
    catch (OperationCanceledException exception)
    {
      // Fire-and-forget ReturnHome must still surface cancellation; otherwise the discarded
      // task fails silently and neither screen change nor error is visible.
      UpdateWorkspaceError(exception.Message);
    }
    catch (Exception exception)
    {
      UpdateWorkspaceError(exception.Message);
    }
    finally
    {
      isDisposingWorkspace = false;
    }
  }

  private async Task SaveSettingsAsync()
  {
    var result = await settingsStore.SaveAsync(settings, CancellationToken.None);
    var error = result is OperationResult<ApplicationSettings>.Failure failure
        ? failure.Error.Message
        : string.Empty;
    UpdateHome(error);
  }

  private void SetOperationState(WorkspaceOperationState state, string? message = null)
  {
    var status = state == WorkspaceOperationState.Finalizing ? FinalizingMessage : message ?? string.Empty;
    Home = Home with
    {
      IsBusy = state != WorkspaceOperationState.Idle,
      IsFinalizing = state == WorkspaceOperationState.Finalizing,
      OperationState = state,
      OperationStatusMessage = status
    };
    OnPropertyChanged(nameof(Home));
    RaiseCommandStateChanged();
  }

  private void UpdateHome(string settingsSaveError)
  {
    Home = Home with
    {
      Recents = CreateRecentItems(settings),
      SettingsSaveError = settingsSaveError,
      HasSettingsSaveError = !string.IsNullOrWhiteSpace(settingsSaveError)
    };
    OnPropertyChanged(nameof(Home));
    RaiseCommandStateChanged();
  }

  private HomeShellState CreateHomeState(WorkspaceOperationState state, string settingsSaveError) =>
      new(
          "Create Workspace",
          "Open Workspace",
          CreateRecentItems(settings),
          settingsSaveError,
          !string.IsNullOrWhiteSpace(settingsSaveError),
          state != WorkspaceOperationState.Idle,
          state == WorkspaceOperationState.Finalizing,
          state,
          state == WorkspaceOperationState.Finalizing ? FinalizingMessage : string.Empty,
          ["Home", "Mod Library", "Collections"]);

  private static WorkspaceShellState CreateWorkspaceState(string operationError) =>
      new(
          EmptyWorkspaceMessage,
          DefaultWorkspaceDestinations,
          operationError,
          !string.IsNullOrWhiteSpace(operationError));

  private async Task LoadWorkspacePanelsAsync()
  {
    await Library.LoadAsync(CancellationToken.None);
    await Collections.LoadAsync(CancellationToken.None);
  }

  private void UpdateWorkspaceError(string operationError)
  {
    if (Workspace.OperationError == operationError)
    {
      return;
    }

    Workspace = CreateWorkspaceState(operationError);
    OnPropertyChanged(nameof(Workspace));
  }

  private static IReadOnlyList<RecentWorkspaceItem> CreateRecentItems(ApplicationSettings settings) =>
      settings.RecentWorkspaces
          .Select(recent => new RecentWorkspaceItem(
              DisplayNameForRecent(recent),
              recent.Path,
              !File.Exists(recent.Path),
              true))
          .ToArray();

  private static string DisplayNameForRecent(RecentWorkspace recent)
  {
    if (!string.IsNullOrWhiteSpace(recent.DisplayName))
    {
      return recent.DisplayName;
    }

    return File.Exists(recent.Path)
        ? Path.GetFileNameWithoutExtension(recent.Path)
        : "Missing Workspace";
  }
  private static string SafeFileName(string displayName)
  {
    var invalid = Path.GetInvalidFileNameChars();
    var safe = new string(displayName.Trim().Select(character =>
        invalid.Contains(character) ? '-' : character).ToArray());
    return string.IsNullOrWhiteSpace(safe) ? "Workspace" : safe;
  }

  private static ThemeChoice ParseTheme(string value) =>
      Enum.TryParse<ThemeChoice>(value, ignoreCase: true, out var theme) && theme != ThemeChoice.HighContrast
          ? theme
          : ThemeChoice.System;

  private void RaiseCommandStateChanged()
  {
    createWorkspaceCommand.RaiseCanExecuteChanged();
    openWorkspaceCommand.RaiseCanExecuteChanged();
    removeRecentCommand.RaiseCanExecuteChanged();
    retrySettingsSaveCommand.RaiseCanExecuteChanged();
  }

  private void SetScreen(ShellScreen screen)
  {
    if (_currentScreen == screen)
    {
      return;
    }

    _currentScreen = screen;
    OnPropertyChanged(nameof(CurrentScreen));
    OnPropertyChanged(nameof(IsHomeVisible));
    OnPropertyChanged(nameof(IsWorkspaceVisible));
    OnPropertyChanged(nameof(IsCompatibilityVisible));
  }

  private static ShellViewModelOptions CreateDefaultOptions() => new()
  {
    SettingsStore = new InMemoryApplicationSettingsStore(DefaultSettings())
  };

  private static ApplicationSettings DefaultSettings() => new(
      SchemaVersion: 1,
      Theme: nameof(ThemeChoice.System),
      WindowPlacement: null,
      RecentWorkspaces: [new RecentWorkspace(@"C:\Missing\Workspace.tww3c", DateTimeOffset.UnixEpoch, "Missing Workspace")]);

  private sealed class ShellViewModelOptions
  {
    public ApplicationSettings? InitialSettings { get; init; }
    public IApplicationSettingsStore SettingsStore { get; init; } = new InMemoryApplicationSettingsStore(DefaultSettings());
    public Func<CancellationToken, Task<string?>> PromptCreateDisplayName { get; init; } = _ => Task.FromResult<string?>(null);
    public Func<CancellationToken, Task<string?>> PromptOpenPath { get; init; } = _ => Task.FromResult<string?>(null);
    public CreateWorkspace? CreateWorkspace { get; init; }
    public OpenWorkspace? OpenWorkspace { get; init; }
    public string DefaultWorkspaceDirectory { get; init; } = Path.GetTempPath();
    public string SettingsDirectory { get; init; } = Path.GetTempPath();
    public IWorkspaceDisposalCoordinator WorkspaceDisposalCoordinator { get; init; } = new WorkspaceDisposalCoordinator();
    public IWorkspaceQuery? WorkspaceQuery { get; init; }
  }

  private sealed class InMemoryApplicationSettingsStore(ApplicationSettings initialSettings) : IApplicationSettingsStore
  {
    private ApplicationSettings settings = initialSettings;

    public Task<ApplicationSettings> LoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(settings);

    public Task<OperationResult<ApplicationSettings>> SaveAsync(
        ApplicationSettings settings,
        CancellationToken cancellationToken)
    {
      this.settings = settings;
      return Task.FromResult<OperationResult<ApplicationSettings>>(
          new OperationResult<ApplicationSettings>.Success(settings));
    }
  }

  private sealed class DelegateCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) : ICommand
  {
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => execute(parameter);
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
  }
}
