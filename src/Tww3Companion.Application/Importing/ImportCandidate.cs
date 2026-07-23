namespace Tww3Companion.Application.Importing;

public sealed record ImportCandidate(
    string SourceId,
    string? LinkedModId,
    string? DisplayName,
    bool IsSkipped,
    string? SuggestedModId = null)
{
  public static ImportCandidate Linked(string sourceId, string linkedModId) =>
      new(sourceId, linkedModId, DisplayName: null, IsSkipped: false);

  public static ImportCandidate CreateWithDisplayName(string sourceId, string displayName) =>
      new(sourceId, LinkedModId: null, displayName, IsSkipped: false);

  public static ImportCandidate Skipped(string sourceId) =>
      new(sourceId, LinkedModId: null, DisplayName: null, IsSkipped: true);
}
