namespace Tww3Companion.Desktop.Services;

public sealed record WindowPlacement(double X, double Y, double Width, double Height, bool IsMaximized);

public sealed record WorkArea(double X, double Y, double Width, double Height, bool IsPrimary)
{
  public bool CanKeepReachable(WindowPlacement placement)
  {
    var left = Math.Max(X, placement.X);
    var top = Math.Max(Y, placement.Y);
    var right = Math.Min(X + Width, placement.X + placement.Width);
    var bottom = Math.Min(Y + Height, placement.Y + placement.Height);

    if (right <= left || bottom <= top)
    {
      return false;
    }

    return placement.Y + 48 > Y
        && placement.Y < Y + Height
        && placement.X + 320 > X
        && placement.X < X + Width;
  }
}

public static class WindowPlacementService
{
  public static WindowPlacement Restore(WindowPlacement? saved, IReadOnlyList<WorkArea> workAreas)
  {
    ArgumentOutOfRangeException.ThrowIfZero(workAreas.Count);

    if (saved is { Width: >= 1024, Height: >= 640 }
        && workAreas.Any(workArea => workArea.CanKeepReachable(saved)))
    {
      return saved;
    }

    var primary = workAreas.FirstOrDefault(workArea => workArea.IsPrimary) ?? workAreas[0];
    const double width = 1280;
    const double height = 800;
    return new WindowPlacement(
        primary.X + Math.Max(0, (primary.Width - width) / 2),
        primary.Y + Math.Max(0, (primary.Height - height) / 2),
        width,
        height,
        false);
  }
}
