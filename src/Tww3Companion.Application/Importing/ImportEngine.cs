namespace Tww3Companion.Application.Importing;

public sealed class ImportEngine : IImportEngine
{
  public Task<ImportPreview> BuildPreviewAsync(
      ImportTargetContext targetContext,
      IReadOnlyList<object> candidates,
      CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    return targetContext switch
    {
      ImportTargetContext.CurrentWorkspace currentWorkspace =>
          Task.FromResult(new CurrentWorkspaceImportSession(currentWorkspace, candidates).BuildPreview()),
      _ => throw new NotSupportedException("The import target context is not supported."),
    };
  }

  public Task<ImportOutcome> ApplyAsync(
      ImportPreview preview,
      bool confirm,
      CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    return preview.TargetContext switch
    {
      ImportTargetContext.CurrentWorkspace currentWorkspace =>
          Task.FromResult(new CurrentWorkspaceImportSession(currentWorkspace, preview.Candidates).Apply(confirm)),
      _ => throw new NotSupportedException("The import target context is not supported."),
    };
  }
}
