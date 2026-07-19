using Tww3Companion.Application.Common;

namespace Tww3Companion.Infrastructure.Paths;

public interface IManagedPathInitializer
{
  Task<OperationResult<ManagedPaths>> InitializeAsync(
      ManagedPaths paths,
      CancellationToken cancellationToken);
}
