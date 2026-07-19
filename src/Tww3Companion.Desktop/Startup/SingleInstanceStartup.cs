using Tww3Companion.Application.Startup;

namespace Tww3Companion.Desktop.Startup;

public static class SingleInstanceStartup
{
  public const string AlreadyRunningMessage =
      "TWW3 Companion is already running for this Windows user. Close the existing installed or portable copy and try again.";

  public static int Run(
      ISingleInstanceGuard guard,
      IStartupNotification notification,
      Action startApplication)
  {
    using var lease = guard.TryAcquire();
    if (lease is null)
    {
      notification.ShowBlockingError(AlreadyRunningMessage);
      return 1;
    }

    startApplication();
    return 0;
  }
}
