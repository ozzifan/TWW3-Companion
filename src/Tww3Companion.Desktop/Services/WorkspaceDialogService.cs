using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Tww3Companion.Desktop.Services;

public sealed class WorkspaceDialogService(Func<TopLevel?> topLevelProvider) : IWorkspaceDialogService
{
    private static readonly FilePickerFileType WorkspaceFileType = new("TWW3 Companion Workspace")
    {
        Patterns = ["*.tww3c"]
    };

    public WorkspaceDialogService(TopLevel topLevel) : this(() => topLevel)
    {
    }

    public Task<string?> PromptForCreateDisplayNameAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<string?>("New Workspace");
    }

    public async Task<string?> PromptForOpenPathAsync(CancellationToken cancellationToken)
    {
        var topLevel = topLevelProvider();
        if (topLevel is null)
        {
            return null;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Workspace",
            AllowMultiple = false,
            FileTypeFilter = [WorkspaceFileType]
        });
        cancellationToken.ThrowIfCancellationRequested();
        return files.Count == 0 ? null : files[0].Path.LocalPath;
    }
}
