using Tww3Companion.Application.Importing;
using Xunit;

namespace Tww3Companion.Application.Tests.Importing;

public sealed class MarkdownImportServiceTests
{
  [Fact]
  public async Task MarkdownImport_preview_does_not_write_until_confirmed()
  {
    var result = MarkdownImportAdapter.Parse("""
        # Test Collection

        - https://steamcommunity.com/sharedfiles/filedetails/?id=123456789
        """);

    var preview = await MarkdownImportService.BuildPreviewAsync(result);

    Assert.NotNull(preview);
    Assert.False(preview.Applied);
  }

  [Fact]
  public async Task MarkdownImport_rolls_back_when_validation_fails()
  {
    var result = MarkdownImportAdapter.Parse("""
        # Test Collection

        - invalid candidate text that cannot validate
        """);

    await Assert.ThrowsAsync<ImportValidationException>(() => MarkdownImportService.ApplyAsync(result, confirm: true));
  }
}
