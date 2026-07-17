using Microsoft.Extensions.Logging;
using Tww3Companion.Infrastructure.Logging;
using Tww3Companion.Infrastructure.Paths;
using Tww3Companion.Infrastructure.Tests.Support;
using Xunit;

namespace Tww3Companion.Infrastructure.Tests.Logging;

public sealed class LoggingConfigurationTests
{
    [Fact]
    public void CreateProvider_WritesOpaqueCallerIdentifierWithoutSensitivePathData()
    {
        using var directory = new TemporaryDirectory();
        var paths = ManagedPaths.ForRoot(ApplicationMode.Portable, directory.Path);
        Directory.CreateDirectory(paths.LogsDirectory);
        const string opaquePathId = "path-7f0c";
        const string secretFilename = "private-campaign-notes.tww3c";
        const string displayName = "Steve's Secret Campaign";

        using (var provider = LoggingConfiguration.CreateProvider(paths))
        {
            var logger = provider.CreateLogger("test");
            logger.LogInformation("Opened workspace {WorkspacePathId}", opaquePathId);
        }

        var log = File.ReadAllText(Assert.Single(Directory.GetFiles(paths.LogsDirectory, "*.log")));
        Assert.Contains(opaquePathId, log);
        Assert.DoesNotContain(secretFilename, log);
        Assert.DoesNotContain(displayName, log);
        Assert.DoesNotContain(directory.Path, log);
    }
}
