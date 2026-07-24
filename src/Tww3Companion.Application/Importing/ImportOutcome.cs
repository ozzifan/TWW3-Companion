namespace Tww3Companion.Application.Importing;

public sealed record ImportOutcome(
    ImportTargetContext TargetContext,
    IReadOnlyList<object> Candidates,
    bool Applied);
