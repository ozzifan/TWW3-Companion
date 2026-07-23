using Tww3Companion.Application.Importing;
using Xunit;

namespace Tww3Companion.Application.Tests.Importing;

public sealed class ImportEngineTests
{
  [Fact]
  public void ImportTargetContext_can_represent_new_workspace_and_current_workspace()
  {
    var newWorkspace = ImportTargetContext.ForNewWorkspace("My New Workspace", "C:\\Workspaces\\my-new.tww3c");
    var currentWorkspace = ImportTargetContext.ForCurrentWorkspace("workspace-id-123");

    Assert.IsType<ImportTargetContext.NewWorkspace>(newWorkspace);
    Assert.IsType<ImportTargetContext.CurrentWorkspace>(currentWorkspace);

    var typedNewWorkspace = Assert.IsType<ImportTargetContext.NewWorkspace>(newWorkspace);
    var typedCurrentWorkspace = Assert.IsType<ImportTargetContext.CurrentWorkspace>(currentWorkspace);

    Assert.Equal("My New Workspace", typedNewWorkspace.DisplayName);
    Assert.Equal("C:\\Workspaces\\my-new.tww3c", typedNewWorkspace.DestinationPath);
    Assert.Equal("workspace-id-123", typedCurrentWorkspace.WorkspaceId);
  }

  [Fact]
  public async Task ImportEngine_builds_preview_from_candidates_and_target_context()
  {
    var engine = new ImportEngine(new FakeWorkspaceImportStore());
    var preview = await engine.BuildPreviewAsync(
        ImportTargetContext.ForCurrentWorkspace("workspace-id-123"),
        new object[] { ImportCandidate.Linked("source-1", "mod-1") },
        TestContext.Current.CancellationToken);

    Assert.NotNull(preview);
  }

  [Fact]
  public void ImportCandidate_can_represent_link_create_and_skip()
  {
    var linked = ImportCandidate.Linked("source-1", "mod-1");
    var created = ImportCandidate.CreateWithDisplayName("source-2", "New Mod");
    var skipped = ImportCandidate.Skipped("source-3");

    Assert.Equal("source-1", linked.SourceId);
    Assert.Equal("mod-1", linked.LinkedModId);
    Assert.Equal("New Mod", created.DisplayName);
    Assert.True(skipped.IsSkipped);
  }

  [Fact]
  public void ImportResolution_can_represent_required_and_optional_resolutions()
  {
    var required = ImportResolution.RequireLink("candidate-1");
    var optional = ImportResolution.OptionalSkip("candidate-2");

    Assert.Equal("candidate-1", required.CandidateId);
    Assert.True(optional.CanSkip);
  }

  [Fact]
  public async Task ImportEngine_builds_preview_through_a_store_port()
  {
    var store = new FakeWorkspaceImportStore();
    var engine = new ImportEngine(store);
    var preview = await engine.BuildPreviewAsync(
        ImportTargetContext.ForCurrentWorkspace("workspace-id-123"),
        new object[] { ImportCandidate.Linked("source-1", "mod-1") },
        TestContext.Current.CancellationToken);

    Assert.NotNull(preview);
    Assert.True(store.ReadCandidatesCalled);
  }

