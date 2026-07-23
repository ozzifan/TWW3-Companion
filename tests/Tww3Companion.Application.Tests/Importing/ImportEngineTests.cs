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

    Assert.Equal("My New Workspace", newWorkspace.DisplayName);
    Assert.Equal("workspace-id-123", currentWorkspace.WorkspaceId);
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
