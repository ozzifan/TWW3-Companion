using System.Reflection;
using Tww3Companion.Application.Workspaces;
using Xunit;

namespace Tww3Companion.Application.Tests.Workspaces;

public sealed class WorkspaceQueryTests
{
  [Fact]
  public void IWorkspaceQuery_GetLibrarySnapshotAsync_ReturnsTaskOfWorkspaceLibrarySnapshot()
  {
    var method = typeof(IWorkspaceQuery).GetMethod(
        "GetLibrarySnapshotAsync",
        BindingFlags.Instance | BindingFlags.Public,
        binder: null,
        types: [typeof(CancellationToken)],
        modifiers: null);

    Assert.NotNull(method);
    Assert.Equal(typeof(Task<WorkspaceLibrarySnapshot>), method!.ReturnType);
  }
}
