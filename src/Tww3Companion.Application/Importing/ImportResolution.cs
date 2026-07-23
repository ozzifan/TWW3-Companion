namespace Tww3Companion.Application.Importing;

public sealed record ImportResolution(
    string CandidateId,
    string? LinkedModId,
    string? DisplayName,
    bool CanSkip)
{
  public static ImportResolution RequireLink(string candidateId) =>
      new(candidateId, LinkedModId: null, DisplayName: null, CanSkip: false);

  public static ImportResolution OptionalSkip(string candidateId) =>
      new(candidateId, LinkedModId: null, DisplayName: null, CanSkip: true);
}
