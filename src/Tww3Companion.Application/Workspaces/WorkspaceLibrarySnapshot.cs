namespace Tww3Companion.Application.Workspaces;

public sealed record WorkspaceLibraryMod(string ModId, string DisplayName);

public sealed record WorkspaceCollection(string CollectionId, string DisplayName);

public sealed record WorkspaceCollectionMembership(string CollectionId, string ModId);

public sealed record WorkspaceLibrarySnapshot(
    IReadOnlyList<WorkspaceLibraryMod> Mods,
    IReadOnlyList<WorkspaceCollection> Collections,
    IReadOnlyList<WorkspaceCollectionMembership> Memberships);
