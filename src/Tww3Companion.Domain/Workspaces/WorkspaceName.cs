using Tww3Companion.Domain.Validation;

namespace Tww3Companion.Domain.Workspaces;

public readonly record struct WorkspaceName
{
    private const int MaximumLength = 200;
    private readonly string value;

    private WorkspaceName(string value)
    {
        this.value = value;
    }

    public static ValidationResult<WorkspaceName> Create(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return ValidationResult<WorkspaceName>.Failure(
                new ValidationError("workspace.name.required", "Workspace name is required."));
        }

        if (trimmed.EnumerateRunes().Count() > MaximumLength)
        {
            return ValidationResult<WorkspaceName>.Failure(
                new ValidationError("workspace.name.too-long", "Workspace name cannot exceed 200 characters."));
        }

        return ValidationResult<WorkspaceName>.Success(new WorkspaceName(trimmed));
    }

    public override string ToString() => value;
}
