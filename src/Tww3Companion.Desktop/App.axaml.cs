using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;
using System.ComponentModel;
using Tww3Companion.Desktop.Views;
using Tww3Companion.Desktop.ViewModels;

namespace Tww3Companion.Desktop;

public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            var viewModel = (ShellViewModel)window.DataContext!;
            viewModel.PropertyChanged += (_, args) => ApplyTheme(viewModel, args);
            PlatformSettings!.ColorValuesChanged += (_, _) => ApplyPlatformContrast(viewModel);
            ApplyPlatformContrast(viewModel);
            ApplyTheme(viewModel, null);
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ApplyPlatformContrast(ShellViewModel viewModel) =>
        viewModel.SetHighContrast(PlatformSettings!.GetColorValues().ContrastPreference != ColorContrastPreference.NoPreference);

    private void ApplyTheme(ShellViewModel viewModel, PropertyChangedEventArgs? args)
    {
        if (args is not null && args.PropertyName != nameof(ShellViewModel.EffectiveTheme))
        {
            return;
        }

        RequestedThemeVariant = viewModel.EffectiveTheme switch
        {
            ThemeChoice.Light => ThemeVariant.Light,
            ThemeChoice.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }
}
