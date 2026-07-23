namespace Tww3Companion.Application.Importing;

public interface IImportEngine
{
  // Candidates remain source-neutral; ImportCandidate is one shared pipeline shape.
  Task<ImportPreview> BuildPreviewAsync(
      ImportTargetContext targetContext,
      IReadOnlyList<object> candidates,
      CancellationToken cancellationToken = default);

  Task<ImportOutcome> ApplyAsync(
      ImportPreview preview,
      bool confirm,
      CancellationToken cancellationToken = default);
}
