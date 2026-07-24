namespace Tww3Companion.Application.Importing;

public sealed record ImportCandidate(
    string CandidateId,
    ImportSourceReference? SourceReference,
    string? LinkedModId,
    string? DisplayName,
    bool IsSkipped,
    string? SuggestedModId = null)
{
  public static ImportCandidate Linked(
      string candidateId,
      string linkedModId,
      ImportSourceReference? sourceReference = null) =>
      new(
          candidateId,
          sourceReference,
          linkedModId,
          DisplayName: null,
          IsSkipped: false);

  public static ImportCandidate CreateWithDisplayName(
      string candidateId,
      string displayName,
      ImportSourceReference? sourceReference = null) =>
      new(
          candidateId,
          sourceReference,
          LinkedModId: null,
          displayName,
          IsSkipped: false);

  public static ImportCandidate Skipped(string candidateId) =>
      new(
          candidateId,
          SourceReference: null,
          LinkedModId: null,
          DisplayName: null,
          IsSkipped: true);
}
