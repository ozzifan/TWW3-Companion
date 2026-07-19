using Tww3Companion.Application.Common;
using Tww3Companion.Infrastructure.Settings;

namespace Tww3Companion.Infrastructure.Paths;

public sealed class ManagedPathInitializer(IAtomicFileSystem? fileSystem = null) : IManagedPathInitializer
{
  private readonly IAtomicFileSystem fileSystem = fileSystem ?? new AtomicFileSystem();

  public Task<OperationResult<ManagedPaths>> InitializeAsync(
      ManagedPaths paths,
      CancellationToken cancellationToken)
  {
    foreach (var directory in paths.RequiredDirectories)
    {
      cancellationToken.ThrowIfCancellationRequested();
      try
      {
        Directory.CreateDirectory(directory);
        using var probe = fileSystem.CreateWriteProbe(directory);
        probe.WriteByte(0);
      }
      catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
      {
        return Task.FromResult<OperationResult<ManagedPaths>>(
            new OperationResult<ManagedPaths>.Failure(new OperationError(
                "startup.managed-path.unwritable",
                $"{paths.Mode} managed directory is not writable: {directory}",
                false,
                paths.Mode == ApplicationMode.Portable
                    ? "Move the portable folder to a writable location or correct its permissions."
                    : "Correct the managed directory permissions.")));
      }
    }

    return Task.FromResult<OperationResult<ManagedPaths>>(
        new OperationResult<ManagedPaths>.Success(paths));
  }
}
