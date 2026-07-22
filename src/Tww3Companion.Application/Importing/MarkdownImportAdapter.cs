namespace Tww3Companion.Application.Importing;

public static class MarkdownImportAdapter
{
  public static MarkdownImportResult Parse(string markdown)
  {
    ArgumentNullException.ThrowIfNull(markdown);

    var candidates = new List<MarkdownImportCandidate>();
    var diagnostics = new List<MarkdownImportDiagnostic>();
    var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    for (var index = 0; index < lines.Length; index++)
    {
      if (string.IsNullOrWhiteSpace(lines[index])) continue;

      var sourceLine = index + 1;
      candidates.Add(new MarkdownImportCandidate(lines[index], sourceLine));
      diagnostics.Add(new MarkdownImportDiagnostic(sourceLine, "Markdown input read."));
    }

    return new MarkdownImportResult(candidates, diagnostics);
  }
}
