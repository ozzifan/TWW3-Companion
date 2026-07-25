using Tww3Companion.Application.Importing;
using Tww3Companion.Desktop.Services;

namespace Tww3Companion.Desktop.ViewModels;

internal static class ImportPreviewResolutionRetention
{
  public static async Task<ImportPreview> MergeAsync(
      ImportPreview freshPreview,
      ImportPreview priorPreview,
      IReadOnlyList<object> priorSourceCandidates,
      IReadOnlyList<object> freshSourceCandidates,
      IImportTaskCoordinator coordinator,
      CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(freshPreview);
    ArgumentNullException.ThrowIfNull(priorPreview);
    ArgumentNullException.ThrowIfNull(priorSourceCandidates);
    ArgumentNullException.ThrowIfNull(freshSourceCandidates);
    ArgumentNullException.ThrowIfNull(coordinator);

    var preview = freshPreview;
    var freshIssues = preview.ValidationIssues ?? [];

    foreach (var priorCandidate in priorPreview.Candidates)
    {
      var freshCandidate = preview.Candidates.FirstOrDefault(candidate =>
          CandidateIdentityMatches(candidate, priorCandidate));
      if (freshCandidate is null)
      {
        continue;
      }

      if (!SourceCandidateIdentityUnchanged(
              priorCandidate,
              priorSourceCandidates,
              freshSourceCandidates))
      {
        continue;
      }

      var freshOperation = GetOperation(preview, freshCandidate.CandidateId);
      if (freshOperation is null ||
          !IsMoreResolved(priorCandidate, freshCandidate, freshOperation, freshIssues) ||
          !AvailableChoicesUnchanged(priorCandidate, freshOperation))
      {
        continue;
      }

      var retained = priorCandidate with { CandidateId = freshCandidate.CandidateId };
      preview = await coordinator.ResolveAsync(preview, retained, cancellationToken);
      freshIssues = preview.ValidationIssues ?? [];
    }

    return preview;
  }

  private static bool IsMoreResolved(
      ImportCandidate prior,
      ImportCandidate fresh,
      ImportPreviewOperation freshOperation,
      IReadOnlyList<ImportValidationIssue> freshIssues)
  {
    if (prior.IsSkipped && !fresh.IsSkipped)
    {
      return true;
    }

    if (!string.IsNullOrWhiteSpace(prior.LinkedModId) &&
        string.IsNullOrWhiteSpace(fresh.LinkedModId) &&
        ImportPreviewRules.IsBlocking(fresh, freshOperation, freshIssues))
    {
      return true;
    }

    if (!string.IsNullOrWhiteSpace(prior.DisplayName) &&
        string.IsNullOrWhiteSpace(fresh.LinkedModId) &&
        ImportPreviewRules.IsBlocking(fresh, freshOperation, freshIssues) &&
        (string.IsNullOrWhiteSpace(fresh.DisplayName) ||
         !string.Equals(prior.DisplayName, fresh.DisplayName, StringComparison.Ordinal)))
    {
      return true;
    }

    return false;
  }

  private static bool AvailableChoicesUnchanged(
      ImportCandidate retainedResolution,
      ImportPreviewOperation freshOperation)
  {
    if (retainedResolution.IsSkipped)
    {
      return true;
    }

    if (!string.IsNullOrWhiteSpace(retainedResolution.LinkedModId))
    {
      if (freshOperation.Issues.Any(issue =>
              string.Equals(issue.Code, "import.source.owner.conflict", StringComparison.Ordinal)))
      {
        return string.Equals(
            ExtractOwnerModId(freshOperation.Issues),
            retainedResolution.LinkedModId,
            StringComparison.Ordinal);
      }

      if (freshOperation.LibraryAction == ImportLibraryAction.SuggestedMatch)
      {
        return string.Equals(
            retainedResolution.LinkedModId,
            retainedResolution.SuggestedModId,
            StringComparison.Ordinal);
      }

      return false;
    }

    if (!string.IsNullOrWhiteSpace(retainedResolution.DisplayName))
    {
      if (freshOperation.Issues.Any(issue =>
              string.Equals(issue.Code, "import.scalar.conflict", StringComparison.Ordinal)))
      {
        return ParseCompetingValues(freshOperation.Issues)
            .Contains(retainedResolution.DisplayName, StringComparer.Ordinal);
      }

      return freshOperation.LibraryAction is ImportLibraryAction.Create or ImportLibraryAction.Conflict;
    }

    return false;
  }

  private static bool CandidateIdentityMatches(ImportCandidate left, ImportCandidate right) =>
      string.Equals(left.CandidateId, right.CandidateId, StringComparison.Ordinal) &&
      SourceReferenceEquals(left.SourceReference, right.SourceReference);

