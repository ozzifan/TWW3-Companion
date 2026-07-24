namespace Tww3Companion.Application.Importing;

internal sealed class CurrentWorkspaceImportSession(
    ImportPreview preview,
    IWorkspaceImportStore store)
{
  private readonly ImportTargetContext.CurrentWorkspace _targetContext = preview.TargetContext as ImportTargetContext.CurrentWorkspace
      ?? throw new ArgumentException("The preview must target the current Workspace.", nameof(preview));

  public async Task<ImportOutcome> ApplyAsync(bool confirm, CancellationToken cancellationToken = default)
  {
    if (!confirm) return new ImportOutcome(preview.TargetContext, preview.Candidates, Applied: false);

    ImportPreviewValidation.Validate(preview);

    foreach (var linkedModId in preview.Candidates
        .OfType<ImportCandidate>()
        .Where(candidate => !candidate.IsSkipped && !string.IsNullOrWhiteSpace(candidate.LinkedModId))
        .Select(candidate => candidate.LinkedModId!))
    {
      if (!await store.ModExistsAsync(_targetContext, linkedModId, cancellationToken))
      {
        throw new InvalidOperationException("All linked import candidates must resolve existing Mods before applying.");
      }
    }

    return await store.CommitAtomicallyAsync(preview, confirm: true, cancellationToken);
  }
}

internal static class ImportPreviewValidation
{
  public static void Validate(ImportPreview preview)
  {
    if (preview.ValidationIssues?.Count > 0)
    {
      throw new InvalidOperationException("The import preview contains validation issues.");
    }

    if (preview.Candidates.Any(candidate => candidate is not ImportCandidate importCandidate ||
        (importCandidate.IsSkipped && preview.Resolutions?.Any(resolution =>
            resolution.CandidateId == importCandidate.CandidateId && resolution.CanSkip) != true) ||
        (!importCandidate.IsSkipped && string.IsNullOrWhiteSpace(importCandidate.LinkedModId) &&
            string.IsNullOrWhiteSpace(importCandidate.DisplayName))))
    {
      throw new InvalidOperationException("All required import candidates must be resolved before applying.");
    }
  }
}
