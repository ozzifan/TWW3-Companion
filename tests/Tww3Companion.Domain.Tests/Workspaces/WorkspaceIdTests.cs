using Tww3Companion.Domain.Validation;
using Tww3Companion.Domain.Workspaces;
using Xunit;

namespace Tww3Companion.Domain.Tests.Workspaces;

public sealed class WorkspaceIdTests
{
  [Fact]
  public void Parse_UppercaseUuid_ReturnsCanonicalLowercaseText()
  {
    var result = WorkspaceId.Parse("6F9619FF-8B86-4D11-B42D-00C04FC964FF");
    var success = Assert.IsType<ValidationResult<WorkspaceId>.Success>(result);

    Assert.Equal("6f9619ff-8b86-4d11-b42d-00c04fc964ff", success.Value.ToString());
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("not-a-uuid")]
  [InlineData("{6f9619ff-8b86-4d11-b42d-00c04fc964ff}")]
  [InlineData("6f9619ff-8b86-d011-b42d-00c04fc964ff")]
  [InlineData("6f9619ff-8b86-4d11-742d-00c04fc964ff")]
  public void Parse_InvalidOrNonVersion4Uuid_ReturnsTypedFailure(string? value)
  {
    var result = WorkspaceId.Parse(value);

    var failure = Assert.IsType<ValidationResult<WorkspaceId>.Failure>(result);
    Assert.Equal("workspace.id.invalid", failure.Error.Code);
  }

  [Fact]
  public void New_ReturnsCanonicalUuidText()
  {
    var id = WorkspaceId.New();
    var text = id.ToString();

    Assert.True(Guid.TryParseExact(text, "D", out var parsed));
    Assert.NotEqual(Guid.Empty, parsed);
    Assert.Equal('4', text[14]);
    Assert.Contains(text[19], "89ab");
    Assert.Equal(text, text.ToLowerInvariant());
  }
}
