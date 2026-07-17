using Tww3Companion.Infrastructure.Startup;
using Xunit;

namespace Tww3Companion.Infrastructure.Tests.Startup;

public sealed class WindowsSingleInstanceLeaseTests
{
  [Fact]
  public void TryAcquire_AllowsOnlyOneLeaseUntilOwnerDisposes()
  {
    if (!OperatingSystem.IsWindows())
    {
      Assert.Skip("Windows named mutex behavior requires Windows.");
      return;
    }

    var guard = new WindowsSingleInstanceLease();

    using var first = guard.TryAcquire();
    var second = guard.TryAcquire();

    Assert.NotNull(first);
    Assert.Null(second);
    second?.Dispose();

    first.Dispose();
    using var reacquired = guard.TryAcquire();
    Assert.NotNull(reacquired);
  }
}
