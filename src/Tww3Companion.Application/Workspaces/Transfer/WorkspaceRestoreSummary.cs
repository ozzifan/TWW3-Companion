namespace Tww3Companion.Application.Workspaces.Transfer;

public sealed record WorkspaceRestoreSummary(
    string WorkspaceId,
    string DisplayName,
    string Format,
    int ModCount,
    int CollectionCount,
    int MembershipCount);

public sealed record InspectedWorkspaceRestore(
    string ExportPath,
    WorkspaceTransferSnapshot Snapshot,
    WorkspaceRestoreSummary Summary);
