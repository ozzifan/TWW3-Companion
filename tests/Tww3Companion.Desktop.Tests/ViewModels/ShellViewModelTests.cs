using Tww3Companion.Desktop.Services;
using Tww3Companion.Desktop.ViewModels;
using Xunit;

namespace Tww3Companion.Desktop.Tests.ViewModels;

public sealed class ShellViewModelTests
{
    [Fact]
    public void StartsOnHomeWithOnlyFoundationWorkspaceDestinations()
    {
        var subject = new ShellViewModel();

        Assert.Equal(ShellScreen.Home, subject.CurrentScreen);
        Assert.Equal(["Mod Library", "Collections"], subject.Workspace.WorkspaceDestinations);
        Assert.DoesNotContain(subject.Workspace.WorkspaceDestinations, destination =>
            destination.Contains("Import", StringComparison.OrdinalIgnoreCase)
            || destination.Contains("Search", StringComparison.OrdinalIgnoreCase)
            || destination.Contains("Profile", StringComparison.OrdinalIgnoreCase)
            || destination.Contains("Health", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("This Workspace contains no Mods or Collections yet. No data has been added.", subject.Workspace.EmptyStateMessage);
    }

    [Fact]
    public void HighContrastOverridesButDoesNotReplaceStoredTheme()
    {
        var subject = new ShellViewModel();

        Assert.Equal(ThemeChoice.System, subject.StoredTheme);
        Assert.Equal(ThemeChoice.System, subject.EffectiveTheme);

        subject.SetTheme(ThemeChoice.Dark);
        subject.SetHighContrast(true);

        Assert.Equal(ThemeChoice.Dark, subject.StoredTheme);
        Assert.Equal(ThemeChoice.HighContrast, subject.EffectiveTheme);

        subject.SetHighContrast(false);
        Assert.Equal(ThemeChoice.Dark, subject.EffectiveTheme);
    }

    [Fact]
    public void UndersizedWorkAreaRequiresCompatibilityDecisionAndRetainsWarningAfterContinue()
    {
        var subject = new ShellViewModel();

        subject.EvaluateWorkArea(1000, 620);

        Assert.Equal(ShellScreen.Compatibility, subject.CurrentScreen);
        Assert.Equal([CompatibilityAction.Exit, CompatibilityAction.ContinueAnyway], subject.CompatibilityActions);

        subject.ContinueAnyway();
        Assert.Equal(ShellScreen.Home, subject.CurrentScreen);
        Assert.True(subject.HasCompatibilityWarning);
    }

    [Fact]
    public async Task WorkspaceAndReturnHomeActionsChangeScreen()
    {
        var coordinator = new CompletingWorkspaceDisposalCoordinator();
        var subject = ShellViewModel.CreateForTest(workspaceDisposalCoordinator: coordinator);

        subject.OpenWorkspace();
        Assert.Equal(ShellScreen.Workspace, subject.CurrentScreen);

        subject.ReturnHome();
        await WaitForScreen(subject, ShellScreen.Home);
        Assert.True(coordinator.WasDisposed);
        Assert.Equal(ShellScreen.Home, subject.CurrentScreen);
    }

    private static async Task WaitForScreen(ShellViewModel subject, ShellScreen screen)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (subject.CurrentScreen == screen)
            {
                return;
            }

            await Task.Delay(10);
        }

        throw new InvalidOperationException($"The shell did not enter {screen}.");
    }

    private sealed class CompletingWorkspaceDisposalCoordinator : IWorkspaceDisposalCoordinator
    {
        public bool WasDisposed { get; private set; }

        public Task DisposeWorkspaceScopeAsync(CancellationToken cancellationToken)
        {
            WasDisposed = true;
            return Task.CompletedTask;
        }
    }
}
