namespace Tww3Companion.Application.Importing;

internal sealed class NewWorkspaceImportSession(
    ImportPreview preview,
    IWorkspaceImportStore store)
{
  private readonly ImportTargetContext.NewWorkspace _targetContext = preview.TargetContext as ImportTargetContext.NewWorkspace
      ?? throw new ArgumentException("The preview must target a new Workspace.", nameof(preview));

  public static void ValidateDestination(ImportTargetContext.NewWorkspace targetContext)
  {
    if (string.IsNullOrWhiteSpace(targetContext.DisplayName))
    {
      throw new ArgumentException("A new Workspace import requires a display name.", nameof(targetContext));
    }

    if (string.IsNullOrWhiteSpace(targetContext.DestinationPath))
    {
      throw new ArgumentException("A new Workspace import requires a destination path.", nameof(targetContext));
    }
  }

  public async Task<ImportOutcome> ApplyAsync(bool confirm, CancellationToken cancellationToken = default)
  {
    if (!confirm) return new ImportOutcome(preview.TargetContext, preview.Candidates, Applied: false);

    ValidateDestination(_targetContext);
    ImportPreviewValidation.Validate(preview);

    var newWorkspace = await store.CreateNewWorkspaceAsync(_targetContext, cancellationToken);
    var newWorkspacePreview = preview with { TargetContext = newWorkspace };

    try
    {
      return await store.CommitAtomicallyAsync(newWorkspacePreview, confirm: true, cancellationToken);
    }
    catch
    {
      await store.RollbackNewWorkspaceAsync(newWorkspace, cancellationToken);
      throw;
    }
  }
}
