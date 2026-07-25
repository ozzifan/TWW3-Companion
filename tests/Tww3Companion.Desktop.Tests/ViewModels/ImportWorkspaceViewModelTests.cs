using Tww3Companion.Application.Importing;
using Tww3Companion.Desktop.Services;
using Tww3Companion.Desktop.ViewModels;
using Xunit;

namespace Tww3Companion.Desktop.Tests.ViewModels;

public sealed class ImportWorkspaceViewModelTests
{
  [Fact]
  public void SteamCollection_suggests_selected_collection_but_does_not_lock_it()
  {
    var subject = CreateSubject(new ImportLaunchContext(
        IsNewWorkspace: false,
        WorkspaceId: "workspace-1",
        WorkspacePath: @"C:\Data\workspace.tww3c",
        Collections: [new CollectionSummary("collection-1", "Current", 0)],
        SelectedCollectionId: "collection-1"));

    subject.Source.Select(ImportSourceKind.SteamCollection);
    subject.OpenDestination();

    Assert.Equal("collection-1", subject.Destination.SelectedCollectionId);
    subject.Destination.SelectLibraryOnly();
    Assert.IsType<ImportMembershipDestination.LibraryOnly>(
        subject.Destination.BuildMembershipDestination());
  }

  [Fact]
  public void Home_launch_context_does_not_offer_existing_collection_destination()
  {
    var subject = CreateSubject(new ImportLaunchContext(
        IsNewWorkspace: true,
        WorkspaceId: null,
        WorkspacePath: null,
        Collections: [],
        SelectedCollectionId: null));

    subject.Source.Select(ImportSourceKind.Markdown);
    subject.OpenDestination();

    Assert.False(subject.Destination.HasExistingCollectionOption);
    Assert.True(subject.Destination.ShowsWorkspaceDetails);
  }

  [Fact]
  public async Task ContinueFromDestination_reuses_preview_when_fingerprint_is_unchanged()
  {
    var coordinator = new RecordingImportTaskCoordinator();
    var subject = CreateSubject(
        CurrentWorkspaceLaunchContext(),
        coordinator);

    await PreparePreviewAsync(subject, coordinator);

    coordinator.ResetCounters();
    subject.GoBack();
    await subject.ContinueFromDestinationAsync(TestContext.Current.CancellationToken);

    Assert.Equal(0, coordinator.LoadSourceCallCount);
    Assert.Equal(0, coordinator.BuildPreviewCallCount);
    Assert.Equal(ImportTaskStage.Preview, subject.Stage);
  }

