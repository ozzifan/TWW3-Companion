namespace Tww3Companion.Application.Settings;

internal static class RecentWorkspaceUpdater
{
    public static ApplicationSettings Add(ApplicationSettings settings, string path, DateTimeOffset openedUtc)
    {
        var recents = new[] { new RecentWorkspace(path, openedUtc) }
            .Concat(settings.RecentWorkspaces.Where(recent =>
                !StringComparer.OrdinalIgnoreCase.Equals(recent.Path, path)))
            .Take(10)
            .ToArray();

        return settings with { RecentWorkspaces = recents };
    }
}
