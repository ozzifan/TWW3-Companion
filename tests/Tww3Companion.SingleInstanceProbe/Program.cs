using System.Runtime.Versioning;
using Tww3Companion.Desktop.Startup;
using Tww3Companion.Infrastructure.Startup;

[assembly: SupportedOSPlatform("windows")]

return args[0] switch
{
  "hold" => Hold(args[1]),
  "contend" => SingleInstanceStartup.Run(
      new WindowsSingleInstanceLease(),
      new FileNotification(args[2]),
      () => File.WriteAllText(args[1], "startup action ran")),
  _ => 2,
};

static int Hold(string signalPath)
{
  var deadline = DateTime.UtcNow.AddSeconds(10);
  do
  {
    var result = SingleInstanceStartup.Run(
        new WindowsSingleInstanceLease(),
        new NullNotification(),
        () =>
        {
          File.WriteAllText(signalPath, "acquired");
          Thread.Sleep(TimeSpan.FromSeconds(30));
        });
    if (result == 0)
    {
      return 0;
    }

    Thread.Sleep(25);
  }
  while (DateTime.UtcNow < deadline);

  return 3;
}

internal sealed class FileNotification(string path) : IStartupNotification
{
  public void ShowBlockingError(string message) => File.WriteAllText(path, message);
}

internal sealed class NullNotification : IStartupNotification
{
  public void ShowBlockingError(string message)
  {
  }
}
