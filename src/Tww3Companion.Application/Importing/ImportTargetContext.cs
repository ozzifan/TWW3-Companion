namespace Tww3Companion.Application.Importing;

public abstract record ImportTargetContext
{
  private ImportTargetContext()
  {
  }

  public sealed record NewWorkspace(
      string DisplayName,
      string DestinationPath,
      string CollectionDisplayName) : ImportTargetContext;

  public sealed record CurrentWorkspace(
      string WorkspaceId,
      string WorkspacePath,
      string CollectionId) : ImportTargetContext;

  public static ImportTargetContext ForNewWorkspace(
      string displayName,
      string destinationPath,
      string collectionDisplayName) =>
      new NewWorkspace(displayName, destinationPath, collectionDisplayName);

  public static ImportTargetContext ForCurrentWorkspace(
      string workspaceId,
      string workspacePath,
      string collectionId) =>
      new CurrentWorkspace(workspaceId, workspacePath, collectionId);
}
