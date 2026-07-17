namespace Tww3Companion.Domain.Workspaces;

public readonly record struct WorkspaceId
{
    private readonly Guid value;

    private WorkspaceId(Guid value)
    {
        this.value = value;
    }

    public static WorkspaceId Parse(string value)
    {
        if (!Guid.TryParseExact(value, "D", out var guid))
        {
            throw new FormatException("Workspace ID must be a UUID in D format.");
        }

        return new WorkspaceId(guid);
    }

    public static WorkspaceId New() => new(Guid.NewGuid());

    public override string ToString() => value.ToString("D").ToLowerInvariant();
}
