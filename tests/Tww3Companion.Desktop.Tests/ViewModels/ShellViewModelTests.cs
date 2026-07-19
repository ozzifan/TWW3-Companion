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
    public void WorkspaceAndReturnHomeActionsChangeScreen()
    {
        var subject = ShellViewModel.CreateForTest();

        subject.OpenWorkspace();
        Assert.Equal(ShellScreen.Workspace, subject.CurrentScreen);

        subject.CompleteWorkspaceDisposalForTest();
        subject.ReturnHome();
        Assert.Equal(ShellScreen.Home, subject.CurrentScreen);
    }
}
