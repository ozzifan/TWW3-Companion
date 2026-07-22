namespace Tww3Companion.Application.Importing;

public enum ImportCandidateKind
{
  CategoryHint,
  Candidate,
  Note
}

public sealed record MarkdownImportSourceReference(string WorkshopId, int SourceLine);

public sealed record MarkdownImportCandidate(
    ImportCandidateKind Kind,
    string Value,
    string Text,
    int SourceLine,
    MarkdownImportSourceReference? SourceReference = null);
