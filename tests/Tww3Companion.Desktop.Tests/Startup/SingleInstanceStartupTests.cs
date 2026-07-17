using Tww3Companion.Application.Startup;
using Tww3Companion.Desktop.Startup;
using Xunit;

namespace Tww3Companion.Desktop.Tests.Startup;

public sealed class SingleInstanceStartupTests
{
    [Fact]
    public void Run_WhenLeaseIsRejected_NotifiesAndDoesNotStartApplication()
    {
        var notification = new RecordingStartupNotification();
        var applicationStarted = false;

        var exitCode = SingleInstanceStartup.Run(
            new RejectingGuard(),
            notification,
            () => applicationStarted = true);

        Assert.Equal(1, exitCode);
        Assert.Equal(SingleInstanceStartup.AlreadyRunningMessage, notification.Message);
        Assert.False(applicationStarted);
    }

    [Fact]
    public void Run_WhenLeaseIsAcquired_StartsApplicationAndDisposesLease()
    {
        var lease = new RecordingLease();
        var applicationStarted = false;

        var exitCode = SingleInstanceStartup.Run(
            new AcquiringGuard(lease),
            new RecordingStartupNotification(),
            () => applicationStarted = true);

        Assert.Equal(0, exitCode);
        Assert.True(applicationStarted);
        Assert.True(lease.IsDisposed);
    }

    private sealed class RejectingGuard : ISingleInstanceGuard
    {
        public ISingleInstanceLease? TryAcquire() => null;
    }

    private sealed class AcquiringGuard(ISingleInstanceLease lease) : ISingleInstanceGuard
    {
        public ISingleInstanceLease? TryAcquire() => lease;
    }

    private sealed class RecordingLease : ISingleInstanceLease
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }

    private sealed class RecordingStartupNotification : IStartupNotification
    {
        public string? Message { get; private set; }

        public void ShowBlockingError(string message) => Message = message;
    }
}
