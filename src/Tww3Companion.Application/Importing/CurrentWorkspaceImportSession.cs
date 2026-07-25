namespace Tww3Companion.Application.Importing;

internal sealed class CurrentWorkspaceImportSession(
    ImportPreview preview,
    IWorkspaceImportStore store)
{
  private readonly ImportTargetContext.CurrentWorkspace _targetContext = preview.TargetContext as ImportTargetContext.CurrentWorkspace
      ?? throw new ArgumentException("The preview must target the current Workspace.", nameof(preview));

  public static void ValidateDestination(ImportTargetContext.CurrentWorkspace targetContext)
  {
    if (string.IsNullOrWhiteSpace(targetContext.WorkspaceId))
    {
      throw new ArgumentException("A current Workspace import requires a Workspace UUID.", nameof(targetContext));
    }

    if (string.IsNullOrWhiteSpace(targetContext.WorkspacePath))
    {
      throw new ArgumentException("A current Workspace import requires a Workspace path.", nameof(targetContext));
    }

    switch (targetContext.MembershipDestination)
    {
      case ImportMembershipDestination.LibraryOnly:
        break;

      case ImportMembershipDestination.ExistingCollection { CollectionId: var collectionId }
          when !string.IsNullOrWhiteSpace(collectionId):
        break;

      case ImportMembershipDestination.ExistingCollection:
        throw new ArgumentException(
            "A current Workspace import requires a Collection UUID when targeting an existing Collection.",
            nameof(targetContext));

      case ImportMembershipDestination.NewCollection { DisplayName: var displayName }
          when !string.IsNullOrWhiteSpace(displayName):
        break;

      case ImportMembershipDestination.NewCollection:
        throw new ArgumentException(
            "A current Workspace import requires a Collection display name when targeting a new Collection.",
            nameof(targetContext));

      default:
        throw new ArgumentException("Unsupported membership destination.", nameof(targetContext));
    }
  }

  public async Task<ImportOutcome> ApplyAsync(bool confirm, CancellationToken cancellationToken = default)
  {
    if (!confirm)
    {
      return new ImportOutcome(
          preview.TargetContext,
          preview.Candidates.Cast<object>().ToArray(),
          Applied: false);
    }

    ImportPreviewValidation.Validate(preview);

    foreach (var linkedModId in preview.Candidates
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

    if (preview.Candidates.Any(candidate =>
        (candidate.IsSkipped && preview.Resolutions?.Any(resolution =>
            resolution.CandidateId == candidate.CandidateId && resolution.CanSkip) != true) ||
        (!candidate.IsSkipped && string.IsNullOrWhiteSpace(candidate.LinkedModId) &&
            string.IsNullOrWhiteSpace(candidate.DisplayName))))
    {
      throw new InvalidOperationException("All required import candidates must be resolved before applying.");
    }
  }
}
