using Tww3Companion.Desktop.ViewModels;

namespace Tww3Companion.Desktop.Services;

public interface IImportSourceFileService
{
  Task<ImportSourceDocument?> ChooseTextFileAsync(
      CancellationToken cancellationToken = default);
}
