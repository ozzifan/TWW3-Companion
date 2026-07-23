namespace Tww3Companion.Application.Importing;

public interface IImportEngine
{
  Task<ImportPreview> BuildPreviewAsync(
      ImportTargetContext targetContext,
      IReadOnlyList<object> candidates,
      CancellationToken cancellationToken = default);

  Task<ImportOutcome> ApplyAsync(
      ImportPreview preview,
      bool confirm,
      CancellationToken cancellationToken = default);
}
