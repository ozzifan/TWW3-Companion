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

  [Fact]
  public void ParseMarkdown_treats_heading_as_category_hint()
  {
    var result = MarkdownImportAdapter.Parse("""
        # Skaven

        - Clanrats
        """);

    Assert.Contains(result.Candidates, c => c.Kind == ImportCandidateKind.CategoryHint && c.Value == "Skaven");
  }

  [Fact]
  public void ParseMarkdown_extracts_workshop_id_from_link()
  {
    var result = MarkdownImportAdapter.Parse("""
        - https://steamcommunity.com/sharedfiles/filedetails/?id=123456789
        """);

    Assert.Contains(result.Candidates, c => c.SourceReference?.WorkshopId == "123456789");
  }

  [Fact]
  public void ParseMarkdown_extracts_workshop_id_from_link_without_trailing_slash()
  {
    var result = MarkdownImportAdapter.Parse("""
        - https://steamcommunity.com/sharedfiles/filedetails?id=123456789
        """);

    Assert.Contains(result.Candidates, c => c.SourceReference?.WorkshopId == "123456789");
  }

  [Fact]
  public void ParseMarkdown_treats_bullet_as_candidate_entry()
  {
    var result = MarkdownImportAdapter.Parse("""
        * Clanrats
        """);

    Assert.Contains(result.Candidates, c => c.Kind == ImportCandidateKind.Candidate && c.Value == "Clanrats");
  }

  [Fact]
  public void ParseMarkdown_extracts_workshop_id_from_bare_numeric_bullet()
  {
    var result = MarkdownImportAdapter.Parse("""
        - 123456789
        """);

    Assert.Contains(result.Candidates, c => c.SourceReference?.WorkshopId == "123456789" && c.SourceReference.SourceLine == 1);
  }

  [Fact]
  public void ParseMarkdown_does_not_create_source_reference_for_empty_bullet()
  {
    var result = MarkdownImportAdapter.Parse("""
        -
        """);

    Assert.DoesNotContain(result.Candidates, c => c.SourceReference is not null);
  }

  [Fact]
  public void ParseMarkdown_preserves_free_form_prose_as_notes()
  {
    var result = MarkdownImportAdapter.Parse("""
        This list is mostly for late-game testing.
        """);

    Assert.Contains(result.Candidates, c => c.Kind == ImportCandidateKind.Note && c.Text.Contains("late-game testing"));
  }
}
