namespace Tww3Companion.Application.Importing;

internal sealed class CurrentWorkspaceImportSession(
    ImportTargetContext.CurrentWorkspace targetContext,
    IReadOnlyList<ImportCandidate> candidates,
    IWorkspaceImportStore store)
{
  public ImportPreview BuildPreview() => new(targetContext, candidates, Applied: false);

  public Task<ImportOutcome> ApplyAsync(bool confirm, CancellationToken cancellationToken = default)
  {
    if (!confirm) return Task.FromResult(new ImportOutcome(targetContext, candidates, Applied: false));

    if (candidates.Any(candidate =>
        candidate.IsSkipped ||
        (string.IsNullOrWhiteSpace(candidate.LinkedModId) && string.IsNullOrWhiteSpace(candidate.DisplayName))))
    {
      throw new InvalidOperationException("All required import candidates must be resolved before applying.");
    }

    return store.CommitAtomicallyAsync(BuildPreview(), confirm: true, cancellationToken);
  }
}
