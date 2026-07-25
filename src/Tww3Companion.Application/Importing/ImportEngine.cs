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
    var existingCandidates = targetContext is ImportTargetContext.NewWorkspace
        ? []
        : await _store.ReadCandidatesAsync(targetContext, cancellationToken);

    return await BuildNormalizedPreviewAsync(
        targetContext,
        importCandidates,
        existingCandidates,
        cancellationToken);
  }

  public async Task<ImportPreview> ResolveAsync(
      ImportPreview preview,
      ImportCandidate resolvedCandidate,
      CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(preview);
    ArgumentNullException.ThrowIfNull(resolvedCandidate);

    var matchingIndices = preview.Candidates
        .Select((candidate, index) => (candidate, index))
        .Where(entry => string.Equals(entry.candidate.CandidateId, resolvedCandidate.CandidateId, StringComparison.Ordinal))
        .Select(entry => entry.index)
        .ToArray();

    if (matchingIndices.Length == 0)
    {
      throw new ArgumentException(
          $"The preview does not contain candidate '{resolvedCandidate.CandidateId}'.",
          nameof(resolvedCandidate));
    }

    if (matchingIndices.Length > 1)
    {
      throw new ArgumentException(
          $"The preview contains duplicate candidate '{resolvedCandidate.CandidateId}'.",
          nameof(resolvedCandidate));
    }

    var updatedCandidates = preview.Candidates.ToArray();
    updatedCandidates[matchingIndices[0]] = resolvedCandidate;

    var existingCandidates = preview.TargetContext is ImportTargetContext.NewWorkspace
        ? []
        : await _store.ReadCandidatesAsync(preview.TargetContext, cancellationToken);

    return await BuildNormalizedPreviewAsync(
        preview.TargetContext,
        updatedCandidates,
        existingCandidates,
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

    if (preview.TargetContext is ImportTargetContext.NewWorkspace)
    {
      return new NewWorkspaceImportSession(preview, _store).ApplyAsync(confirm, cancellationToken);
    }

    throw new ArgumentException("Unsupported import target context.", nameof(preview));
  }

  private async Task<ImportPreview> BuildNormalizedPreviewAsync(
      ImportTargetContext targetContext,
      IReadOnlyList<ImportCandidate> candidates,
      IReadOnlyList<ImportCandidate> existingCandidates,
      CancellationToken cancellationToken)
  {
    if (targetContext is ImportTargetContext.NewWorkspace newWorkspace)
    {
      NewWorkspaceImportSession.ValidateDestination(newWorkspace);
    }
    else if (targetContext is ImportTargetContext.CurrentWorkspace currentWorkspace)
    {
      CurrentWorkspaceImportSession.ValidateDestination(currentWorkspace);
    }

    var matchedCandidates = MatchExactSourceReferences(candidates, existingCandidates);
    var suggestedCandidates = SuggestNameMatches(matchedCandidates, existingCandidates);
    var validationIssues = DetectSourceOwnerConflicts(suggestedCandidates, existingCandidates);
    var resolutions = suggestedCandidates.Select(candidate => new ImportResolution(
        candidate.CandidateId,
        candidate.LinkedModId,
        candidate.DisplayName,
        CanSkip: string.IsNullOrWhiteSpace(candidate.LinkedModId))).ToArray();
    var operations = BuildOperations(suggestedCandidates, validationIssues, targetContext);
    var warningCount = CountWarnings(suggestedCandidates, validationIssues);

    var preview = await _store.SavePreviewAsync(
        targetContext,
        suggestedCandidates,
        resolutions,
        cancellationToken);

    return preview with
    {
      Resolutions = resolutions,
      ValidationIssues = validationIssues,
      Operations = operations,
      WarningCount = warningCount
    };
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

  private static IReadOnlyList<ImportPreviewOperation> BuildOperations(
      IReadOnlyList<ImportCandidate> candidates,
      IReadOnlyList<ImportValidationIssue> validationIssues,
      ImportTargetContext targetContext) =>
      candidates.Select(candidate =>
      {
        var candidateIssues = validationIssues
            .Where(issue => string.Equals(issue.CandidateId, candidate.CandidateId, StringComparison.Ordinal))
            .ToArray();

        return new ImportPreviewOperation(
            candidate.CandidateId,
            DetermineLibraryAction(candidate, candidateIssues),
            DetermineMembershipAction(candidate, candidateIssues, targetContext),
            candidateIssues);
      }).ToArray();

  private static ImportLibraryAction DetermineLibraryAction(
      ImportCandidate candidate,
      IReadOnlyList<ImportValidationIssue> candidateIssues)
  {
    if (candidate.IsSkipped)
    {
      return ImportLibraryAction.Skip;
    }

    if (candidateIssues.Any(issue => issue.Code == "import.source.owner.conflict"))
    {
      return ImportLibraryAction.Conflict;
    }

    if (!string.IsNullOrWhiteSpace(candidate.LinkedModId))
    {
      return ImportLibraryAction.Existing;
    }

    if (!string.IsNullOrWhiteSpace(candidate.SuggestedModId))
    {
      return ImportLibraryAction.SuggestedMatch;
    }

    if (!string.IsNullOrWhiteSpace(candidate.DisplayName))
    {
      return ImportLibraryAction.Create;
    }

    return ImportLibraryAction.Create;
  }

  private static ImportMembershipAction DetermineMembershipAction(
      ImportCandidate candidate,
      IReadOnlyList<ImportValidationIssue> candidateIssues,
      ImportTargetContext targetContext)
  {
    if (candidate.IsSkipped)
    {
      return ImportMembershipAction.Skip;
    }

    if (candidateIssues.Any(issue => issue.Code == "import.source.owner.conflict"))
    {
      return ImportMembershipAction.Blocked;
    }

    return targetContext switch
    {
      ImportTargetContext.NewWorkspace { MembershipDestination: ImportMembershipDestination.LibraryOnly } =>
          ImportMembershipAction.None,
      ImportTargetContext.CurrentWorkspace { MembershipDestination: ImportMembershipDestination.LibraryOnly } =>
          ImportMembershipAction.None,
      _ => ImportMembershipAction.Add
    };
  }

  private static int CountWarnings(
      IReadOnlyList<ImportCandidate> candidates,
      IReadOnlyList<ImportValidationIssue> validationIssues)
  {
    var nonSkippedCandidateIds = candidates
        .Where(candidate => !candidate.IsSkipped)
        .Select(candidate => candidate.CandidateId)
        .ToHashSet(StringComparer.Ordinal);

    return validationIssues.Count(issue =>
        nonSkippedCandidateIds.Contains(issue.CandidateId) &&
        issue.Code.Contains("warning", StringComparison.OrdinalIgnoreCase));
  }

  private static bool SourceReferencesMatch(
      ImportSourceReference? left,
      ImportSourceReference? right) =>
      left is not null &&
      right is not null &&
      left.SourceType == right.SourceType &&
      left.ExternalId == right.ExternalId;
}
