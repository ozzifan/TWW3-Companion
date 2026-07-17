using System.Windows.Input;

namespace Tww3Companion.Desktop.ViewModels;

public enum ShellScreen { Home, Workspace, Compatibility }
public enum ThemeChoice { System, Light, Dark, HighContrast }
public enum CompatibilityAction { Exit, ContinueAnyway }

public sealed class ShellViewModel : ViewModelBase
{
    public const double MinimumWidth = 1024;
    public const double MinimumHeight = 640;

    private ShellScreen _currentScreen = ShellScreen.Home;
    private ThemeChoice _storedTheme = ThemeChoice.System;
    private bool _isHighContrast;

    public ShellViewModel()
    {
        OpenWorkspaceCommand = new DelegateCommand(OpenWorkspace);
        ReturnHomeCommand = new DelegateCommand(ReturnHome);
        ContinueAnywayCommand = new DelegateCommand(ContinueAnyway);
    }

    public ShellScreen CurrentScreen => _currentScreen;
    public bool IsHomeVisible => _currentScreen == ShellScreen.Home;
    public bool IsWorkspaceVisible => _currentScreen == ShellScreen.Workspace;
    public bool IsCompatibilityVisible => _currentScreen == ShellScreen.Compatibility;
    public IReadOnlyList<string> WorkspaceDestinations { get; } = ["Mod Library", "Collections"];
    public IReadOnlyList<ThemeChoice> ThemeChoices { get; } = [ThemeChoice.System, ThemeChoice.Light, ThemeChoice.Dark];
    public IReadOnlyList<CompatibilityAction> CompatibilityActions { get; } = [CompatibilityAction.Exit, CompatibilityAction.ContinueAnyway];
    public string EmptyStateMessage { get; } = "This Workspace contains no Mods or Collections yet. No data has been added.";
    public bool HasCompatibilityWarning { get; private set; }
    public ThemeChoice StoredTheme
    {
        get => _storedTheme;
        set => SetTheme(value);
    }
    public ThemeChoice EffectiveTheme => _isHighContrast ? ThemeChoice.HighContrast : _storedTheme;
    public ICommand OpenWorkspaceCommand { get; }
    public ICommand ReturnHomeCommand { get; }
    public ICommand ContinueAnywayCommand { get; }

    public void OpenWorkspace() => SetScreen(ShellScreen.Workspace);
    public void ReturnHome() => SetScreen(ShellScreen.Home);

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
        OnPropertyChanged(nameof(StoredTheme));
        OnPropertyChanged(nameof(EffectiveTheme));
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

    private void SetScreen(ShellScreen screen)
    {
        _currentScreen = screen;
        OnPropertyChanged(nameof(CurrentScreen));
        OnPropertyChanged(nameof(IsHomeVisible));
        OnPropertyChanged(nameof(IsWorkspaceVisible));
        OnPropertyChanged(nameof(IsCompatibilityVisible));
    }

    private sealed class DelegateCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute();
    }
}
