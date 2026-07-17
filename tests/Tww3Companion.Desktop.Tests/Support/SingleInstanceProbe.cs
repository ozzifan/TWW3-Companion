using System.Diagnostics;
using Xunit;

namespace Tww3Companion.Desktop.Tests.Support;

internal sealed class SingleInstanceProbe : IDisposable
{
  private readonly Process process;

  private SingleInstanceProbe(Process process) => this.process = process;

  public static SingleInstanceProbe StartContender(string mutexName, string sentinelPath)
  {
    var script = "$mutex = [Threading.Mutex]::new($false,$env:TWW3C_MUTEX); " +
        "$owned = $mutex.WaitOne(0); if (-not $owned) { exit 23 }; " +
        "[IO.File]::WriteAllText($env:TWW3C_SENTINEL,'settings touched'); " +
        "$mutex.ReleaseMutex(); $mutex.Dispose()";
    var startInfo = new ProcessStartInfo("powershell.exe")
    {
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true,
    };
    startInfo.ArgumentList.Add("-NoProfile");
    startInfo.ArgumentList.Add("-Command");
    startInfo.ArgumentList.Add(script);
    startInfo.Environment["TWW3C_MUTEX"] = mutexName;
    startInfo.Environment["TWW3C_SENTINEL"] = sentinelPath;
    return new SingleInstanceProbe(Process.Start(startInfo)!);
  }

  public int WaitForExit()
  {
    Assert.True(process.WaitForExit(TimeSpan.FromSeconds(10)), "Single-instance probe timed out.");
    return process.ExitCode;
  }

  public void Dispose()
  {
    if (!process.HasExited)
    {
      process.Kill(entireProcessTree: true);
      process.WaitForExit();
    }

    process.Dispose();
  }
}
