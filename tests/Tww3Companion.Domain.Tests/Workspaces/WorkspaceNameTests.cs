using Tww3Companion.Domain.Workspaces;
using Xunit;

namespace Tww3Companion.Domain.Tests.Workspaces;

public sealed class WorkspaceNameTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankName_ReturnsValidationError(string value)
    {
        var result = WorkspaceName.Create(value);

        Assert.False(result.IsSuccess);
        Assert.Equal("workspace.name.required", result.Error.Code);
    }

    [Fact]
    public void Create_NonEmptyName_TrimsValue()
    {
        var result = WorkspaceName.Create("  My Workspace  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("My Workspace", result.Value.ToString());
    }

    [Fact]
    public void Create_200UnicodeScalars_Succeeds()
    {
        var value = string.Concat(Enumerable.Repeat("😀", 200));

        var result = WorkspaceName.Create(value);

        Assert.True(result.IsSuccess);
        Assert.Equal(value, result.Value.ToString());
    }

    [Fact]
    public void Create_201UnicodeScalars_ReturnsValidationError()
    {
        var value = string.Concat(Enumerable.Repeat("😀", 201));

        var result = WorkspaceName.Create(value);

        Assert.False(result.IsSuccess);
        Assert.Equal("workspace.name.too-long", result.Error.Code);
    }
}
