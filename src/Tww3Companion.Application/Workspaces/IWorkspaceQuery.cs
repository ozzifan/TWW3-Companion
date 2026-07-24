namespace Tww3Companion.Application.Workspaces;

public interface IWorkspaceQuery
{
  Task<WorkspaceLibrarySnapshot> GetLibrarySnapshotAsync(CancellationToken cancellationToken);
}
