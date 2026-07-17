using Tww3Companion.Application.Common;
using Tww3Companion.Domain.Workspaces;

namespace Tww3Companion.Application.Workspaces;

public interface IWorkspaceStore
{
    Task<OperationResult<Workspace>> CreateAsync(
        string path,
        Workspace workspace,
        CancellationToken cancellationToken);

    Task<OperationResult<Workspace>> OpenAsync(
        string path,
        CancellationToken cancellationToken);
}
