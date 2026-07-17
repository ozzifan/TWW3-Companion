using Tww3Companion.Domain.Workspaces;
using Xunit;

namespace Tww3Companion.Domain.Tests.Workspaces;

public sealed class WorkspaceTests
{
    [Fact]
    public void Create_ValidMetadata_ReturnsImmutableWorkspace()
    {
        var id = WorkspaceId.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff");
        var name = WorkspaceName.Create("Campaign notes").Value;
        var created = new DateTimeOffset(2026, 7, 18, 1, 0, 0, TimeSpan.Zero);
        var modified = created.AddMinutes(1);

        var result = Workspace.Create(id, name, created, modified);

        Assert.True(result.IsSuccess);
        Assert.Equal(new Workspace(id, name, created, modified), result.Value);
    }

    [Fact]
    public void Create_ModifiedBeforeCreated_ReturnsValidationError()
    {
        var created = new DateTimeOffset(2026, 7, 18, 1, 0, 0, TimeSpan.Zero);

        var result = Workspace.Create(
            WorkspaceId.New(),
            WorkspaceName.Create("Campaign notes").Value,
            created,
            created.AddTicks(-1));

        Assert.False(result.IsSuccess);
        Assert.Equal("workspace.modified.before-created", result.Error.Code);
    }
}
