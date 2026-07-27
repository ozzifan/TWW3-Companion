namespace Tww3Companion.Application.Workspaces.Transfer;

public sealed record WorkspaceTransferSnapshot(
    string Format,
    WorkspaceTransferWorkspace Workspace,
    IReadOnlyList<WorkspaceTransferMod> Mods,
    IReadOnlyList<WorkspaceTransferSourceReference> SourceReferences,
    IReadOnlyList<WorkspaceTransferCollection> Collections,
    IReadOnlyList<WorkspaceTransferMembership> Memberships);

public sealed record WorkspaceTransferWorkspace(
    string Id,
    string DisplayName,
    DateTimeOffset CreatedUtc,
    DateTimeOffset ModifiedUtc);

public sealed record WorkspaceTransferMod(string Id, string DisplayName);

public sealed record WorkspaceTransferSourceReference(
    string SourceType,
    string ExternalId,
    string ModId);

public sealed record WorkspaceTransferCollection(string Id, string DisplayName);

public sealed record WorkspaceTransferMembership(
    string CollectionId,
    string ModId,
    int Position);
