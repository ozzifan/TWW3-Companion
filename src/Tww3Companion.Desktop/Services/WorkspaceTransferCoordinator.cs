using Tww3Companion.Application.Abstractions;
using Tww3Companion.Application.Common;
using Tww3Companion.Application.Workspaces.Transfer;
using Tww3Companion.Domain.Workspaces;

namespace Tww3Companion.Desktop.Services;

public sealed class WorkspaceTransferCoordinator(
    ExportWorkspace exportWorkspace,
    InspectWorkspaceRestore inspectWorkspaceRestore,
    RestoreWorkspace restoreWorkspace,
    IWorkspaceDialogService dialogService,
    IClock clock,
    string defaultWorkspaceDirectory) : IWorkspaceTransferCoordinator
{
  public string? LastRestoreDestinationPath { get; private set; }

  public async Task<OperationResult<string>> BackupAsync(
      string workspacePath,
      string workspaceDisplayName,
      CancellationToken cancellationToken)
  {
    var suggestedFileName = $"{WorkspaceFileName.Sanitize(workspaceDisplayName)}-{clock.UtcNow:yyyy-MM-dd}.json";
    var exportPath = await dialogService.PromptForBackupPathAsync(suggestedFileName, cancellationToken);
    if (string.IsNullOrWhiteSpace(exportPath))
    {
      return Cancelled<string>();
    }

    return await exportWorkspace.ExecuteAsync(workspacePath, exportPath, cancellationToken);
  }

  public async Task<OperationResult<InspectedWorkspaceRestore>> InspectRestoreAsync(
      CancellationToken cancellationToken)
  {
    var exportPath = await dialogService.PromptForRestoreJsonPathAsync(cancellationToken);
    if (string.IsNullOrWhiteSpace(exportPath))
    {
      return Cancelled<InspectedWorkspaceRestore>();
    }

    return await inspectWorkspaceRestore.ExecuteAsync(exportPath, cancellationToken);
  }

  public async Task<OperationResult<Workspace>> RestoreNewAsync(
      InspectedWorkspaceRestore inspected,
      CancellationToken cancellationToken)
  {
    var suggestedFileName = $"{WorkspaceFileName.Sanitize(inspected.Summary.DisplayName)}.tww3c";
    var destinationPath = await dialogService.PromptForRestoredWorkspacePathAsync(
        suggestedFileName,
        cancellationToken);
    if (string.IsNullOrWhiteSpace(destinationPath))
    {
      return Cancelled<Workspace>();
    }

    if (!Path.IsPathRooted(destinationPath))
    {
      destinationPath = Path.Combine(defaultWorkspaceDirectory, destinationPath);
    }

    LastRestoreDestinationPath = destinationPath;
    return await restoreWorkspace.RestoreNewAsync(inspected, destinationPath, cancellationToken);
  }

  public async Task<OperationResult<Workspace>> ReplaceOpenAsync(
      InspectedWorkspaceRestore inspected,
      string workspacePath,
      CancellationToken cancellationToken)
  {
    LastRestoreDestinationPath = workspacePath;
    var confirmed = await dialogService.ConfirmWorkspaceReplacementAsync(
        Path.GetFileNameWithoutExtension(workspacePath),
        inspected.Summary,
        cancellationToken);
    if (!confirmed)
    {
      return new OperationResult<Workspace>.Failure(new OperationError(
          "workspace.restore.unconfirmed",
          "Workspace replacement was not confirmed.",
          false,
          "Review the restore summary and confirm replacement."));
    }

    return await restoreWorkspace.ReplaceAsync(inspected, workspacePath, confirmed: true, cancellationToken);
  }

  private static OperationResult<T>.Failure Cancelled<T>() =>
      new(new OperationError(
          "workspace.transfer.cancelled",
          "The operation was cancelled.",
          false,
          "Try again when ready."));
}
