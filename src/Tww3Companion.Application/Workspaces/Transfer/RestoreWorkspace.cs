using Tww3Companion.Application.Common;
using Tww3Companion.Domain.Workspaces;

namespace Tww3Companion.Application.Workspaces.Transfer;

public sealed class RestoreWorkspace(IWorkspaceTransferStore store)
{
  public async Task<OperationResult<Workspace>> RestoreNewAsync(
      InspectedWorkspaceRestore inspected,
      string destinationPath,
      CancellationToken cancellationToken)
  {
    var current = await store.ReadExportAsync(inspected.ExportPath, cancellationToken);
    if (current is OperationResult<WorkspaceTransferSnapshot>.Failure failure)
    {
      return new OperationResult<Workspace>.Failure(failure.Error);
    }

    if (!WorkspaceTransferValidation.ContentEquals(
            inspected.Snapshot,
            ((OperationResult<WorkspaceTransferSnapshot>.Success)current).Value))
    {
      return SourceChangedFailure();
    }

    return await store.RestoreNewAsync(
        ((OperationResult<WorkspaceTransferSnapshot>.Success)current).Value,
        destinationPath,
        cancellationToken);
  }

  public async Task<OperationResult<Workspace>> ReplaceAsync(
      InspectedWorkspaceRestore inspected,
      string destinationPath,
      bool confirmed,
      CancellationToken cancellationToken)
  {
    if (!confirmed)
    {
      return new OperationResult<Workspace>.Failure(new OperationError(
          "workspace.restore.unconfirmed",
          "Workspace replacement was not confirmed.",
          false,
          "Review the restore summary and confirm replacement."));
    }

    var current = await store.ReadExportAsync(inspected.ExportPath, cancellationToken);
    if (current is OperationResult<WorkspaceTransferSnapshot>.Failure failure)
    {
      return new OperationResult<Workspace>.Failure(failure.Error);
    }

    if (!WorkspaceTransferValidation.ContentEquals(
            inspected.Snapshot,
            ((OperationResult<WorkspaceTransferSnapshot>.Success)current).Value))
    {
      return SourceChangedFailure();
    }

    return await store.ReplaceAsync(
        ((OperationResult<WorkspaceTransferSnapshot>.Success)current).Value,
        destinationPath,
        cancellationToken);
  }

  private static OperationResult<Workspace>.Failure SourceChangedFailure() =>
      new(new OperationError(
          "workspace.restore.source.changed",
          "The selected export changed after inspection.",
          false,
          "Choose the export again and retry restore."));
}
