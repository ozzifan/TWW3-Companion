namespace Tww3Companion.Application.Workspaces;

public interface IWorkspaceQuery
{
  Task<WorkspaceLibrarySnapshot> GetSnapshotAsync(CancellationToken cancellationToken);
}
