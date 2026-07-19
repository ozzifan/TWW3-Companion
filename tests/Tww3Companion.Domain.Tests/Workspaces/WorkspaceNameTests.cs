using Tww3Companion.Domain.Validation;
using Tww3Companion.Domain.Workspaces;
using Xunit;

namespace Tww3Companion.Domain.Tests.Workspaces;

public sealed class WorkspaceNameTests
{
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void Create_BlankName_ReturnsValidationError(string? value)
  {
    var result = WorkspaceName.Create(value);

    var failure = Assert.IsType<ValidationResult<WorkspaceName>.Failure>(result);
    Assert.Equal("workspace.name.required", failure.Error.Code);
  }

  [Fact]
  public void Create_NonEmptyName_TrimsValue()
  {
    var result = WorkspaceName.Create("  My Workspace  ");

    var success = Assert.IsType<ValidationResult<WorkspaceName>.Success>(result);
    Assert.Equal("My Workspace", success.Value.ToString());
  }

  [Fact]
  public void Create_120UnicodeScalars_Succeeds()
  {
    var value = string.Concat(Enumerable.Repeat("😀", 120));

    var result = WorkspaceName.Create(value);

    var success = Assert.IsType<ValidationResult<WorkspaceName>.Success>(result);
    Assert.Equal(value, success.Value.ToString());
  }

  [Fact]
  public void Create_121UnicodeScalars_ReturnsValidationError()
  {
    var value = string.Concat(Enumerable.Repeat("😀", 121));

    var result = WorkspaceName.Create(value);

    var failure = Assert.IsType<ValidationResult<WorkspaceName>.Failure>(result);
    Assert.Equal("workspace.name.too-long", failure.Error.Code);
  }
}
