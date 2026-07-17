using Tww3Companion.Application.Common;

namespace Tww3Companion.Application.Settings;

public interface IApplicationSettingsStore
{
    Task<ApplicationSettings> LoadAsync(CancellationToken cancellationToken);

    Task<OperationResult<ApplicationSettings>> SaveAsync(
        ApplicationSettings settings,
        CancellationToken cancellationToken);
}
