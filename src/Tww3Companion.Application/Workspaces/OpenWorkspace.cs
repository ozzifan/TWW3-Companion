using Tww3Companion.Application.Abstractions;
using Tww3Companion.Application.Common;
using Tww3Companion.Application.Settings;
using Tww3Companion.Domain.Workspaces;

namespace Tww3Companion.Application.Workspaces;

public sealed class OpenWorkspace(
    IWorkspaceStore workspaceStore,
    IApplicationSettingsStore settingsStore,
    IClock clock)
{
    public async Task<OperationResult<Workspace>> ExecuteAsync(string path, CancellationToken cancellationToken)
    {
        var result = await workspaceStore.OpenAsync(path, cancellationToken);
        if (result is not OperationResult<Workspace>.Success)
        {
            return result;
        }

        var settings = await settingsStore.LoadAsync(cancellationToken);
        await settingsStore.SaveAsync(
            RecentWorkspaceUpdater.Add(settings, path, clock.UtcNow),
            cancellationToken);
        return result;
    }
}
