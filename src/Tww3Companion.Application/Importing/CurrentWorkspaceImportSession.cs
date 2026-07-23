namespace Tww3Companion.Application.Importing;

internal sealed class CurrentWorkspaceImportSession(
    ImportPreview preview,
    IWorkspaceImportStore store)
{
  public Task<ImportOutcome> ApplyAsync(bool confirm, CancellationToken cancellationToken = default)
  {
    if (!confirm) return Task.FromResult(new ImportOutcome(preview.TargetContext, preview.Candidates, Applied: false));

    ImportPreviewValidation.Validate(preview);

    return store.CommitAtomicallyAsync(preview, confirm: true, cancellationToken);
  }
}

internal static class ImportPreviewValidation
{
  public static void Validate(ImportPreview preview)
  {
    if (preview.Candidates.Any(candidate => candidate is not ImportCandidate importCandidate ||
        (importCandidate.IsSkipped && preview.Resolutions?.Any(resolution =>
            resolution.CandidateId == importCandidate.SourceId && resolution.CanSkip) != true) ||
        (!importCandidate.IsSkipped && string.IsNullOrWhiteSpace(importCandidate.LinkedModId) &&
            string.IsNullOrWhiteSpace(importCandidate.DisplayName))))
    {
      throw new InvalidOperationException("All required import candidates must be resolved before applying.");
    }

  }
}
