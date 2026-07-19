namespace Tww3Companion.Desktop.Services;

public interface IWorkspaceDisposalCoordinator
{
  Task DisposeWorkspaceScopeAsync(CancellationToken cancellationToken);
}

public sealed class WorkspaceDisposalCoordinator : IWorkspaceDisposalCoordinator
{
  public Task DisposeWorkspaceScopeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
