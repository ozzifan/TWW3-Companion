namespace Tww3Companion.Application.Importing;

public sealed record MarkdownImportResult(
    IReadOnlyList<MarkdownImportCandidate> Candidates,
    IReadOnlyList<MarkdownImportDiagnostic> Diagnostics,
    bool Applied = false);
