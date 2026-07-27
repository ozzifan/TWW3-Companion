using Tww3Companion.Application.Common;
using Tww3Companion.Application.Workspaces.Transfer;
using Tww3Companion.Domain.Workspaces;

namespace Tww3Companion.Desktop.Services;

public enum WorkspaceRestoreDestination
{
  NewWorkspace,
  ReplaceOpenWorkspace
}

public interface IWorkspaceTransferCoordinator
{
  string? LastRestoreDestinationPath { get; }

  Task<OperationResult<string>> BackupAsync(
      string workspacePath,
      string workspaceDisplayName,
      CancellationToken cancellationToken);

  Task<OperationResult<InspectedWorkspaceRestore>> InspectRestoreAsync(
      CancellationToken cancellationToken);

  Task<OperationResult<Workspace>> RestoreNewAsync(
      InspectedWorkspaceRestore inspected,
      CancellationToken cancellationToken);

  Task<OperationResult<Workspace>> ReplaceOpenAsync(
      InspectedWorkspaceRestore inspected,
      string workspacePath,
      CancellationToken cancellationToken);
}
