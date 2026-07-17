using Tww3Companion.Domain.Workspaces;
using Xunit;

namespace Tww3Companion.Domain.Tests.Workspaces;

public sealed class WorkspaceIdTests
{
    [Fact]
    public void Parse_UppercaseUuid_ReturnsCanonicalLowercaseText()
    {
        var id = WorkspaceId.Parse("6F9619FF-8B86-D011-B42D-00C04FC964FF");

        Assert.Equal("6f9619ff-8b86-d011-b42d-00c04fc964ff", id.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-uuid")]
    [InlineData("{6f9619ff-8b86-d011-b42d-00c04fc964ff}")]
    public void Parse_InvalidUuid_ThrowsFormatException(string value)
    {
        Assert.Throws<FormatException>(() => WorkspaceId.Parse(value));
    }

    [Fact]
    public void New_ReturnsCanonicalUuidText()
    {
        var id = WorkspaceId.New();

        Assert.True(Guid.TryParseExact(id.ToString(), "D", out var parsed));
        Assert.NotEqual(Guid.Empty, parsed);
        Assert.Equal(id.ToString(), id.ToString().ToLowerInvariant());
    }
}
