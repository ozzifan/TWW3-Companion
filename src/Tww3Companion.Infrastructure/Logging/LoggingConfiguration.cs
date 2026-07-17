using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using Tww3Companion.Infrastructure.Paths;

namespace Tww3Companion.Infrastructure.Logging;

public static class LoggingConfiguration
{
    public static ILoggerProvider CreateProvider(ManagedPaths paths)
    {
        var logger = new LoggerConfiguration()
            .WriteTo.File(
                path: Path.Combine(paths.LogsDirectory, "tww3-companion-.log"),
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: 7,
                shared: false)
            .CreateLogger();

        return new SerilogLoggerProvider(logger, dispose: true);
    }
}
