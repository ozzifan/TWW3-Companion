using Tww3Companion.Application.Common;
using Tww3Companion.Infrastructure.Paths;
using Tww3Companion.Infrastructure.Settings;
using Tww3Companion.Infrastructure.Tests.Support;
using Xunit;

namespace Tww3Companion.Infrastructure.Tests.Paths;

public sealed class ManagedPathInitializerTests
{
  [Fact]
  public void Detect_UsesPortableDataDirectoryWhenMarkerExists()
  {
    using var executable = new TemporaryDirectory();
    File.WriteAllText(Path.Combine(executable.Path, "portable.flag"), "");

    var paths = ManagedPaths.Detect(executable.Path, "ignored");

    Assert.Equal(ApplicationMode.Portable, paths.Mode);
    Assert.Equal(Path.Combine(executable.Path, "Data"), paths.RootDirectory);
    Assert.Equal(Path.Combine(executable.Path, "Data", "settings.json"), paths.SettingsFile);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void Detect_DoesNotRequireLocalApplicationDataInPortableMode(string? unavailableLocalAppData)
  {
    using var executable = new TemporaryDirectory();
    File.WriteAllText(Path.Combine(executable.Path, "portable.flag"), "");

    var paths = ManagedPaths.Detect(executable.Path, unavailableLocalAppData!);

    Assert.Equal(ApplicationMode.Portable, paths.Mode);
  }

  [Fact]
  public void Detect_UsesLocalApplicationDataWithoutMarker()
  {
    using var executable = new TemporaryDirectory();
    using var local = new TemporaryDirectory();

    var paths = ManagedPaths.Detect(executable.Path, local.Path);

    Assert.Equal(ApplicationMode.Installed, paths.Mode);
    Assert.Equal(Path.Combine(local.Path, "TWW3 Companion"), paths.RootDirectory);
  }

  [Fact]
  public async Task InitializeAsync_CreatesAndProbesEveryManagedDirectory()
  {
    using var root = new TemporaryDirectory();
    var paths = ManagedPaths.ForRoot(ApplicationMode.Installed, Path.Combine(root.Path, "managed"));
    var fileSystem = new RecordingAtomicFileSystem();

    var result = await new ManagedPathInitializer(fileSystem).InitializeAsync(paths, TestContext.Current.CancellationToken);

    Assert.IsType<OperationResult<ManagedPaths>.Success>(result);
    Assert.All(paths.RequiredDirectories, directory => Assert.True(Directory.Exists(directory)));
    Assert.Equal(paths.RequiredDirectories.Order(), fileSystem.ProbedDirectories.Order());
    Assert.All(fileSystem.Probes, probe => Assert.True(probe.Disposed));
  }

  [Fact]
  public async Task InitializeAsync_ReturnsUnwritableFailureAndDisposesProbe()
  {
    using var root = new TemporaryDirectory();
    var paths = ManagedPaths.ForRoot(ApplicationMode.Portable, Path.Combine(root.Path, "managed"));
    var fileSystem = new RecordingAtomicFileSystem { FailProbeNumber = 2 };

    var result = await new ManagedPathInitializer(fileSystem).InitializeAsync(paths, TestContext.Current.CancellationToken);

    var failure = Assert.IsType<OperationResult<ManagedPaths>.Failure>(result);
    Assert.Equal("startup.managed-path.unwritable", failure.Error.Code);
    Assert.Contains("Portable", failure.Error.Message);
    Assert.All(fileSystem.Probes, probe => Assert.True(probe.Disposed));
  }
}

internal sealed class RecordingAtomicFileSystem : IAtomicFileSystem
{
  private int probeCount;
  public int? FailProbeNumber { get; init; }
  public List<string> ProbedDirectories { get; } = [];
  public List<TrackingStream> Probes { get; } = [];

  public Stream CreateWriteProbe(string directory)
  {
    ProbedDirectories.Add(directory);
    if (++probeCount == FailProbeNumber)
    {
      throw new UnauthorizedAccessException();
    }

    var stream = new TrackingStream();
    Probes.Add(stream);
    return stream;
  }

  public void MoveWithoutOverwrite(string source, string destination) => File.Move(source, destination, overwrite: false);
  public Task WriteAllTextAtomicallyAsync(string path, string content, CancellationToken token) => File.WriteAllTextAsync(path, content, token);
}

internal sealed class TrackingStream : MemoryStream
{
  public bool Disposed { get; private set; }
  protected override void Dispose(bool disposing)
  {
    Disposed = true;
    base.Dispose(disposing);
  }
}
