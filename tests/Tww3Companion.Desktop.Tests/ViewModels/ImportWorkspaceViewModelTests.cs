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
    Assert.Null(subject.Confirmation.Summary);
  }

  [Fact]
  public async Task Changed_destination_retains_resolution_when_candidates_and_choices_unchanged()
  {
    var coordinator = new BlockingResolutionCoordinator();
    var subject = CreateSubject(
        CurrentWorkspaceLaunchContext(),
        coordinator);

    subject.Source.Select(ImportSourceKind.SteamItems);
    subject.Source.InputText = "123456789";
    subject.OpenDestination();
    subject.Destination.SelectLibraryOnly();
    await subject.ContinueFromDestinationAsync(TestContext.Current.CancellationToken);

    await subject.Resolution.LinkToModAsync("owner-mod", TestContext.Current.CancellationToken);

    Assert.Null(subject.Resolution.ActiveCandidateId);
    Assert.True(subject.Preview.CanContinue);

    subject.GoBack();
    subject.Destination.SelectExistingCollection("collection-1");
    await subject.ContinueFromDestinationAsync(TestContext.Current.CancellationToken);

    Assert.Equal(2, coordinator.BuildPreviewCallCount);
    var row = Assert.Single(subject.Preview.Rows);
    Assert.Equal("owner-mod", row.Candidate.LinkedModId);
    Assert.True(subject.Preview.CanContinue);
    Assert.Null(subject.Confirmation.Summary);
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
  public void ConfirmDiscardCommand_invokes_cancel_handler()
  {
    var subject = CreateSubject(CurrentWorkspaceLaunchContext(), new RecordingImportTaskCoordinator());
    var cancelInvoked = false;
    subject.SetCancelHandler(() => cancelInvoked = true);

    subject.ConfirmDiscardCommand.Execute(null);

    Assert.True(cancelInvoked);
  }

  [Fact]
  public void SelectExistingCollectionCommand_activates_without_command_parameter()
  {
    var subject = CreateSubject(new ImportLaunchContext(
        IsNewWorkspace: false,
        WorkspaceId: "workspace-1",
        WorkspacePath: @"C:\Data\workspace.tww3c",
        Collections: [new CollectionSummary("collection-1", "Current", 0)],
        SelectedCollectionId: "collection-1"));

    subject.Source.Select(ImportSourceKind.Markdown);
    subject.OpenDestination();
    subject.Destination.SelectLibraryOnly();
    Assert.False(subject.Destination.IsExistingCollection);

    Assert.True(subject.Destination.SelectExistingCollectionCommand.CanExecute(null));
    subject.Destination.SelectExistingCollectionCommand.Execute(null);

    Assert.True(subject.Destination.IsExistingCollection);
    Assert.Equal("collection-1", subject.Destination.SelectedCollectionId);
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
      IImportTaskCoordinator? coordinator = null) =>
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

  private sealed class BlockingResolutionCoordinator : IImportTaskCoordinator
  {
    public int LoadSourceCallCount { get; private set; }
    public int BuildPreviewCallCount { get; private set; }

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
      return Task.FromResult(CreateBlockingPreview(targetContext));
    }

    public Task<ImportPreview> ResolveAsync(
        ImportPreview preview,
        ImportCandidate resolvedCandidate,
        CancellationToken cancellationToken = default)
    {
      var updatedCandidates = preview.Candidates
          .Select(candidate => candidate.CandidateId == resolvedCandidate.CandidateId
              ? resolvedCandidate
              : candidate)
          .ToArray();
      var updatedIssues = (preview.ValidationIssues ?? [])
          .Where(issue => !string.Equals(issue.CandidateId, resolvedCandidate.CandidateId, StringComparison.Ordinal))
          .ToArray();
      var updatedOperations = (preview.Operations ?? [])
          .Select(operation =>
          {
            if (!string.Equals(operation.CandidateId, resolvedCandidate.CandidateId, StringComparison.Ordinal))
            {
              return operation;
            }

            var candidate = updatedCandidates.First(entry =>
                string.Equals(entry.CandidateId, resolvedCandidate.CandidateId, StringComparison.Ordinal));
            return new ImportPreviewOperation(
                operation.CandidateId,
                ImportPreviewRules.InferLibraryAction(candidate),
                ImportPreviewRules.InferMembershipAction(candidate, preview.TargetContext),
                []);
          })
          .ToArray();

      return Task.FromResult(preview with
      {
        Candidates = updatedCandidates,
        ValidationIssues = updatedIssues,
        Operations = updatedOperations
      });
    }

    public Task<ImportOutcome> ApplyAsync(
        ImportPreview preview,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ImportOutcome(preview.TargetContext, [], Applied: true));

    private static ImportPreview CreateBlockingPreview(ImportTargetContext targetContext)
    {
      const string candidateId = "steam:123456789:0";
      return new ImportPreview(
          targetContext,
          [
              ImportCandidate.Unresolved(
                  candidateId,
                  ImportSourceReference.SteamWorkshop("123456789"))
          ],
          Applied: false,
          Operations:
          [
              new ImportPreviewOperation(
                  candidateId,
                  ImportLibraryAction.Conflict,
                  ImportMembershipAction.Blocked,
                  [
                      new ImportValidationIssue(
                          candidateId,
                          "import.source.owner.conflict",
                          "The source identity is already owned by Mod owner-mod.")
                  ])
          ],
          ValidationIssues:
          [
              new ImportValidationIssue(
                  candidateId,
                  "import.source.owner.conflict",
                  "The source identity is already owned by Mod owner-mod.")
          ]);
    }
  }
}
