using Tww3Companion.Application.Common;
using Tww3Companion.Domain.Workspaces;

namespace Tww3Companion.Application.Workspaces.Transfer;

public interface IWorkspaceTransferStore
{
  Task<OperationResult<WorkspaceTransferSnapshot>> ReadSnapshotAsync(
      string workspacePath,
      CancellationToken cancellationToken);

  Task<OperationResult<WorkspaceTransferSnapshot>> ReadExportAsync(
      string exportPath,
      CancellationToken cancellationToken);

  Task<OperationResult<string>> WriteExportAsync(
      WorkspaceTransferSnapshot snapshot,
      string exportPath,
      CancellationToken cancellationToken);

  Task<OperationResult<Workspace>> RestoreNewAsync(
      WorkspaceTransferSnapshot snapshot,
      string destinationPath,
      CancellationToken cancellationToken);

  Task<OperationResult<Workspace>> ReplaceAsync(
      WorkspaceTransferSnapshot snapshot,
      string destinationPath,
      CancellationToken cancellationToken);
}
