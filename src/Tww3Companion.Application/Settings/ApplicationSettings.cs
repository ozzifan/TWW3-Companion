namespace Tww3Companion.Application.Settings;

public sealed record RecentWorkspace(string Path, DateTimeOffset LastOpenedUtc);

public sealed record ApplicationSettings(
    int SchemaVersion,
    string Theme,
    WindowPlacement? WindowPlacement,
    IReadOnlyList<RecentWorkspace> RecentWorkspaces);

public sealed record WindowPlacement(
    double X,
    double Y,
    double Width,
    double Height,
    bool IsMaximized);
