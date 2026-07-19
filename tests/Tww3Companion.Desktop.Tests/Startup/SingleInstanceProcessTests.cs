using System.Diagnostics;
using Tww3Companion.Desktop.Startup;
using Xunit;

namespace Tww3Companion.Desktop.Tests.Startup;

public sealed class SingleInstanceProcessTests
{
  [Fact]
  public void SecondProductionStartupRejectsBeforeStartupAction()
  {
    if (!OperatingSystem.IsWindows())
    {
      Assert.Skip("Windows named mutex process behavior requires Windows.");
      return;
    }

    var directory = Path.Combine(Path.GetTempPath(), $"tww3c-process-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    var acquiredSignal = Path.Combine(directory, "lease-acquired.signal");
    var startupSentinel = Path.Combine(directory, "settings-or-avalonia-started.sentinel");
    var notification = Path.Combine(directory, "already-running.txt");

    using var holder = StartProbe("hold", acquiredSignal);
    try
    {
      WaitForFile(acquiredSignal);
      using var contender = StartProbe("contend", startupSentinel, notification);
      Assert.True(contender.WaitForExit(10_000));

      Assert.Equal(1, contender.ExitCode);
      Assert.Equal(SingleInstanceStartup.AlreadyRunningMessage, File.ReadAllText(notification));
      Assert.False(File.Exists(startupSentinel));
    }
    finally
    {
      if (!holder.HasExited)
      {
        holder.Kill(entireProcessTree: true);
        holder.WaitForExit();
      }

      Directory.Delete(directory, recursive: true);
    }
  }

  private static Process StartProbe(params string[] arguments)
  {
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var probe = Directory
        .GetFiles(
            Path.Combine(root, "tests", "Tww3Companion.SingleInstanceProbe", "bin"),
            "Tww3Companion.SingleInstanceProbe.dll",
            SearchOption.AllDirectories)
        .MaxBy(File.GetLastWriteTimeUtc)!;
    var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
    var startInfo = new ProcessStartInfo(dotnet)
    {
      UseShellExecute = false,
      CreateNoWindow = true,
    };
    startInfo.ArgumentList.Add(probe);
    foreach (var argument in arguments)
    {
      startInfo.ArgumentList.Add(argument);
    }

    return Process.Start(startInfo)!;
  }

  private static void WaitForFile(string path)
  {
    Assert.True(SpinWait.SpinUntil(() => File.Exists(path), TimeSpan.FromSeconds(10)), "Lease holder did not start.");
  }
}