  [Fact]
  public async Task Changed_source_rebuilds_preview_and_invalidates_confirmation()
  {
    var coordinator = new RecordingImportTaskCoordinator();
    var subject = CreateSubject(
        CurrentWorkspaceLaunchContext(),
        coordinator);

    await PreparePreviewAsync(subject, coordinator);
    await subject.ContinueFromPreviewAsync(TestContext.Current.CancellationToken);
    Assert.Equal(ImportTaskStage.Confirmation, subject.Stage);

    subject.GoBack();
    subject.GoBack();
    subject.Source.InputText = "987654321";
    await subject.ContinueFromDestinationAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, coordinator.LoadSourceCallCount);
    Assert.Equal(2, coordinator.BuildPreviewCallCount);
    Assert.Equal(ImportTaskStage.Preview, subject.Stage);
  }

  [Fact]
  public async Task RequestDismiss_requires_confirmation_when_preview_exists()
  {
    var coordinator = new RecordingImportTaskCoordinator();
    var subject = CreateSubject(
        CurrentWorkspaceLaunchContext(),
        coordinator);

    await PreparePreviewAsync(subject, coordinator);

    Assert.False(subject.TryDismiss());

    Assert.True(subject.RequiresDiscardConfirmation);
    subject.ConfirmDiscard();
    Assert.True(subject.TryDismiss());
  }

  [Fact]
  public async Task Apply_sets_finalizing_disables_navigation_and_raises_completion()
  {
    var coordinator = new RecordingImportTaskCoordinator();
    ImportTaskCompletedEvent? completed = null;
    var subject = CreateSubject(
        CurrentWorkspaceLaunchContext(),
        coordinator);
    subject.Completed += (_, args) => completed = args;

    await PreparePreviewAsync(subject, coordinator);
    await subject.ContinueFromPreviewAsync(TestContext.Current.CancellationToken);

    await subject.ApplyAsync(TestContext.Current.CancellationToken);

    Assert.Equal(ImportTaskStage.Complete, subject.Stage);
    Assert.False(subject.CanGoBack);
    Assert.False(subject.CanCancel);
    Assert.NotNull(completed);
    Assert.True(completed!.Outcome.Applied);
  }

  [Fact]
  public async Task Apply_failure_retains_preview_and_returns_to_preview_stage()
  {
    var coordinator = new RecordingImportTaskCoordinator
    {
      ApplyException = new InvalidOperationException("Persistence failed.")
    };
    var subject = CreateSubject(
        CurrentWorkspaceLaunchContext(),
        coordinator);

    await PreparePreviewAsync(subject, coordinator);
    await subject.ContinueFromPreviewAsync(TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<InvalidOperationException>(() =>
        subject.ApplyAsync(TestContext.Current.CancellationToken));

    Assert.Equal(ImportTaskStage.Preview, subject.Stage);
    Assert.NotNull(subject.Preview.Preview);
    Assert.True(subject.CanGoBack);
  }

  private static ImportLaunchContext CurrentWorkspaceLaunchContext() =>
      new(
          IsNewWorkspace: false,
          WorkspaceId: "workspace-1",
          WorkspacePath: @"C:\Data\workspace.tww3c",
          Collections: [new CollectionSummary("collection-1", "Current", 0)],
          SelectedCollectionId: null);

  private static async Task PreparePreviewAsync(
      ImportWorkspaceViewModel subject,
      RecordingImportTaskCoordinator coordinator)
  {
    subject.Source.Select(ImportSourceKind.SteamItems);
    subject.Source.InputText = "123456789";
    subject.OpenDestination();
    subject.Destination.SelectLibraryOnly();
    await subject.ContinueFromDestinationAsync(TestContext.Current.CancellationToken);
    Assert.Equal(1, coordinator.BuildPreviewCallCount);
  }

  private static ImportWorkspaceViewModel CreateSubject(
      ImportLaunchContext launchContext,
      RecordingImportTaskCoordinator? coordinator = null) =>
      new(
          launchContext,
          coordinator ?? new RecordingImportTaskCoordinator(),
          new FakeImportSourceFileService());

  private sealed class FakeImportSourceFileService : IImportSourceFileService
  {
    public Task<ImportSourceDocument?> ChooseTextFileAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<ImportSourceDocument?>(null);
  }

  internal sealed class RecordingImportTaskCoordinator : IImportTaskCoordinator
  {
    public int LoadSourceCallCount { get; private set; }
    public int BuildPreviewCallCount { get; private set; }
    public int ApplyCallCount { get; private set; }
    public Exception? ApplyException { get; init; }
    public ImportPreview LastBuiltPreview { get; private set; } = CreateDefaultPreview();

    public void ResetCounters()
    {
      LoadSourceCallCount = 0;
      BuildPreviewCallCount = 0;
      ApplyCallCount = 0;
    }

    public Task<ImportSourceLoadResult> LoadSourceAsync(
        ImportSourceRequest request,
        CancellationToken cancellationToken = default)
    {
      LoadSourceCallCount++;
      return Task.FromResult(new ImportSourceLoadResult(
          [new SteamImportCandidate("123456789", "Example Mod")],
          [],
          ["123456789"]));
    }

    public Task<ImportPreview> BuildPreviewAsync(
        ImportTargetContext targetContext,
        IReadOnlyList<object> candidates,
        CancellationToken cancellationToken = default)
    {
      BuildPreviewCallCount++;
      LastBuiltPreview = CreateDefaultPreview(targetContext);
      return Task.FromResult(LastBuiltPreview);
    }

    public Task<ImportPreview> ResolveAsync(
        ImportPreview preview,
        ImportCandidate resolvedCandidate,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(preview);

    public Task<ImportOutcome> ApplyAsync(
        ImportPreview preview,
        CancellationToken cancellationToken = default)
    {
      ApplyCallCount++;
      if (ApplyException is not null)
      {
        return Task.FromException<ImportOutcome>(ApplyException);
      }

      return Task.FromResult(new ImportOutcome(preview.TargetContext, [], Applied: true));
    }

    private static ImportPreview CreateDefaultPreview(ImportTargetContext? targetContext = null) =>
        new(
            targetContext ?? ImportTargetContext.ForCurrentWorkspace(
                "workspace-1",
                @"C:\Data\workspace.tww3c",
                ImportMembershipDestination.ForLibraryOnly()),
            [ImportCandidate.CreateWithDisplayName("candidate-1", "Example Mod")],
            Applied: false,
            Operations:
            [
                new ImportPreviewOperation(
                    "candidate-1",
                    ImportLibraryAction.Create,
                    ImportMembershipAction.None,
                    [])
            ]);
  }
}
