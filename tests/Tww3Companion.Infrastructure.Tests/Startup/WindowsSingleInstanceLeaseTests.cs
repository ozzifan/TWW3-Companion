using System.Runtime.Versioning;
using Tww3Companion.Application.Startup;
using Tww3Companion.Infrastructure.Startup;
using Xunit;

namespace Tww3Companion.Infrastructure.Tests.Startup;

public sealed class WindowsSingleInstanceLeaseTests
{
  [Fact]
  [SupportedOSPlatform("windows")]
  public void TryAcquire_AllowsOnlyOneLeaseUntilOwnerDisposes()
  {
    if (!OperatingSystem.IsWindows())
    {
      Assert.Skip("Windows named mutex behavior requires Windows.");
      return;
    }

    var guard = new WindowsSingleInstanceLease();
    ISingleInstanceLease? first = null;

    Assert.True(
        SpinWait.SpinUntil(() => (first = guard.TryAcquire()) is not null, TimeSpan.FromSeconds(10)),
        "The global test lease remained occupied.");
    using (first)
    {
      var second = guard.TryAcquire();

      Assert.NotNull(first);
      Assert.Null(second);
      second?.Dispose();
    }

    using var reacquired = guard.TryAcquire();
    Assert.NotNull(reacquired);
  }
}
