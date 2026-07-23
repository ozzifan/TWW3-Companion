namespace Tww3Companion.Application.Importing;

public sealed class ImportEngine(IWorkspaceImportStore store) : IImportEngine
{
  private readonly IWorkspaceImportStore _store = store ?? throw new ArgumentNullException(nameof(store));

  public async Task<ImportPreview> BuildPreviewAsync(
      ImportTargetContext targetContext,
      IReadOnlyList<object> candidates,
      CancellationToken cancellationToken = default)
  {
    var currentWorkspace = targetContext as ImportTargetContext.CurrentWorkspace
        ?? throw new NotSupportedException("Only current-workspace imports are supported.");
    var importCandidates = candidates.Cast<ImportCandidate>().ToArray();
    var session = new CurrentWorkspaceImportSession(currentWorkspace, importCandidates, _store);
    var preview = session.BuildPreview();

    await _store.ReadCandidatesAsync(preview.TargetContext, cancellationToken);
    return await _store.SavePreviewAsync(
        preview.TargetContext,
        importCandidates,
        importCandidates.Select(candidate => new ImportResolution(
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
    var currentWorkspace = preview.TargetContext as ImportTargetContext.CurrentWorkspace
        ?? throw new NotSupportedException("Only current-workspace imports are supported.");
    var session = new CurrentWorkspaceImportSession(currentWorkspace, preview.Candidates.Cast<ImportCandidate>().ToArray(), _store);

    return session.ApplyAsync(confirm, cancellationToken);
  }
}
