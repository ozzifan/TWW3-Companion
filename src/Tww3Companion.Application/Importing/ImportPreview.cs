namespace Tww3Companion.Application.Importing;

public sealed record ImportPreview(
    ImportTargetContext TargetContext,
    IReadOnlyList<ImportCandidate> Candidates,
    bool Applied,
    IReadOnlyList<ImportResolution>? Resolutions = null,
    IReadOnlyList<ImportValidationIssue>? ValidationIssues = null,
    IReadOnlyList<ImportPreviewOperation>? Operations = null,
    int WarningCount = 0);
