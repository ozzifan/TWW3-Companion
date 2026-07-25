using Tww3Companion.Application.Importing;
using Tww3Companion.Desktop.Services;
using Tww3Companion.Desktop.ViewModels;
using Xunit;

namespace Tww3Companion.Desktop.Tests.ViewModels;

public sealed class ImportResolutionViewModelTests
{
  [Fact]
  public async Task LinkAsync_replaces_preview_and_advances_to_next_blocking_row()
  {
    var coordinator = new RecordingCoordinator();
    var preview = CreatePreviewWithBlockingRows();
    var previewViewModel = new ImportPreviewViewModel();
    previewViewModel.Load(preview);
    var subject = new ImportResolutionViewModel(coordinator, previewViewModel);

    await subject.LinkToModAsync("owner-mod", TestContext.Current.CancellationToken);

    Assert.Same(coordinator.LastResolvedPreview, preview);
    Assert.Equal("owner-mod", coordinator.LastResolvedCandidate!.LinkedModId);
    Assert.Equal("unresolved-1", subject.ActiveCandidateId);
    Assert.False(previewViewModel.CanContinue);
  }

  [Fact]
  public async Task SkipAsync_clears_blocking_queue_and_allows_continue()
  {
    var coordinator = new RecordingCoordinator();
    var previewViewModel = new ImportPreviewViewModel();
    previewViewModel.Load(CreatePreviewWithBlockingRows());
    var subject = new ImportResolutionViewModel(coordinator, previewViewModel);

    await subject.SkipAsync(TestContext.Current.CancellationToken);
    await subject.SkipAsync(TestContext.Current.CancellationToken);

    Assert.Null(subject.ActiveCandidateId);
    Assert.True(previewViewModel.CanContinue);
  }

  [Fact]
  public void Source_owner_collision_exposes_only_owner_link_and_skip()
  {
    var previewViewModel = new ImportPreviewViewModel();
    previewViewModel.Load(CreateSourceOwnerConflictPreview());
    var subject = new ImportResolutionViewModel(new RecordingCoordinator(), previewViewModel);

    Assert.Equal("owner-mod", subject.SourceOwnerModId);
    Assert.True(subject.CanLinkToOwner);
    Assert.True(subject.CanSkip);
    Assert.False(subject.CanCreate);
    Assert.False(subject.CanChooseScalarValue);
  }

  [Fact]
  public async Task Scalar_conflict_requires_explicit_selected_value()
  {
    var coordinator = new RecordingCoordinator();
    var previewViewModel = new ImportPreviewViewModel();
    previewViewModel.Load(CreateScalarConflictPreview());
    var subject = new ImportResolutionViewModel(coordinator, previewViewModel);

    Assert.Equal(["Imported Name", "Existing Name"], subject.CompetingScalarValues);
    Assert.False(subject.CanApplyResolution);

    subject.SelectedScalarValue = "Imported Name";
    Assert.True(subject.CanApplyResolution);

    await subject.ApplyScalarSelectionAsync(TestContext.Current.CancellationToken);

    Assert.Equal("Imported Name", coordinator.LastResolvedCandidate!.DisplayName);
  }

  private static ImportPreview CreateSourceOwnerConflictPreview()
  {
    var target = ImportTargetContext.ForCurrentWorkspace(
        "workspace-1",
        @"C:\Data\workspace.tww3c",
        ImportMembershipDestination.ForLibraryOnly());

    return new ImportPreview(
        target,
        [
            ImportCandidate.Linked(
                "conflict-1",
                "different-mod",
                ImportSourceReference.SteamWorkshop("123"))
        ],
        Applied: false,
        Operations:
        [
            new ImportPreviewOperation(
                "conflict-1",
                ImportLibraryAction.Conflict,
                ImportMembershipAction.Blocked,
                [
                    new ImportValidationIssue(
                        "conflict-1",
                        "import.source.owner.conflict",
                        "The source identity is already owned by Mod owner-mod.")
                ])
        ],
        ValidationIssues:
        [
            new ImportValidationIssue(
                "conflict-1",
                "import.source.owner.conflict",
                "The source identity is already owned by Mod owner-mod.")
        ]);
  }

  private static ImportPreview CreateScalarConflictPreview()
  {
    var target = ImportTargetContext.ForCurrentWorkspace(
        "workspace-1",
        @"C:\Data\workspace.tww3c",
        ImportMembershipDestination.ForLibraryOnly());

    return new ImportPreview(
        target,
        [ImportCandidate.CreateWithDisplayName("scalar-1", "Imported Name")],
        Applied: false,
        Operations:
        [
            new ImportPreviewOperation(
                "scalar-1",
                ImportLibraryAction.Conflict,
                ImportMembershipAction.Blocked,
                [
                    new ImportValidationIssue(
                        "scalar-1",
                        "import.scalar.conflict",
                        "Imported Name|Existing Name")
                ])
        ]);
  }

  private static ImportPreview CreatePreviewWithBlockingRows()
  {
    var target = ImportTargetContext.ForCurrentWorkspace(
        "workspace-1",
        @"C:\Data\workspace.tww3c",
        ImportMembershipDestination.ForLibraryOnly());

    return new ImportPreview(
        target,
        [
            ImportCandidate.Unresolved(
                "conflict-1",
                ImportSourceReference.SteamWorkshop("123")),
            ImportCandidate.Unresolved(
                "unresolved-1",
                ImportSourceReference.SteamWorkshop("456"))
        ],
        Applied: false,
        Operations:
        [
            new ImportPreviewOperation(
                "conflict-1",
                ImportLibraryAction.Conflict,
                ImportMembershipAction.Blocked,
                [
                    new ImportValidationIssue(
                        "conflict-1",
                        "import.source.owner.conflict",
                        "The source identity is already owned by Mod owner-mod.")
                ]),
            new ImportPreviewOperation(
                "unresolved-1",
                ImportLibraryAction.Create,
                ImportMembershipAction.None,
                [])
        ],
        ValidationIssues:
        [
            new ImportValidationIssue(
                "conflict-1",
                "import.source.owner.conflict",
                "The source identity is already owned by Mod owner-mod.")
        ]);
  }

  private sealed class RecordingCoordinator : IImportTaskCoordinator
  {
    public ImportPreview? LastResolvedPreview { get; private set; }
    public ImportCandidate? LastResolvedCandidate { get; private set; }
    public ImportPreview ResolveResult { get; set; } = CreateResolvedPreview();

    public Task<ImportSourceLoadResult> LoadSourceAsync(
        ImportSourceRequest request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<ImportPreview> BuildPreviewAsync(
        ImportTargetContext targetContext,
        IReadOnlyList<object> candidates,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<ImportPreview> ResolveAsync(
        ImportPreview preview,
        ImportCandidate resolvedCandidate,
        CancellationToken cancellationToken = default)
    {
      LastResolvedPreview = preview;
      LastResolvedCandidate = resolvedCandidate;
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

      ResolveResult = preview with
      {
        Candidates = updatedCandidates,
        ValidationIssues = updatedIssues,
        Operations = updatedOperations
      };
      return Task.FromResult(ResolveResult);
    }

    public Task<ImportOutcome> ApplyAsync(
        ImportPreview preview,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    private static ImportPreview CreateResolvedPreview() =>
        new(
            ImportTargetContext.ForCurrentWorkspace(
                "workspace-1",
                @"C:\Data\workspace.tww3c",
                ImportMembershipDestination.ForLibraryOnly()),
            [],
            Applied: false);
  }
}
