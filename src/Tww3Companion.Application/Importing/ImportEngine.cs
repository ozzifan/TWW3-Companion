namespace Tww3Companion.Application.Importing;

public sealed class ImportEngine(IWorkspaceImportStore store) : IImportEngine
{
  private readonly IWorkspaceImportStore _store = store ?? throw new ArgumentNullException(nameof(store));

  public async Task<ImportPreview> BuildPreviewAsync(
      ImportTargetContext targetContext,
      IReadOnlyList<object> candidates,
      CancellationToken cancellationToken = default)
  {
    if (targetContext is ImportTargetContext.NewWorkspace newWorkspace)
    {
      NewWorkspaceImportSession.ValidateDestination(newWorkspace);
    }

    var importCandidates = NormalizeCandidates(candidates);
    var existingCandidates = targetContext is ImportTargetContext.NewWorkspace
        ? []
        : await _store.ReadCandidatesAsync(targetContext, cancellationToken);
    var matchedCandidates = MatchExactSourceReferences(importCandidates, existingCandidates);
    var suggestedCandidates = SuggestNameMatches(matchedCandidates, existingCandidates);
    var validationIssues = DetectSourceOwnerConflicts(suggestedCandidates, existingCandidates);
    var resolutions = suggestedCandidates.Select(candidate => new ImportResolution(
        candidate.CandidateId,
        candidate.LinkedModId,
        candidate.DisplayName,
        CanSkip: string.IsNullOrWhiteSpace(candidate.LinkedModId))).ToArray();

    var preview = await _store.SavePreviewAsync(
        targetContext,
        suggestedCandidates,
        resolutions,
        cancellationToken);
    return preview with { Resolutions = resolutions, ValidationIssues = validationIssues };
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

    if (preview.TargetContext is ImportTargetContext.NewWorkspace)
    {
      return new NewWorkspaceImportSession(preview, _store).ApplyAsync(confirm, cancellationToken);
    }

    throw new ArgumentException("Unsupported import target context.", nameof(preview));
  }

  private static IReadOnlyList<ImportCandidate> NormalizeCandidates(
      IReadOnlyList<object> candidates) =>
      candidates.Select((candidate, index) => candidate switch
      {
        SteamImportCandidate steamCandidate =>
            CreateSteamCandidate(steamCandidate, index),

        MarkdownImportCandidate
        {
          Kind: ImportCandidateKind.Candidate,
          SourceReference: { } source
        } markdownCandidate =>
            ImportCandidate.CreateWithDisplayName(
                $"markdown:{markdownCandidate.SourceLine}",
                markdownCandidate.Value,
                ImportSourceReference.SteamWorkshop(source.WorkshopId)),

        MarkdownImportCandidate
        {
          Kind: ImportCandidateKind.Candidate
        } markdownCandidate =>
            ImportCandidate.CreateWithDisplayName(
                $"markdown:{markdownCandidate.SourceLine}",
                markdownCandidate.Value),

        ImportCandidate importCandidate => importCandidate,

        MarkdownImportCandidate =>
            throw new ArgumentException(
                "Only Markdown candidate entries can enter the import pipeline.",
                nameof(candidates)),

        _ => throw new ArgumentException(
            $"Unsupported import candidate type: {candidate?.GetType().FullName ?? "<null>"}.",
            nameof(candidates))
      }).ToArray();

  private static ImportCandidate CreateSteamCandidate(
      SteamImportCandidate candidate,
      int index)
  {
    if (!SteamImportAdapter.TryGetWorkshopItemId(
            candidate.SourceReference,
            out var workshopItemId))
    {
      throw new ArgumentException(
          "Steam candidates require a numeric Workshop ID or supported Workshop URL.",
          nameof(candidate));
    }

    return ImportCandidate.CreateWithDisplayName(
        $"steam:{workshopItemId}:{index}",
        candidate.DisplayName ?? candidate.SourceReference,
        ImportSourceReference.SteamWorkshop(workshopItemId));
  }

  private static IReadOnlyList<ImportCandidate> MatchExactSourceReferences(
      IReadOnlyList<ImportCandidate> candidates,
      IReadOnlyList<ImportCandidate> existingCandidates) =>
      candidates.Select(candidate =>
      {
        var match = existingCandidates.FirstOrDefault(existing =>
            SourceReferencesMatch(existing.SourceReference, candidate.SourceReference) &&
            !string.IsNullOrWhiteSpace(existing.LinkedModId));
        return match is null || !string.IsNullOrWhiteSpace(candidate.LinkedModId)
            ? candidate
            : candidate with { LinkedModId = match.LinkedModId };
      }).ToArray();

  private static IReadOnlyList<ImportCandidate> SuggestNameMatches(
      IReadOnlyList<ImportCandidate> candidates,
      IReadOnlyList<ImportCandidate> existingCandidates) =>
      candidates.Select(candidate =>
      {
        if (!string.IsNullOrWhiteSpace(candidate.LinkedModId) || string.IsNullOrWhiteSpace(candidate.DisplayName))
        {
          return candidate;
        }

        var matches = existingCandidates.Where(existing =>
            !string.IsNullOrWhiteSpace(existing.LinkedModId) &&
            !string.IsNullOrWhiteSpace(existing.DisplayName) &&
            string.Equals(existing.DisplayName.Trim(), candidate.DisplayName.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return matches.Length == 1
            ? candidate with { SuggestedModId = matches[0].LinkedModId }
            : candidate;
      }).ToArray();

  private static IReadOnlyList<ImportValidationIssue> DetectSourceOwnerConflicts(
      IReadOnlyList<ImportCandidate> candidates,
      IReadOnlyList<ImportCandidate> existingCandidates) =>
      candidates
          .Where(candidate =>
              candidate.SourceReference is not null &&
              !string.IsNullOrWhiteSpace(candidate.LinkedModId))
          .Select(candidate =>
          {
            var existing = existingCandidates.FirstOrDefault(existingCandidate =>
                SourceReferencesMatch(existingCandidate.SourceReference, candidate.SourceReference) &&
                !string.IsNullOrWhiteSpace(existingCandidate.LinkedModId));

            return existing is not null &&
                   !string.Equals(existing.LinkedModId, candidate.LinkedModId, StringComparison.Ordinal)
                ? new ImportValidationIssue(
                    candidate.CandidateId,
                    "import.source.owner.conflict",
                    $"The source identity is already owned by Mod {existing.LinkedModId}.")
                : null;
          })
          .Where(issue => issue is not null)
          .Cast<ImportValidationIssue>()
          .ToArray();

  private static bool SourceReferencesMatch(
      ImportSourceReference? left,
      ImportSourceReference? right) =>
      left is not null &&
      right is not null &&
      left.SourceType == right.SourceType &&
      left.ExternalId == right.ExternalId;
}
