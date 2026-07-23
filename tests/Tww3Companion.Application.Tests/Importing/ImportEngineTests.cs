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
    var engine = new FakeImportEngine(new FakeWorkspaceImportStore());
    var preview = await engine.BuildPreviewAsync(
        ImportTargetContext.ForCurrentWorkspace("workspace-id-123"),
        new object[] { "candidate-1" },
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
    var engine = new FakeImportEngine(store);
    var preview = await engine.BuildPreviewAsync(
        ImportTargetContext.ForCurrentWorkspace("workspace-id-123"),
        new object[] { ImportCandidate.Linked("source-1", "mod-1") },
        TestContext.Current.CancellationToken);

    Assert.NotNull(preview);
    Assert.True(store.ReadCandidatesCalled);
  }

  private sealed class FakeImportEngine : IImportEngine
  {
    public FakeImportEngine(IWorkspaceImportStore store) => Store = store;

    private IWorkspaceImportStore Store { get; }

    public Task<ImportPreview> BuildPreviewAsync(
        ImportTargetContext targetContext,
        IReadOnlyList<object> candidates,
        CancellationToken cancellationToken = default) =>
        BuildPreviewCoreAsync(targetContext, candidates, cancellationToken);

    public Task<ImportOutcome> ApplyAsync(
        ImportPreview preview,
        bool confirm,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ImportOutcome(preview.TargetContext, preview.Candidates, confirm));

    private async Task<ImportPreview> BuildPreviewCoreAsync(
        ImportTargetContext targetContext,
        IReadOnlyList<object> candidates,
        CancellationToken cancellationToken)
    {
      await Store.ReadCandidatesAsync(targetContext, cancellationToken);
      return new ImportPreview(targetContext, candidates, Applied: false);
    }
  }

  private sealed class FakeWorkspaceImportStore : IWorkspaceImportStore
  {
    public bool ReadCandidatesCalled { get; private set; }

    public Task<IReadOnlyList<ImportCandidate>> ReadCandidatesAsync(
        ImportTargetContext targetContext,
        CancellationToken cancellationToken = default)
    {
      ReadCandidatesCalled = true;
      return Task.FromResult<IReadOnlyList<ImportCandidate>>([]);
    }

    public Task<ImportPreview> SavePreviewAsync(
        ImportTargetContext targetContext,
        IReadOnlyList<ImportCandidate> candidates,
        IReadOnlyList<ImportResolution> resolutions,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ImportPreview(targetContext, candidates, Applied: false));

    public Task<ImportOutcome> CommitAsync(
        ImportPreview preview,
        bool confirm,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ImportOutcome(preview.TargetContext, preview.Candidates, confirm));
  }
}
