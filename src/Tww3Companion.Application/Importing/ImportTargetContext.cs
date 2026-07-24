namespace Tww3Companion.Application.Importing;

public abstract record ImportTargetContext
{
  private ImportTargetContext()
  {
  }

  public sealed record NewWorkspace(string DisplayName, string DestinationPath) : ImportTargetContext;

  public sealed record CurrentWorkspace(string WorkspaceId) : ImportTargetContext;

  public static ImportTargetContext ForNewWorkspace(string displayName, string destinationPath) =>
      new NewWorkspace(displayName, destinationPath);

  public static ImportTargetContext ForCurrentWorkspace(string workspaceId) =>
      new CurrentWorkspace(workspaceId);
}
