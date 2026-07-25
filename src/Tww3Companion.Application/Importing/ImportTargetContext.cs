namespace Tww3Companion.Application.Importing;

public abstract record ImportTargetContext
{
  private ImportTargetContext()
  {
  }

  public sealed record NewWorkspace(
      string DisplayName,
      string DestinationPath,
      ImportMembershipDestination MembershipDestination) : ImportTargetContext;

  public sealed record CurrentWorkspace(
      string WorkspaceId,
      string WorkspacePath,
      ImportMembershipDestination MembershipDestination) : ImportTargetContext;

  public static ImportTargetContext ForNewWorkspace(
      string displayName,
      string destinationPath,
      ImportMembershipDestination membershipDestination) =>
      new NewWorkspace(displayName, destinationPath, membershipDestination);

  public static ImportTargetContext ForCurrentWorkspace(
      string workspaceId,
      string workspacePath,
      ImportMembershipDestination membershipDestination) =>
      new CurrentWorkspace(workspaceId, workspacePath, membershipDestination);
}
