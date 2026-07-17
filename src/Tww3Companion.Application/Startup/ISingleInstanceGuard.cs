namespace Tww3Companion.Application.Startup;

public interface ISingleInstanceGuard
{
  ISingleInstanceLease? TryAcquire();
}
