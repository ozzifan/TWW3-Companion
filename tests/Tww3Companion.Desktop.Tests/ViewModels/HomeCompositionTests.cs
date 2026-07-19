using Tww3Companion.Application.Common;
using Tww3Companion.Application.Settings;
using Tww3Companion.Desktop.Services;
using Tww3Companion.Desktop.ViewModels;
using Xunit;

namespace Tww3Companion.Desktop.Tests.ViewModels;

public sealed class HomeCompositionTests
{
    [Fact]
    public void HomeStartsVisibleAndShowsOnlyApprovedActions()
    {
        var subject = new ShellViewModel();

        Assert.Equal(ShellScreen.Home, subject.CurrentScreen);
        Assert.NotNull(subject.Home);
        Assert.Equal("Create Workspace", subject.Home.PrimaryActionLabel);
        Assert.Equal("Open Workspace", subject.Home.SecondaryActionLabel);
        Assert.Contains(subject.Home.Recents, recent => recent.DisplayName == "Missing Workspace");
        Assert.Equal("This Workspace contains no Mods or Collections yet. No data has been added.", subject.Workspace.EmptyStateMessage);
        Assert.DoesNotContain(subject.Home.NavigationItems, item => item.Contains("Import", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CreateOrOpenBusyFlagPreventsDuplicateCommands()
    {
        var dialogs = new BlockingWorkspaceDialogService();
        var subject = ShellViewModel.CreateForTest(dialogs);

        var createTask = Task.Run(() => subject.CreateWorkspaceCommand.Execute(null));
        await WaitForBusyState(subject);

        Assert.True(subject.Home.IsBusy);
        Assert.False(subject.CreateWorkspaceCommand.CanExecute(null));
        Assert.False(subject.OpenWorkspaceCommand.CanExecute(null));

        dialogs.ReleaseCreatePrompt();
        await createTask;
    }

    [Fact]
    public void FinalizingMessageAppearsOnlyInFinalizingState()
    {
        var subject = ShellViewModel.CreateForTest(new ImmediateWorkspaceDialogService());

        Assert.Equal(WorkspaceOperationState.Idle, subject.Home.OperationState);
        Assert.NotEqual("Finalizing — please wait", subject.Home.OperationStatusMessage);

        subject.BeginCreateWorkspaceForTest();
        Assert.Equal(WorkspaceOperationState.Busy, subject.Home.OperationState);
        Assert.NotEqual("Finalizing — please wait", subject.Home.OperationStatusMessage);

        subject.EnterFinalizingForTest();
        Assert.Equal(WorkspaceOperationState.Finalizing, subject.Home.OperationState);
        Assert.Equal("Finalizing — please wait", subject.Home.OperationStatusMessage);
    }

    [Fact]
    public void SettingsSaveFailureRetainsInMemoryValueAndExposesRecoveryCommands()
    {
        var subject = ShellViewModel.CreateForTest(settingsStore: new FailingApplicationSettingsStore());

        subject.SetTheme(ThemeChoice.Dark);

        Assert.Equal(ThemeChoice.Dark, subject.StoredTheme);
        Assert.NotEmpty(subject.Home.SettingsSaveError);
        Assert.NotNull(subject.RetrySettingsSaveCommand);
        Assert.NotNull(subject.OpenSettingsFolderCommand);
        Assert.True(subject.RetrySettingsSaveCommand.CanExecute(null));
        Assert.True(subject.OpenSettingsFolderCommand.CanExecute(null));
    }

    [Fact]
    public void ReturnHomeDisposalPreventsScreenChangeUntilDisposalSucceeds()
    {
        var lifetime = new ControllableWorkspaceLifetime();
        var subject = ShellViewModel.CreateForTest(workspaceLifetime: lifetime);

        subject.OpenWorkspace();
        Assert.Equal(ShellScreen.Workspace, subject.CurrentScreen);

        subject.ReturnHome();
        Assert.Equal(ShellScreen.Workspace, subject.CurrentScreen);

        lifetime.CompleteDisposal();
        Assert.Equal(ShellScreen.Home, subject.CurrentScreen);
    }

    [Fact]
    public void RecentsAreLoadedEagerlySoMissingItemsAreVisibleImmediately()
    {
        var subject = new ShellViewModel();

        Assert.NotEmpty(subject.Home.Recents);
        Assert.Contains(
            subject.Home.Recents,
            recent => recent.DisplayName == "Missing Workspace" && recent.IsMissing);
    }

    private static async Task WaitForBusyState(ShellViewModel subject)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (subject.Home.IsBusy)
            {
                return;
            }

            await Task.Delay(10);
        }

        throw new InvalidOperationException("The shell did not enter a busy state.");
    }

    private sealed class BlockingWorkspaceDialogService : IWorkspaceDialogService
    {
        private readonly TaskCompletionSource _createPromptRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<string?> PromptForCreateDisplayNameAsync(CancellationToken cancellationToken) =>
            _createPromptRelease.Task.ContinueWith(_ => (string?)"Busy Test Workspace", cancellationToken);

        public Task<string?> PromptForOpenPathAsync(CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);

        public void ReleaseCreatePrompt() => _createPromptRelease.TrySetResult();
    }

    private sealed class ImmediateWorkspaceDialogService : IWorkspaceDialogService
    {
        public Task<string?> PromptForCreateDisplayNameAsync(CancellationToken cancellationToken) =>
            Task.FromResult<string?>("Immediate Workspace");

        public Task<string?> PromptForOpenPathAsync(CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);
    }

    private sealed class FailingApplicationSettingsStore : IApplicationSettingsStore
    {
        private ApplicationSettings _settings = new(
            SchemaVersion: 1,
            Theme: nameof(ThemeChoice.System),
            WindowPlacement: null,
            RecentWorkspaces:
            [
                new RecentWorkspace(@"C:\Missing\Workspace.tww3c", DateTimeOffset.UtcNow)
            ]);

        public Task<ApplicationSettings> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_settings);

        public Task<OperationResult<ApplicationSettings>> SaveAsync(
            ApplicationSettings settings,
            CancellationToken cancellationToken)
        {
            _settings = settings;
            return Task.FromResult<OperationResult<ApplicationSettings>>(
                new OperationResult<ApplicationSettings>.Failure(
                    new("settings.save.failed", "Saving application settings failed.", false, "Retry saving application settings.")));
        }
    }

    private sealed class ControllableWorkspaceLifetime
    {
        private Action? _onDisposalCompleted;

        public void RegisterDisposalCompletion(Action onCompleted) => _onDisposalCompleted = onCompleted;

        public void CompleteDisposal() => _onDisposalCompleted?.Invoke();
    }
}
