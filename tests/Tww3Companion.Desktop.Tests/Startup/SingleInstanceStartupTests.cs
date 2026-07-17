using System.Security.Principal;
using Tww3Companion.Desktop.Tests.Support;
using Tww3Companion.Infrastructure.Startup;
using Xunit;

namespace Tww3Companion.Desktop.Tests.Startup;

public sealed class SingleInstanceStartupTests
{
  [Fact]
  public void LeaseRejectionUsesApprovedLaunchAbortMessage()
  {
    Assert.Equal(
        "TWW3 Companion is already running for this Windows user. Close the existing installed or portable copy and try again.",
        Program.AlreadyRunningMessage);
  }

  [Fact]
  public void InstalledAndPortableLaunchesShareLeaseBeforeSettingsAccess()
  {
    if (!OperatingSystem.IsWindows())
    {
      Assert.Skip("Windows named mutex process behavior requires Windows.");
      return;
    }

    var mutexName = $@"Local\TWW3Companion.SingleInstance.{WindowsIdentity.GetCurrent().User!.Value}";
    var sentinel = Path.Combine(Path.GetTempPath(), $"tww3c-settings-{Guid.NewGuid():N}.json");
    using var installedLease = new WindowsSingleInstanceLease().TryAcquire();
    using var portableProbe = SingleInstanceProbe.StartContender(mutexName, sentinel);

    var exitCode = portableProbe.WaitForExit();

    Assert.NotNull(installedLease);
    Assert.Equal(23, exitCode);
    Assert.False(File.Exists(sentinel));
  }
}