  [Fact]
  public async Task NewWorkspace_import_requires_a_display_name_and_destination_path()
  {
    var store = new FakeWorkspaceImportStore();
    var engine = new ImportEngine(store);
    var target = ImportTargetContext.ForNewWorkspace("My New Workspace", "C:\\Workspaces\\my-new.tww3c");

    var preview = await engine.BuildPreviewAsync(
        target,
        new object[] { ImportCandidate.Linked("source-1", "mod-1") },
        TestContext.Current.CancellationToken);

    Assert.Equal("My New Workspace", preview.TargetContext is ImportTargetContext.NewWorkspace newTarget ? newTarget.DisplayName : "");
    await Assert.ThrowsAsync<ArgumentException>(() => engine.BuildPreviewAsync(
        ImportTargetContext.ForNewWorkspace("", "C:\\Workspaces\\my-new.tww3c"),
        new object[] { ImportCandidate.Linked("source-1", "mod-1") },
        TestContext.Current.CancellationToken));
    await Assert.ThrowsAsync<ArgumentException>(() => engine.BuildPreviewAsync(
        ImportTargetContext.ForNewWorkspace("My New Workspace", ""),
        new object[] { ImportCandidate.Linked("source-1", "mod-1") },
        TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task NewWorkspace_import_applies_into_the_new_workspace()
  {
    var store = new FakeWorkspaceImportStore();
    var engine = new ImportEngine(store);
    var target = ImportTargetContext.ForNewWorkspace("My New Workspace", "C:\\Workspaces\\my-new.tww3c");
    var preview = await engine.BuildPreviewAsync(
        target,
        new object[] { ImportCandidate.Linked("source-1", "mod-1") },
        TestContext.Current.CancellationToken);

    var outcome = await engine.ApplyAsync(preview, confirm: true, TestContext.Current.CancellationToken);

    Assert.True(outcome.Applied);
    Assert.True(store.CreateNewWorkspaceCalled);
    Assert.IsType<ImportTargetContext.CurrentWorkspace>(store.CommittedPreview!.TargetContext);
  }

  [Fact]
  public async Task NewWorkspace_import_does_not_match_against_the_open_workspace()
  {
    var store = new FakeWorkspaceImportStore
    {
      ExistingCandidates = [ImportCandidate.Linked("source-1", "open-workspace-mod")]
    };
    var engine = new ImportEngine(store);

    var preview = await engine.BuildPreviewAsync(
        ImportTargetContext.ForNewWorkspace("My New Workspace", "C:\\Workspaces\\my-new.tww3c"),
        new object[] { ImportCandidate.CreateWithDisplayName("source-1", "Imported mod") },
        TestContext.Current.CancellationToken);

    Assert.Null(Assert.IsType<ImportCandidate>(Assert.Single(preview.Candidates)).LinkedModId);
  }

  [Fact]
  public async Task NewWorkspace_import_rolls_back_when_persistence_fails()
  {
    var store = new FakeWorkspaceImportStore { CommitFailure = new InvalidOperationException("Persistence failed.") };
    var engine = new ImportEngine(store);
    var preview = await engine.BuildPreviewAsync(
        ImportTargetContext.ForNewWorkspace("My New Workspace", "C:\\Workspaces\\my-new.tww3c"),
        new object[] { ImportCandidate.Linked("source-1", "mod-1") },
        TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<InvalidOperationException>(
        () => engine.ApplyAsync(preview, confirm: true, TestContext.Current.CancellationToken));

    Assert.True(store.RollbackNewWorkspaceCalled);
  }

  [Fact]
  public async Task ImportEngine_applies_the_confirmed_preview_without_rebuilding_it()
  {
    var store = new FakeWorkspaceImportStore();
    var engine = new ImportEngine(store);
    var preview = await engine.BuildPreviewAsync(
        ImportTargetContext.ForCurrentWorkspace("workspace-id-123"),
        new object[] { ImportCandidate.Linked("source-1", "mod-1") },
        TestContext.Current.CancellationToken);

    await engine.ApplyAsync(preview, confirm: true, TestContext.Current.CancellationToken);

    Assert.Same(preview, store.CommittedPreview);
  }

  [Fact]
  public async Task ImportEngine_normalizes_and_exactly_matches_source_candidates_before_preview()
  {
    var store = new FakeWorkspaceImportStore
    {
      ExistingCandidates = [ImportCandidate.Linked("source-1", "matched-mod-1")]
    };
    var engine = new ImportEngine(store);

    var preview = await engine.BuildPreviewAsync(
        ImportTargetContext.ForCurrentWorkspace("workspace-id-123"),
        new object[] { new SteamImportCandidate("source-1", "Imported mod") },
        TestContext.Current.CancellationToken);

    var candidate = Assert.IsType<ImportCandidate>(Assert.Single(preview.Candidates));
    Assert.Equal("matched-mod-1", candidate.LinkedModId);
  }

  [Fact]
  public async Task ImportEngine_suggests_a_unique_display_name_match_without_linking_it()
  {
    var store = new FakeWorkspaceImportStore
    {
      ExistingCandidates = [ImportCandidate.Linked("existing-source-1", "existing-mod-1") with { DisplayName = "Existing Mod" }]
    };
    var engine = new ImportEngine(store);

    var preview = await engine.BuildPreviewAsync(
        ImportTargetContext.ForCurrentWorkspace("workspace-id-123"),
        new object[] { ImportCandidate.CreateWithDisplayName("source-1", "existing mod") },
        TestContext.Current.CancellationToken);

    var candidate = Assert.IsType<ImportCandidate>(Assert.Single(preview.Candidates));
    Assert.Null(candidate.LinkedModId);
    Assert.Equal("existing-mod-1", candidate.SuggestedModId);
  }

  [Fact]
  public async Task CurrentWorkspace_import_requires_all_required_resolutions()
  {
    var store = new FakeWorkspaceImportStore();
    var engine = new ImportEngine(store);
    var target = ImportTargetContext.ForCurrentWorkspace("workspace-id-123");

    var preview = await engine.BuildPreviewAsync(
        target,
        new object[] { new ImportCandidate("source-1", LinkedModId: null, DisplayName: null, IsSkipped: false) },
        TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<InvalidOperationException>(
        () => engine.ApplyAsync(preview, confirm: true, TestContext.Current.CancellationToken));
  }

  [Fact]
  public async Task CurrentWorkspace_import_requires_linked_mod_to_exist_before_committing()
  {
    var store = new FakeWorkspaceImportStore();
    var engine = new ImportEngine(store);
    var target = ImportTargetContext.ForCurrentWorkspace("workspace-id-123");
    var preview = await engine.BuildPreviewAsync(
        target,
        new object[] { ImportCandidate.Linked("source-1", "nonexistent-mod") },
        TestContext.Current.CancellationToken);

    await Assert.ThrowsAsync<InvalidOperationException>(
        () => engine.ApplyAsync(preview, confirm: true, TestContext.Current.CancellationToken));

    Assert.False(store.CommitAtomicallyCalled);
  }

  [Fact]
  public async Task CurrentWorkspace_import_allows_optional_skips()
  {
    var store = new FakeWorkspaceImportStore();
    var engine = new ImportEngine(store);
    var target = ImportTargetContext.ForCurrentWorkspace("workspace-id-123");
    var preview = await engine.BuildPreviewAsync(
        target,
        new object[] { ImportCandidate.Skipped("source-1") },
        TestContext.Current.CancellationToken);

    Assert.True(Assert.Single(preview.Resolutions!).CanSkip);

    var outcome = await engine.ApplyAsync(preview, confirm: true, TestContext.Current.CancellationToken);

    Assert.True(outcome.Applied);
  }

  [Fact]
  public async Task CurrentWorkspace_import_commits_all_changes_atomically()
  {
    var store = new FakeWorkspaceImportStore();
    var engine = new ImportEngine(store);
    var target = ImportTargetContext.ForCurrentWorkspace("workspace-id-123");
    var preview = await engine.BuildPreviewAsync(
        target,
        new object[] { ImportCandidate.Linked("source-1", "mod-1") },
        TestContext.Current.CancellationToken);

    var outcome = await engine.ApplyAsync(preview, confirm: true, TestContext.Current.CancellationToken);

    Assert.True(outcome.Applied);
    Assert.True(store.CommitAtomicallyCalled);
  }

  private sealed class FakeWorkspaceImportStore : IWorkspaceImportStore
  {
    public bool ReadCandidatesCalled { get; private set; }

    public IReadOnlyList<ImportCandidate> ExistingCandidates { get; init; } = [];

    public IReadOnlySet<string> ExistingModIds { get; init; } = new HashSet<string>(StringComparer.Ordinal) { "mod-1" };

    public ImportPreview? CommittedPreview { get; private set; }

    public bool CreateNewWorkspaceCalled { get; private set; }

    public bool RollbackNewWorkspaceCalled { get; private set; }

    public Exception? CommitFailure { get; init; }

    public Task<IReadOnlyList<ImportCandidate>> ReadCandidatesAsync(
        ImportTargetContext targetContext,
      CancellationToken cancellationToken = default)
    {
      ReadCandidatesCalled = true;
      return Task.FromResult(ExistingCandidates);
    }

    public Task<bool> ModExistsAsync(
        ImportTargetContext.CurrentWorkspace targetContext,
        string modId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ExistingModIds.Contains(modId));

    public Task<ImportTargetContext.CurrentWorkspace> CreateNewWorkspaceAsync(
        ImportTargetContext.NewWorkspace targetContext,
        CancellationToken cancellationToken = default)
    {
      CreateNewWorkspaceCalled = true;
      return Task.FromResult((ImportTargetContext.CurrentWorkspace)ImportTargetContext.ForCurrentWorkspace("new-workspace-id"));
    }

    public Task RollbackNewWorkspaceAsync(
        ImportTargetContext.CurrentWorkspace targetContext,
        CancellationToken cancellationToken = default)
    {
      RollbackNewWorkspaceCalled = true;
      return Task.CompletedTask;
    }

    public Task<ImportPreview> SavePreviewAsync(
        ImportTargetContext targetContext,
        IReadOnlyList<ImportCandidate> candidates,
        IReadOnlyList<ImportResolution> resolutions,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ImportPreview(targetContext, candidates, Applied: false));

    public bool CommitAtomicallyCalled { get; private set; }

    public Task<ImportOutcome> CommitAtomicallyAsync(
        ImportPreview preview,
        bool confirm,
        CancellationToken cancellationToken = default)
    {
      CommitAtomicallyCalled = true;
      if (CommitFailure is not null) throw CommitFailure;
      CommittedPreview = preview;
      return Task.FromResult(new ImportOutcome(preview.TargetContext, preview.Candidates, confirm));
    }
  }
}
