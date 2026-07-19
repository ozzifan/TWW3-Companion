namespace Tww3Companion.Infrastructure.Paths;

public sealed record ManagedPaths(
    ApplicationMode Mode,
    string RootDirectory,
    string SettingsFile,
    string BackupsDirectory,
    string LogsDirectory,
    string WorkspacesDirectory)
{
  public IReadOnlyList<string> RequiredDirectories =>
      [RootDirectory, BackupsDirectory, LogsDirectory, WorkspacesDirectory];

  public static ManagedPaths Detect(string executableDirectory, string? localAppData)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(executableDirectory);

    if (File.Exists(Path.Combine(executableDirectory, "portable.flag")))
    {
      return ForRoot(ApplicationMode.Portable, Path.Combine(executableDirectory, "Data"));
    }

    ArgumentException.ThrowIfNullOrWhiteSpace(localAppData);
    return ForRoot(ApplicationMode.Installed, Path.Combine(localAppData, "TWW3 Companion"));
  }

  public static ManagedPaths ForRoot(ApplicationMode mode, string rootDirectory)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
    return new ManagedPaths(
        mode,
        rootDirectory,
        Path.Combine(rootDirectory, "settings.json"),
        Path.Combine(rootDirectory, "Backups"),
        Path.Combine(rootDirectory, "Logs"),
        Path.Combine(rootDirectory, "Workspaces"));
  }
}
