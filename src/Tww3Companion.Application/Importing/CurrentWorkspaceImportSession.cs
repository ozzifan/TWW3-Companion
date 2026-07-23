namespace Tww3Companion.Application.Importing;

internal sealed class CurrentWorkspaceImportSession(
    ImportTargetContext.CurrentWorkspace targetContext,
    IReadOnlyList<object> candidates)
{
  public ImportPreview BuildPreview() =>
      new(targetContext, candidates, Applied: false);

  public ImportOutcome Apply(bool confirm) =>
      new(targetContext, candidates, Applied: confirm && candidates.All(candidate => candidate is not null));
}
