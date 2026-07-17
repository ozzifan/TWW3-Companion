using Avalonia;
using System;
using System.Runtime.Versioning;
using Tww3Companion.Infrastructure.Startup;

namespace Tww3Companion.Desktop;

public class Program
{
    public const string AlreadyRunningMessage =
        "TWW3 Companion is already running for this Windows user. Close the existing installed or portable copy and try again.";

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    [SupportedOSPlatform("windows")]
    public static void Main(string[] args)
    {
        using var lease = new WindowsSingleInstanceLease().TryAcquire();
        if (lease is null)
        {
            Console.Error.WriteLine(AlreadyRunningMessage);
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
