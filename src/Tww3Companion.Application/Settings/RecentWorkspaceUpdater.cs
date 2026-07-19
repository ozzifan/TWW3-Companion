using Tww3Companion.Application.Common;

namespace Tww3Companion.Application.Settings;

internal static class RecentWorkspaceUpdater
{
    public static async Task<OperationError?> AddAsync(
        IApplicationSettingsStore settingsStore,
        string path,
        string displayName,
        DateTimeOffset openedUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            var settings = await settingsStore.LoadAsync(cancellationToken);
            var saveResult = await settingsStore.SaveAsync(
                Add(settings, path, displayName, openedUtc),
                cancellationToken);
            return saveResult is OperationResult<ApplicationSettings>.Failure failure
                ? PostCommit(failure.Error.Code, failure.Error.Message)
                : null;
        }
        catch (OperationCanceledException)
        {
            return PostCommit("settings.update.cancelled", "Updating recent workspaces was cancelled.");
        }
    }

    public static ApplicationSettings Add(
        ApplicationSettings settings,
        string path,
        string displayName,
        DateTimeOffset openedUtc)
    {
        var recents = new[] { new RecentWorkspace(path, openedUtc, displayName) }
            .Concat(settings.RecentWorkspaces.Where(recent =>
                !StringComparer.OrdinalIgnoreCase.Equals(recent.Path, path)))
            .Take(10)
            .ToArray();

        return settings with { RecentWorkspaces = recents };
    }

    private static OperationError PostCommit(string code, string message) =>
        new(code, message, true, "Retry saving application settings.");
}
