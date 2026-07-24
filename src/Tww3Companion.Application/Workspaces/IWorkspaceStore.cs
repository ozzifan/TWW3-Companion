using Tww3Companion.Application.Common;
using Tww3Companion.Domain.Workspaces;

namespace Tww3Companion.Application.Workspaces;

public interface IWorkspaceStore : IWorkspaceCatalogReader
{
  Task<OperationResult<Workspace>> CreateAsync(
      string path,
      Workspace workspace,
      CancellationToken cancellationToken);

  Task<OperationResult<Workspace>> OpenAsync(
      string path,
      CancellationToken cancellationToken);

  async Task<WorkspaceLibrarySnapshot> IWorkspaceCatalogReader.ReadLibrarySnapshotAsync(
      string workspacePath,
      CancellationToken cancellationToken)
  {
    var result = await OpenAsync(workspacePath, cancellationToken);
    if (result is OperationResult<Workspace>.Failure failure)
    {
      throw new InvalidOperationException(failure.Error.Message);
    }

    // Schema v1 has no mod/collection tables yet. Opening the workspace validates the file
    // without mutating it; the overlay is empty until a later persistence slice adds catalog rows.
    return new WorkspaceLibrarySnapshot([], [], []);
  }
}
