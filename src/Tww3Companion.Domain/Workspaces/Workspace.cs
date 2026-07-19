using Tww3Companion.Domain.Validation;

namespace Tww3Companion.Domain.Workspaces;

public sealed record Workspace(
    WorkspaceId Id,
    WorkspaceName Name,
    DateTimeOffset CreatedUtc,
    DateTimeOffset ModifiedUtc)
{
  public static ValidationResult<Workspace> Create(
      WorkspaceId id,
      WorkspaceName name,
      DateTimeOffset createdUtc,
      DateTimeOffset modifiedUtc)
  {
    if (modifiedUtc < createdUtc)
    {
      return new ValidationResult<Workspace>.Failure(
          new ValidationError(
              "workspace.modified.before-created",
              "Workspace modification time cannot precede its creation time."));
    }

    return new ValidationResult<Workspace>.Success(new Workspace(id, name, createdUtc, modifiedUtc));
  }
}
