using Tww3Companion.Application.Common;

namespace Tww3Companion.Application.Workspaces.Transfer;

public sealed class ExportWorkspace(IWorkspaceTransferStore store)
{
  public async Task<OperationResult<string>> ExecuteAsync(
      string workspacePath,
      string exportPath,
      CancellationToken cancellationToken)
  {
    var snapshot = await store.ReadSnapshotAsync(workspacePath, cancellationToken);
    if (snapshot is OperationResult<WorkspaceTransferSnapshot>.Failure failure)
    {
      return new OperationResult<string>.Failure(failure.Error);
    }

    return await store.WriteExportAsync(
        ((OperationResult<WorkspaceTransferSnapshot>.Success)snapshot).Value,
        exportPath,
        cancellationToken);
  }
}
