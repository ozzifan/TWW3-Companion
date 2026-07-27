namespace Tww3Companion.Desktop.Services;

public static class WorkspaceFileName
{
  public static string Sanitize(string displayName)
  {
    var invalid = Path.GetInvalidFileNameChars();
    var safe = new string(displayName.Trim().Select(character =>
        invalid.Contains(character) ? '-' : character).ToArray());
    return string.IsNullOrWhiteSpace(safe) ? "Workspace" : safe;
  }
}
