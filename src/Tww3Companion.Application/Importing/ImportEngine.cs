namespace Tww3Companion.Application.Importing;

public sealed class ImportEngine(IWorkspaceImportStore store) : IImportEngine
{
  private readonly IWorkspaceImportStore _store = store ?? throw new ArgumentNullException(nameof(store));

  public async Task<ImportPreview> BuildPreviewAsync(
      ImportTargetContext targetContext,
      IReadOnlyList<object> candidates,
      CancellationToken cancellationToken = default)
  {
    var importCandidates = NormalizeCandidates(candidates);
    var existingCandidates = await _store.ReadCandidatesAsync(targetContext, cancellationToken);
    var matchedCandidates = MatchExactSourceReferences(importCandidates, existingCandidates);

    return await _store.SavePreviewAsync(
        targetContext,
        matchedCandidates,
        matchedCandidates.Select(candidate => new ImportResolution(
            candidate.SourceId,
            candidate.LinkedModId,
            candidate.DisplayName,
            CanSkip: false)).ToArray(),
        cancellationToken);
  }

  public Task<ImportOutcome> ApplyAsync(
      ImportPreview preview,
      bool confirm,
      CancellationToken cancellationToken = default)
  {
    if (preview.TargetContext is ImportTargetContext.CurrentWorkspace)
    {
      return new CurrentWorkspaceImportSession(preview, _store).ApplyAsync(confirm, cancellationToken);
    }

    if (!confirm) return Task.FromResult(new ImportOutcome(preview.TargetContext, preview.Candidates, Applied: false));

    ImportPreviewValidation.Validate(preview.Candidates);
    return _store.CommitAtomicallyAsync(preview, confirm: true, cancellationToken);
  }

  private static IReadOnlyList<ImportCandidate> NormalizeCandidates(IReadOnlyList<object> candidates) =>
      candidates.Select(candidate => candidate switch
      {
        ImportCandidate importCandidate => importCandidate,
        SteamImportCandidate steamCandidate => ImportCandidate.CreateWithDisplayName(
            steamCandidate.SourceReference,
            steamCandidate.DisplayName ?? steamCandidate.SourceReference),
        MarkdownImportCandidate { Kind: ImportCandidateKind.Candidate } markdownCandidate =>
            ImportCandidate.CreateWithDisplayName(
                markdownCandidate.SourceReference?.WorkshopId ?? $"markdown:{markdownCandidate.SourceLine}",
                markdownCandidate.Value),
        MarkdownImportCandidate => throw new ArgumentException("Only Markdown candidate entries can enter the import pipeline.", nameof(candidates)),
        _ => throw new ArgumentException("Unsupported import candidate type.", nameof(candidates))
      }).ToArray();

  private static IReadOnlyList<ImportCandidate> MatchExactSourceReferences(
      IReadOnlyList<ImportCandidate> candidates,
      IReadOnlyList<ImportCandidate> existingCandidates) =>
      candidates.Select(candidate =>
      {
        var match = existingCandidates.FirstOrDefault(existing =>
            existing.SourceId == candidate.SourceId && !string.IsNullOrWhiteSpace(existing.LinkedModId));
        return match is null || !string.IsNullOrWhiteSpace(candidate.LinkedModId)
            ? candidate
            : candidate with { LinkedModId = match.LinkedModId };
      }).ToArray();
}
