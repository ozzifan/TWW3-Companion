using Avalonia;
using System;
using System.Runtime.Versioning;
using Tww3Companion.Desktop.Composition;
using Tww3Companion.Desktop.Startup;
using Tww3Companion.Infrastructure.Startup;

namespace Tww3Companion.Desktop;

public class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    [SupportedOSPlatform("windows")]
    public static int Main(string[] args)
    {
        if (Environment.GetEnvironmentVariable("TWW3_COMPANION_TEST_MODE") == "1"
            && args is ["--smoke-test", _] or ["--hold-single-instance", _])
        {
            return SmokeTestCommand.Run(args);
        }

        return ApplicationComposition.RunDesktopStartup(
            args,
            new WindowsSingleInstanceLease(),
            new NativeStartupDialog(),
            runtime =>
            {
                App.Runtime = runtime;
                try
                {
                    return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
                }
                finally
                {
                    App.Runtime = null;
                }
            });
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
