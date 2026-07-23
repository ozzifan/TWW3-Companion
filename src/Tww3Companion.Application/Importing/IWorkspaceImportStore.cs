namespace Tww3Companion.Application.Importing;

public interface IWorkspaceImportStore
{
  Task<IReadOnlyList<ImportCandidate>> ReadCandidatesAsync(
      ImportTargetContext targetContext,
      CancellationToken cancellationToken = default);

  Task<ImportPreview> SavePreviewAsync(
      ImportTargetContext targetContext,
      IReadOnlyList<ImportCandidate> candidates,
      IReadOnlyList<ImportResolution> resolutions,
      CancellationToken cancellationToken = default);

  Task<ImportOutcome> CommitAtomicallyAsync(
      ImportPreview preview,
      bool confirm,
      CancellationToken cancellationToken = default);
}
