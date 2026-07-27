using Tww3Companion.Application.Common;

namespace Tww3Companion.Application.Workspaces.Transfer;

public sealed class InspectWorkspaceRestore(IWorkspaceTransferStore store)
{
  public async Task<OperationResult<InspectedWorkspaceRestore>> ExecuteAsync(
      string exportPath,
      CancellationToken cancellationToken)
  {
    var read = await store.ReadExportAsync(exportPath, cancellationToken);
    if (read is OperationResult<WorkspaceTransferSnapshot>.Failure failure)
    {
      return new OperationResult<InspectedWorkspaceRestore>.Failure(failure.Error);
    }

    var snapshot = ((OperationResult<WorkspaceTransferSnapshot>.Success)read).Value;
    var summary = new WorkspaceRestoreSummary(
        snapshot.Workspace.Id,
        snapshot.Workspace.DisplayName,
        snapshot.Format,
        snapshot.Mods.Count,
        snapshot.Collections.Count,
        snapshot.Memberships.Count);
    return new OperationResult<InspectedWorkspaceRestore>.Success(
        new InspectedWorkspaceRestore(exportPath, snapshot, summary));
  }
}
