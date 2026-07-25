using Tww3Companion.Application.Importing;
using Tww3Companion.Desktop.ViewModels;

namespace Tww3Companion.Desktop.Services;

public interface IImportTaskCoordinator
{
  Task<ImportSourceLoadResult> LoadSourceAsync(
      ImportSourceRequest request,
      CancellationToken cancellationToken = default);

  Task<ImportPreview> BuildPreviewAsync(
      ImportTargetContext targetContext,
      IReadOnlyList<object> candidates,
      CancellationToken cancellationToken = default);

  Task<ImportPreview> ResolveAsync(
      ImportPreview preview,
      ImportCandidate resolvedCandidate,
      CancellationToken cancellationToken = default);

  Task<ImportOutcome> ApplyAsync(
      ImportPreview preview,
      CancellationToken cancellationToken = default);
}
