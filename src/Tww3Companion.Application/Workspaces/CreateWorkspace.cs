using Tww3Companion.Application.Abstractions;
using Tww3Companion.Application.Common;
using Tww3Companion.Application.Settings;
using Tww3Companion.Domain.Validation;
using Tww3Companion.Domain.Workspaces;

namespace Tww3Companion.Application.Workspaces;

public sealed class CreateWorkspace(
    IWorkspaceStore workspaceStore,
    IApplicationSettingsStore settingsStore,
    IClock clock,
    IUuidGenerator uuidGenerator)
{
  public async Task<OperationResult<Workspace>> ExecuteAsync(
      string displayName,
      string path,
      CancellationToken cancellationToken)
  {
    var nameResult = WorkspaceName.Create(displayName);
    if (nameResult is ValidationResult<WorkspaceName>.Failure nameFailure)
    {
      return Failure(nameFailure.Error.Code, nameFailure.Error.Message);
    }

    var idResult = WorkspaceId.Parse(uuidGenerator.NewUuid());
    if (idResult is ValidationResult<WorkspaceId>.Failure idFailure)
    {
      return Failure(idFailure.Error.Code, idFailure.Error.Message);
    }

    var now = clock.UtcNow;
    var workspaceResult = Workspace.Create(
        ((ValidationResult<WorkspaceId>.Success)idResult).Value,
        ((ValidationResult<WorkspaceName>.Success)nameResult).Value,
        now,
        now);
    if (workspaceResult is ValidationResult<Workspace>.Failure workspaceFailure)
    {
      return Failure(workspaceFailure.Error.Code, workspaceFailure.Error.Message);
    }

    var result = await workspaceStore.CreateAsync(
        path,
        ((ValidationResult<Workspace>.Success)workspaceResult).Value,
        cancellationToken);
    if (result is not OperationResult<Workspace>.Success)
    {
      return result;
    }

    var settingsError = await RecentWorkspaceUpdater.AddAsync(
        settingsStore,
        path,
        ((ValidationResult<WorkspaceName>.Success)nameResult).Value.ToString(),
        now,
        cancellationToken);
    return settingsError is null ? result : new OperationResult<Workspace>.Failure(settingsError);
  }

  private static OperationResult<Workspace>.Failure Failure(string code, string message) =>
      new(new OperationError(code, message, false, "Correct the value and try again."));
}
