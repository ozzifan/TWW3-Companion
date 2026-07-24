using System.Reflection;
using Tww3Companion.Application.Workspaces;
using Xunit;

namespace Tww3Companion.Application.Tests.Workspaces;

public sealed class WorkspaceQueryTests
{
  [Fact]
  public void IWorkspaceQuery_GetLibrarySnapshotAsync_ReturnsTaskOfWorkspaceLibrarySnapshot()
  {
    var method = typeof(IWorkspaceQuery).GetMethod(
        "GetLibrarySnapshotAsync",
        BindingFlags.Instance | BindingFlags.Public,
        binder: null,
        types: [typeof(CancellationToken)],
        modifiers: null);

    Assert.NotNull(method);
    Assert.Equal(typeof(Task<WorkspaceLibrarySnapshot>), method!.ReturnType);
  }

  [Fact]
  public async Task GetLibrarySnapshotAsync_passes_active_workspace_path_to_catalog_reader()
  {
    var reader = new RecordingCatalogReader();
    var query = new WorkspaceLibraryQuery(reader);
    const string workspacePath = "C:\\Workspaces\\current.tww3c";

    query.SetActiveWorkspacePath(workspacePath);
    await query.GetLibrarySnapshotAsync(TestContext.Current.CancellationToken);

    Assert.Equal(workspacePath, reader.LastPath);
  }

  [Fact]
  public async Task GetLibrarySnapshotAsync_returns_empty_snapshot_when_no_active_path()
  {
    var reader = new RecordingCatalogReader();
    var query = new WorkspaceLibraryQuery(reader);

    var snapshot = await query.GetLibrarySnapshotAsync(TestContext.Current.CancellationToken);

    Assert.Empty(snapshot.Mods);
    Assert.Empty(snapshot.Collections);
    Assert.Empty(snapshot.Memberships);
    Assert.Null(reader.LastPath);
  }

  private sealed class RecordingCatalogReader : IWorkspaceCatalogReader
  {
    public string? LastPath { get; private set; }

    public Task<WorkspaceLibrarySnapshot> ReadLibrarySnapshotAsync(
        string workspacePath,
        CancellationToken cancellationToken = default)
    {
      LastPath = workspacePath;
      return Task.FromResult(new WorkspaceLibrarySnapshot([], [], []));
    }
  }
}
