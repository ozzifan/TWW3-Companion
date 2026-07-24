using Tww3Companion.Application.Common;

namespace Tww3Companion.Application.Workspaces;

public sealed class WorkspaceLibraryQuery(IWorkspaceStore workspaceStore) : IWorkspaceQuery
{
  private static readonly WorkspaceLibrarySnapshot EmptySnapshot = new([], [], []);

  private string? activeWorkspacePath;

  public void SetActiveWorkspacePath(string? path) =>
      activeWorkspacePath = string.IsNullOrWhiteSpace(path) ? null : path;

  public async Task<WorkspaceLibrarySnapshot> GetLibrarySnapshotAsync(CancellationToken cancellationToken)
  {
    if (activeWorkspacePath is null)
    {
      return EmptySnapshot;
    }

    var result = await workspaceStore.OpenAsync(activeWorkspacePath, cancellationToken);
    if (result is OperationResult<Domain.Workspaces.Workspace>.Failure failure)
    {
      throw new InvalidOperationException(failure.Error.Message);
    }

    // Schema v1 has no mod/collection tables yet. Opening the workspace validates the file
    // without mutating it; the overlay is empty until a later persistence slice adds catalog rows.
    return EmptySnapshot;
  }
}
