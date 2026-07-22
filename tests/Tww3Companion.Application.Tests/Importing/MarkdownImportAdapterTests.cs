using Tww3Companion.Application.Importing;
using Xunit;

namespace Tww3Companion.Application.Tests.Importing;

public sealed class MarkdownImportAdapterTests
{
  [Fact]
  public void ParseMarkdown_returns_candidates_and_diagnostics_for_input_text()
  {
    var result = MarkdownImportAdapter.Parse("""
        # My Collection

        - https://steamcommunity.com/sharedfiles/filedetails/?id=123456789
        - Keep these notes with the collection
        """);

    Assert.NotEmpty(result.Candidates);
    Assert.Contains(result.Diagnostics, diagnostic => diagnostic.SourceLine == 1);
  }
}
