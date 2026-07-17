using Tww3Companion.Domain.Validation;
using Tww3Companion.Domain.Workspaces;
using Xunit;

namespace Tww3Companion.Domain.Tests.Workspaces;

public sealed class WorkspaceTests
{
    [Fact]
    public void Create_ValidMetadata_ReturnsImmutableWorkspace()
    {
        var id = Assert.IsType<ValidationResult<WorkspaceId>.Success>(
            WorkspaceId.Parse("6f9619ff-8b86-4d11-b42d-00c04fc964ff")).Value;
        var name = Assert.IsType<ValidationResult<WorkspaceName>.Success>(
            WorkspaceName.Create("Campaign notes")).Value;
        var created = new DateTimeOffset(2026, 7, 18, 1, 0, 0, TimeSpan.Zero);
        var modified = created.AddMinutes(1);

        var result = Workspace.Create(id, name, created, modified);

        var success = Assert.IsType<ValidationResult<Workspace>.Success>(result);
        Assert.Equal(new Workspace(id, name, created, modified), success.Value);
    }

    [Fact]
    public void Create_ModifiedBeforeCreated_ReturnsValidationError()
    {
        var created = new DateTimeOffset(2026, 7, 18, 1, 0, 0, TimeSpan.Zero);

        var result = Workspace.Create(
            WorkspaceId.New(),
            Assert.IsType<ValidationResult<WorkspaceName>.Success>(
                WorkspaceName.Create("Campaign notes")).Value,
            created,
            created.AddTicks(-1));

        var failure = Assert.IsType<ValidationResult<Workspace>.Failure>(result);
        Assert.Equal("workspace.modified.before-created", failure.Error.Code);
    }
}
