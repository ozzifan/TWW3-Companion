using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Tww3Companion.Application.Workspaces.Transfer;

namespace Tww3Companion.Desktop.Services;

public sealed class WorkspaceDialogService(Func<TopLevel?> topLevelProvider) : IWorkspaceDialogService
{
  private static readonly FilePickerFileType WorkspaceFileType = new("TWW3 Companion Workspace")
  {
    Patterns = ["*.tww3c"]
  };

  private static readonly FilePickerFileType WorkspaceExportFileType = new("Workspace JSON backup")
  {
    Patterns = ["*.json"]
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

  public async Task<string?> PromptForBackupPathAsync(
      string suggestedFileName,
      CancellationToken cancellationToken)
  {
    var topLevel = topLevelProvider();
    if (topLevel is null)
    {
      return null;
    }

    var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
    {
      Title = "Backup Workspace",
      SuggestedFileName = suggestedFileName,
      DefaultExtension = "json",
      FileTypeChoices = [WorkspaceExportFileType]
    });
    cancellationToken.ThrowIfCancellationRequested();
    return file?.Path.LocalPath;
  }

  public async Task<string?> PromptForRestoreJsonPathAsync(CancellationToken cancellationToken)
  {
    var topLevel = topLevelProvider();
    if (topLevel is null)
    {
      return null;
    }

    var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
    {
      Title = "Restore Workspace backup",
      AllowMultiple = false,
      FileTypeFilter = [WorkspaceExportFileType]
    });
    cancellationToken.ThrowIfCancellationRequested();
    return files.Count == 0 ? null : files[0].Path.LocalPath;
  }

  public async Task<string?> PromptForRestoredWorkspacePathAsync(
      string suggestedFileName,
      CancellationToken cancellationToken)
  {
    var topLevel = topLevelProvider();
    if (topLevel is null)
    {
      return null;
    }

    var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
    {
      Title = "Restore Workspace",
      SuggestedFileName = suggestedFileName,
      DefaultExtension = "tww3c",
      FileTypeChoices = [WorkspaceFileType]
    });
    cancellationToken.ThrowIfCancellationRequested();
    return file?.Path.LocalPath;
  }

  public async Task<bool> ConfirmWorkspaceReplacementAsync(
      string currentWorkspaceName,
      WorkspaceRestoreSummary source,
      CancellationToken cancellationToken)
  {
    var owner = topLevelProvider() as Window;
    if (owner is null)
    {
      return false;
    }

    var confirmButton = new Button
    {
      Content = "Replace Workspace",
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
      Title = "Replace open Workspace",
      Width = 480,
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
            Text = "Replace open Workspace",
            FontWeight = Avalonia.Media.FontWeight.SemiBold
          },
          new TextBlock
          {
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Text = $"Current Workspace: {currentWorkspaceName}"
          },
          new TextBlock
          {
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Text = $"Backup Workspace: {source.DisplayName}"
          },
          new TextBlock
          {
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Text = $"Format: {source.Format}; {source.ModCount} Mods; {source.CollectionCount} Collections; {source.MembershipCount} Memberships"
          },
          new TextBlock
          {
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Text = "This replaces the complete open Workspace. It does not merge data."
          },
          new StackPanel
          {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children =
            {
              cancelButton,
              confirmButton
            }
          }
        }
      }
    };

    confirmButton.Click += (_, _) => dialog.Close(true);
    cancelButton.Click += (_, _) => dialog.Close(false);
    using var registration = cancellationToken.Register(() => dialog.Close(false));
    var result = await dialog.ShowDialog<bool>(owner);
    cancellationToken.ThrowIfCancellationRequested();
    return result;
  }
}
