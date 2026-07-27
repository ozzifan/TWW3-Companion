using Tww3Companion.Application.Workspaces.Transfer;

namespace Tww3Companion.Desktop.Services;

public interface IWorkspaceDialogService
{
  Task<string?> PromptForCreateDisplayNameAsync(CancellationToken cancellationToken);

  Task<string?> PromptForOpenPathAsync(CancellationToken cancellationToken);

  Task<string?> PromptForBackupPathAsync(
      string suggestedFileName,
      CancellationToken cancellationToken);

  Task<string?> PromptForRestoreJsonPathAsync(
      CancellationToken cancellationToken);

  Task<string?> PromptForRestoredWorkspacePathAsync(
      string suggestedFileName,
      CancellationToken cancellationToken);

  Task<bool> ConfirmWorkspaceReplacementAsync(
      string currentWorkspaceName,
      WorkspaceRestoreSummary source,
      CancellationToken cancellationToken);
}
