namespace Tww3Companion.Application.Workspaces;

public sealed class WorkspaceLibraryQuery(
    IWorkspaceCatalogReader catalogReader) : IWorkspaceQuery
{
  private static readonly WorkspaceLibrarySnapshot EmptySnapshot =
      new([], [], []);

  private string? activeWorkspacePath;

  public void SetActiveWorkspacePath(string? path) =>
      activeWorkspacePath =
          string.IsNullOrWhiteSpace(path) ? null : path;

  public Task<WorkspaceLibrarySnapshot> GetLibrarySnapshotAsync(
      CancellationToken cancellationToken) =>
      activeWorkspacePath is null
          ? Task.FromResult(EmptySnapshot)
          : catalogReader.ReadLibrarySnapshotAsync(
              activeWorkspacePath,
              cancellationToken);
}
