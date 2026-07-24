namespace Tww3Companion.Application.Importing;

public sealed record ImportPreview(
    ImportTargetContext TargetContext,
    IReadOnlyList<object> Candidates,
    bool Applied,
    IReadOnlyList<ImportResolution>? Resolutions = null,
    IReadOnlyList<ImportValidationIssue>? ValidationIssues = null);
