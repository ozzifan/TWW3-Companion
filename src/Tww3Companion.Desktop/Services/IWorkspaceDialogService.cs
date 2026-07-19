namespace Tww3Companion.Desktop.Services;

public interface IWorkspaceDialogService
{
  Task<string?> PromptForCreateDisplayNameAsync(CancellationToken cancellationToken);

  Task<string?> PromptForOpenPathAsync(CancellationToken cancellationToken);
}
