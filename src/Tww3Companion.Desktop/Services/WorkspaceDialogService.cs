using Avalonia.Controls;
using Avalonia.Layout;
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

    public async Task<string?> PromptForCreateDisplayNameAsync(CancellationToken cancellationToken)
    {
        var owner = topLevelProvider() as Window;
        if (owner is null)
        {
            return null;
        }

        var displayName = new TextBox
        {
            PlaceholderText = "Workspace display name",
            MinWidth = 280
        };
        var createButton = new Button
        {
            Content = "Create Workspace",
            IsDefault = true,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var cancelButton = new Button
        {
            Content = "Cancel",
            IsCancel = true,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var dialog = new Window
        {
            Title = "Create Workspace",
            Width = 420,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Workspace display name",
                        FontWeight = Avalonia.Media.FontWeight.SemiBold
                    },
                    displayName,
                    new TextBlock
                    {
                        Text = "TWW3 Companion Workspace (*.tww3c)",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children =
                        {
                            cancelButton,
                            createButton
                        }
                    }
                }
            }
        };

        createButton.Click += (_, _) => dialog.Close(displayName.Text?.Trim());
        cancelButton.Click += (_, _) => dialog.Close(null);
        using var registration = cancellationToken.Register(() => dialog.Close(null));
        var result = await dialog.ShowDialog<string?>(owner);
        cancellationToken.ThrowIfCancellationRequested();
        return string.IsNullOrWhiteSpace(result) ? null : result;
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
