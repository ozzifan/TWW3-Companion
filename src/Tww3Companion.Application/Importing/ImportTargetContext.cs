namespace Tww3Companion.Application.Importing;

public sealed record ImportTargetContext(
    string? WorkspaceId,
    string? DisplayName,
    string? DestinationPath,
    bool CreateNewWorkspace)
{
  public static ImportTargetContext ForNewWorkspace(string displayName, string destinationPath) =>
      new(null, displayName, destinationPath, true);

  public static ImportTargetContext ForCurrentWorkspace(string workspaceId) =>
      new(workspaceId, null, null, false);
}
