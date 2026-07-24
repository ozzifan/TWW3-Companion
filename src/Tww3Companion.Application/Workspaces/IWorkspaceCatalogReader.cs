namespace Tww3Companion.Application.Workspaces;

public interface IWorkspaceCatalogReader
{
  Task<WorkspaceLibrarySnapshot> ReadLibrarySnapshotAsync(
      string workspacePath,
      CancellationToken cancellationToken = default);
}
