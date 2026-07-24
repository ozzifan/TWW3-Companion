using Tww3Companion.Application.Workspaces;
using Xunit;

namespace Tww3Companion.Application.Tests.Workspaces;

public sealed class WorkspaceLibrarySnapshotTests
{
  [Fact]
  public void SnapshotCarriesGlobalModsCollectionsAndMemberships()
  {
    var snapshot = new WorkspaceLibrarySnapshot(
        [
            new WorkspaceLibraryMod("mod-1", "Alpha Mod"),
            new WorkspaceLibraryMod("mod-2", "Beta Mod")
        ],
        [
            new WorkspaceCollection("collection-1", "Core Collection")
        ],
        [
            new WorkspaceCollectionMembership("collection-1", "mod-1")
        ]);

    Assert.Equal(2, snapshot.Mods.Count);
    Assert.Single(snapshot.Collections);
    Assert.Single(snapshot.Memberships);
    Assert.Equal("mod-1", snapshot.Memberships[0].ModId);
  }
}
