using Tww3Companion.Application.Workspaces;
using Xunit;

namespace Tww3Companion.Application.Tests.Workspaces;

public sealed class WorkspaceQueryTests
{
  [Fact]
  public async Task GetSnapshotAsync_ReturnsLibraryDataFromTheImplementation()
  {
    var query = new FakeWorkspaceQuery(
      new WorkspaceLibrarySnapshot(
        [
          new WorkspaceLibraryMod("mod-1", "Mod One"),
        ],
        [
          new WorkspaceCollection("collection-1", "Collection One"),
        ],
        [
          new WorkspaceCollectionMembership("collection-1", "mod-1"),
        ]));

    var snapshot = await query.GetSnapshotAsync(CancellationToken.None);

    Assert.Single(snapshot.Mods);
    Assert.Equal("mod-1", snapshot.Mods[0].ModId);
    Assert.Equal("Mod One", snapshot.Mods[0].DisplayName);

    Assert.Single(snapshot.Collections);
    Assert.Equal("collection-1", snapshot.Collections[0].CollectionId);
    Assert.Equal("Collection One", snapshot.Collections[0].DisplayName);

    Assert.Single(snapshot.Memberships);
    Assert.Equal("collection-1", snapshot.Memberships[0].CollectionId);
    Assert.Equal("mod-1", snapshot.Memberships[0].ModId);
  }

  private sealed class FakeWorkspaceQuery(WorkspaceLibrarySnapshot snapshot)
    : IWorkspaceQuery
  {
    public Task<WorkspaceLibrarySnapshot> GetSnapshotAsync(
      CancellationToken cancellationToken) =>
      Task.FromResult(snapshot);
  }
}
