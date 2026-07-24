namespace Tww3Companion.Application.Importing;

public sealed record ImportValidationIssue(
    string CandidateId,
    string Code,
    string Message);
