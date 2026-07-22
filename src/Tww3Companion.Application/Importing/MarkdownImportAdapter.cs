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
      var line = lines[index];
      if (string.IsNullOrWhiteSpace(line)) continue;

      var sourceLine = index + 1;
      var trimmed = line.Trim();
      var (kind, value) = ParseLine(trimmed);
      candidates.Add(new MarkdownImportCandidate(kind, value, line, sourceLine, ExtractSourceReference(value, sourceLine)));
      diagnostics.Add(new MarkdownImportDiagnostic(sourceLine, "Markdown input read."));
    }

    return new MarkdownImportResult(candidates, diagnostics);
  }

  private static (ImportCandidateKind Kind, string Value) ParseLine(string line)
  {
    if (line.StartsWith('#')) return (ImportCandidateKind.CategoryHint, line.TrimStart('#').Trim());
    if (line.StartsWith('-') || line.StartsWith('*')) return (ImportCandidateKind.Candidate, line[1..].Trim());

    return (ImportCandidateKind.Note, line);
  }

  private static MarkdownImportSourceReference? ExtractSourceReference(string value, int sourceLine)
  {
    if (value.Length > 0 && value.All(char.IsDigit)) return new MarkdownImportSourceReference(value, sourceLine);

    if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
        !uri.Host.Equals("steamcommunity.com", StringComparison.OrdinalIgnoreCase) ||
        !(uri.AbsolutePath.Equals("/sharedfiles/filedetails", StringComparison.OrdinalIgnoreCase) ||
          uri.AbsolutePath.Equals("/sharedfiles/filedetails/", StringComparison.OrdinalIgnoreCase)))
    {
      return null;
    }

    var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
    foreach (var item in query)
    {
      var pair = item.Split('=', 2);
      if (pair.Length == 2 && pair[0].Equals("id", StringComparison.OrdinalIgnoreCase) && pair[1].Length > 0 && pair[1].All(char.IsDigit))
      {
        return new MarkdownImportSourceReference(pair[1], sourceLine);
      }
    }

    return null;
  }
}
