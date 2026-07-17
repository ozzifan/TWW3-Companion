using System.Security.Principal;
using System.Runtime.Versioning;
using Tww3Companion.Application.Startup;

namespace Tww3Companion.Infrastructure.Startup;

[SupportedOSPlatform("windows")]
public sealed class WindowsSingleInstanceLease : ISingleInstanceGuard
{
  private static readonly object SyncRoot = new();
  private static readonly HashSet<string> OwnedMutexes = [];

  public ISingleInstanceLease? TryAcquire()
  {
    var sid = WindowsIdentity.GetCurrent().User!.Value;
    var mutexName = $@"Local\TWW3Companion.SingleInstance.{sid}";

    lock (SyncRoot)
    {
      if (!OwnedMutexes.Add(mutexName))
      {
        return null;
      }
    }

    var mutex = new Mutex(false, mutexName);
    var ownsMutex = false;
    try
    {
      try
      {
        ownsMutex = mutex.WaitOne(0);
      }
      catch (AbandonedMutexException)
      {
        ownsMutex = true;
      }

      return ownsMutex ? new Lease(mutexName, mutex) : null;
    }
    finally
    {
      if (!ownsMutex)
      {
        mutex.Dispose();
        lock (SyncRoot)
        {
          OwnedMutexes.Remove(mutexName);
        }
      }
    }
  }

  private sealed class Lease(string mutexName, Mutex mutex) : ISingleInstanceLease
  {
    private bool ownsMutex = true;

    public void Dispose()
    {
      if (!ownsMutex)
      {
        return;
      }

      ownsMutex = false;
      mutex.ReleaseMutex();
      mutex.Dispose();
      lock (SyncRoot)
      {
        OwnedMutexes.Remove(mutexName);
      }
    }
  }
}