  private static bool SourceCandidateIdentityUnchanged(
      ImportCandidate previewCandidate,
      IReadOnlyList<object> priorSourceCandidates,
      IReadOnlyList<object> freshSourceCandidates)
  {
    if (previewCandidate.SourceReference is null)
    {
      return false;
    }

    var key = SourceReferenceKey(previewCandidate.SourceReference);
    return priorSourceCandidates.Any(candidate => SourceCandidateKey(candidate) == key) &&
        freshSourceCandidates.Any(candidate => SourceCandidateKey(candidate) == key);
  }

  private static bool SourceReferenceEquals(
      ImportSourceReference? left,
      ImportSourceReference? right)
  {
    if (left is null || right is null)
    {
      return left is null && right is null;
    }

    return left.SourceType == right.SourceType &&
        string.Equals(left.ExternalId, right.ExternalId, StringComparison.Ordinal);
  }

  private static string SourceReferenceKey(ImportSourceReference sourceReference) =>
      $"{sourceReference.SourceType}:{sourceReference.ExternalId}";

  private static string SourceCandidateKey(object candidate) =>
      candidate switch
      {
        SteamImportCandidate steamCandidate when TryGetWorkshopItemId(
            steamCandidate.SourceReference,
            out var workshopItemId) =>
            $"{ImportSourceType.SteamWorkshop}:{workshopItemId}",
        ImportCandidate importCandidate when importCandidate.SourceReference is not null =>
            SourceReferenceKey(importCandidate.SourceReference),
        MarkdownImportCandidate
        {
          SourceReference: { } sourceReference
        } markdownCandidate =>
            $"{ImportSourceType.SteamWorkshop}:{sourceReference.WorkshopId}:markdown:{markdownCandidate.SourceLine}",
        _ => candidate.GetType().FullName ?? candidate.ToString() ?? string.Empty
      };

  private static bool TryGetWorkshopItemId(string sourceReference, out string workshopItemId)
  {
    if (sourceReference.Length > 0 && sourceReference.All(char.IsAsciiDigit))
    {
      workshopItemId = sourceReference;
      return true;
    }

    if (Uri.TryCreate(sourceReference, UriKind.Absolute, out var uri) &&
        (uri.Host.Equals("steamcommunity.com", StringComparison.OrdinalIgnoreCase) ||
         uri.Host.Equals("www.steamcommunity.com", StringComparison.OrdinalIgnoreCase)) &&
        (uri.AbsolutePath.Equals("/sharedfiles/filedetails/", StringComparison.OrdinalIgnoreCase) ||
         uri.AbsolutePath.Equals("/sharedfiles/filedetails", StringComparison.OrdinalIgnoreCase)))
    {
      var id = uri.Query.TrimStart('?').Split('&')
          .Select(parameter => parameter.Split('=', 2))
          .FirstOrDefault(parameter => parameter.Length == 2 && parameter[0].Equals("id", StringComparison.OrdinalIgnoreCase))?[1];
      if (!string.IsNullOrWhiteSpace(id) && id.All(char.IsAsciiDigit))
      {
        workshopItemId = id;
        return true;
      }
    }

    workshopItemId = string.Empty;
    return false;
  }

  private static ImportPreviewOperation? GetOperation(ImportPreview preview, string candidateId) =>
      (preview.Operations ?? []).FirstOrDefault(operation =>
          string.Equals(operation.CandidateId, candidateId, StringComparison.Ordinal));

  private static string? ExtractOwnerModId(IReadOnlyList<ImportValidationIssue> issues)
  {
    var issue = issues.FirstOrDefault(candidateIssue =>
        string.Equals(candidateIssue.Code, "import.source.owner.conflict", StringComparison.Ordinal));
    if (issue is null)
    {
      return null;
    }

    const string marker = "Mod ";
    var index = issue.Message.LastIndexOf(marker, StringComparison.Ordinal);
    if (index < 0)
    {
      return null;
    }

    var ownerModId = issue.Message[(index + marker.Length)..].Trim().TrimEnd('.');
    return string.IsNullOrWhiteSpace(ownerModId) ? null : ownerModId;
  }

  private static IReadOnlyList<string> ParseCompetingValues(IReadOnlyList<ImportValidationIssue> issues)
  {
    var issue = issues.FirstOrDefault(candidateIssue =>
        string.Equals(candidateIssue.Code, "import.scalar.conflict", StringComparison.Ordinal));
    return issue?.CompetingValues is { Count: > 0 } values ? values : [];
  }
}
