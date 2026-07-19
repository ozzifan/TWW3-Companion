using Tww3Companion.Desktop.Services;
using Xunit;

namespace Tww3Companion.Desktop.Tests.Services;

public sealed class WindowPlacementServiceTests
{
  private static readonly WorkArea Primary = new(0, 0, 1920, 1040, true);

  [Fact]
  public void RestoresValidFullyVisiblePlacement()
  {
    var saved = new WindowPlacement(100, 80, 1200, 700, true);

    Assert.Equal(saved, WindowPlacementService.Restore(saved, [Primary]));
  }

  [Fact]
  public void KeepsPartiallyVisiblePlacementWhenTheTitleBarAndPrimaryContentRemainReachable()
  {
    var saved = new WindowPlacement(-120, 10, 1280, 800, false);

    Assert.Equal(saved, WindowPlacementService.Restore(saved, [Primary]));
  }

  [Theory]
  [InlineData(2000, 50, 1200, 700)]
  [InlineData(100, 50, 1000, 700)]
  [InlineData(100, 50, 1200, 600)]
  public void InvalidOrOffScreenPlacementFallsBackToCenteredDefault(double x, double y, double width, double height)
  {
    var restored = WindowPlacementService.Restore(new WindowPlacement(x, y, width, height, false), [Primary]);

    Assert.Equal(new WindowPlacement(320, 120, 1280, 800, false), restored);
  }

  [Fact]
  public void MissingPlacementFallsBackToFullyVisibleDefaultOnPrimaryWorkArea()
  {
    var restored = WindowPlacementService.Restore(null, [Primary]);

    Assert.Equal(1280, restored.Width);
    Assert.Equal(800, restored.Height);
    Assert.True(Primary.CanKeepReachable(restored));
  }
}
