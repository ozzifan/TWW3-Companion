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
    var engine = new FakeImportEngine();
    var preview = await engine.BuildPreviewAsync(
        ImportTargetContext.ForCurrentWorkspace("workspace-id-123"),
        new object[] { "candidate-1" },
        TestContext.Current.CancellationToken);

    Assert.NotNull(preview);
  }

  [Fact]
  public async Task CurrentWorkspace_import_builds_preview_without_changing_the_workspace()
  {
    var engine = new ImportEngine();
    var target = ImportTargetContext.ForCurrentWorkspace("workspace-id-123");

    var preview = await engine.BuildPreviewAsync(target, new object[] { "candidate-1" }, TestContext.Current.CancellationToken);

    Assert.False(preview.Applied);
  }

  [Fact]
  public async Task CurrentWorkspace_import_applies_atomically_when_confirmed()
  {
    var engine = new ImportEngine();
    var target = ImportTargetContext.ForCurrentWorkspace("workspace-id-123");
    var preview = await engine.BuildPreviewAsync(target, new object[] { "candidate-1" }, TestContext.Current.CancellationToken);

    var outcome = await engine.ApplyAsync(preview, confirm: true, TestContext.Current.CancellationToken);

    Assert.True(outcome.Applied);
  }

  [Fact]
  public async Task CurrentWorkspace_import_does_not_apply_without_confirmation()
  {
    var engine = new ImportEngine();
    var preview = await engine.BuildPreviewAsync(
        ImportTargetContext.ForCurrentWorkspace("workspace-id-123"),
        new object[] { "candidate-1" },
        TestContext.Current.CancellationToken);

    var outcome = await engine.ApplyAsync(preview, confirm: false, TestContext.Current.CancellationToken);

    Assert.False(outcome.Applied);
  }

  [Fact]
  public async Task CurrentWorkspace_import_does_not_apply_with_an_unresolved_candidate()
  {
    var engine = new ImportEngine();
    var preview = await engine.BuildPreviewAsync(
        ImportTargetContext.ForCurrentWorkspace("workspace-id-123"),
        new object[] { null! },
        TestContext.Current.CancellationToken);

    var outcome = await engine.ApplyAsync(preview, confirm: true, TestContext.Current.CancellationToken);

    Assert.False(outcome.Applied);
  }

  private sealed class FakeImportEngine : IImportEngine
  {
    public Task<ImportPreview> BuildPreviewAsync(
        ImportTargetContext targetContext,
        IReadOnlyList<object> candidates,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ImportPreview(targetContext, candidates, Applied: false));

    public Task<ImportOutcome> ApplyAsync(
        ImportPreview preview,
        bool confirm,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ImportOutcome(preview.TargetContext, preview.Candidates, confirm));
  }
}
