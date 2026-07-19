namespace Tww3Companion.Domain.Workspaces;

public readonly record struct WorkspaceId
{
  private readonly Guid value;

  private WorkspaceId(Guid value)
  {
    this.value = value;
  }

  public static Validation.ValidationResult<WorkspaceId> Parse(string? value)
  {
    if (!Guid.TryParseExact(value, "D", out var guid))
    {
      return Invalid();
    }

    var canonical = guid.ToString("D").ToLowerInvariant();
    if (canonical[14] != '4' || !"89ab".Contains(canonical[19]))
    {
      return Invalid();
    }

    return new Validation.ValidationResult<WorkspaceId>.Success(new WorkspaceId(guid));
  }

  public static WorkspaceId New() => new(Guid.NewGuid());

  public override string ToString() => value.ToString("D").ToLowerInvariant();

  private static Validation.ValidationResult<WorkspaceId> Invalid() =>
      new Validation.ValidationResult<WorkspaceId>.Failure(
          new Validation.ValidationError(
              "workspace.id.invalid",
              "Workspace ID must be an RFC 4122 version 4 UUID in D format."));
}
