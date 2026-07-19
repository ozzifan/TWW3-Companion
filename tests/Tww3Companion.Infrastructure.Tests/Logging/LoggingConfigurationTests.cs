using Microsoft.Extensions.Logging;
using Tww3Companion.Infrastructure.Logging;
using Tww3Companion.Infrastructure.Paths;
using Tww3Companion.Infrastructure.Tests.Support;
using Xunit;

namespace Tww3Companion.Infrastructure.Tests.Logging;

public sealed class LoggingConfigurationTests
{
  [Fact]
  public void CreateProvider_PersistsOnlyConstrainedPrivacySafeEvents()
  {
    using var directory = new TemporaryDirectory();
    var paths = ManagedPaths.ForRoot(ApplicationMode.Portable, directory.Path);
    Directory.CreateDirectory(paths.LogsDirectory);
    const string opaquePathId = "path-7f0c";
    const string secretFilename = "private-campaign-notes.tww3c";
    const string displayName = "Steve's Secret Campaign";
    const string sourceReference = "secret source reference";
    var fullPath = Path.Combine(directory.Path, secretFilename);
    var exception = CaptureSeededException(fullPath);

    using (var provider = LoggingConfiguration.CreateProvider(paths))
    {
      var logger = provider.CreateLogger("test");
      logger.LogInformation(new EventId(42), "Operation {OperationName} {WorkspacePathId}", "workspace.open", opaquePathId);
      logger.LogInformation("Operation {OperationName} {WorkspacePathId} {FileName}", "workspace.open", opaquePathId, secretFilename);
      logger.LogInformation("Operation {OperationName} {WorkspacePathId} {DisplayName}", "workspace.open", opaquePathId, displayName);
      logger.LogInformation("Operation {OperationName} {WorkspacePathId} {FullPath}", "workspace.open", opaquePathId, fullPath);
      logger.LogInformation("Imported text: {ImportedText}", "seeded private imported text");
      logger.LogInformation("Operation {OperationName} {WorkspacePathId} {SourceReference}", "workspace.open", opaquePathId, sourceReference);
      logger.LogInformation("Operation {OperationName} {WorkspacePathId}", secretFilename, opaquePathId);
      logger.LogError(
          exception,
          "Operation {OperationName} {WorkspacePathId} failed {FailureCategory}",
          "workspace.open",
          opaquePathId,
          "unexpected");
    }

    var log = File.ReadAllText(Assert.Single(Directory.GetFiles(paths.LogsDirectory, "*.log")));
    Assert.Contains(opaquePathId, log);
    Assert.DoesNotContain(secretFilename, log);
    Assert.DoesNotContain(displayName, log);
    Assert.DoesNotContain(fullPath, log);
    Assert.DoesNotContain("seeded private imported text", log);
    Assert.DoesNotContain(sourceReference, log);
    Assert.DoesNotContain(directory.Path, log);
    Assert.DoesNotContain(exception.Message, log);
    Assert.Contains("[System.InvalidOperationException]", log);
    Assert.Contains(nameof(ThrowSeededException), log);
    Assert.Contains("[42]", log);
    Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z", log);
  }

  private static Exception CaptureSeededException(string secretPath)
  {
    try
    {
      ThrowSeededException(secretPath);
    }
    catch (InvalidOperationException exception)
    {
      return exception;
    }

    throw new InvalidOperationException("Unreachable.");
  }

  private static void ThrowSeededException(string secretPath) =>
      throw new InvalidOperationException($"secret exception at {secretPath}");
}
