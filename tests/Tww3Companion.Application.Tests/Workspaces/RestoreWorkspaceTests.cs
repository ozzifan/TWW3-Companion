using Tww3Companion.Application.Common;
using Tww3Companion.Application.Workspaces.Transfer;
using Tww3Companion.Domain.Workspaces;
using Xunit;

namespace Tww3Companion.Application.Tests.Workspaces;

public sealed class RestoreWorkspaceTests
{
  [Fact]
  public async Task Inspect_ReturnsCountsWithoutWritingDestination()
  {
    var snapshot = SampleSnapshot();
    var store = new FakeTransferStore
    {
      ReadExportResult = new OperationResult<WorkspaceTransferSnapshot>.Success(snapshot)
    };

    var result = await new InspectWorkspaceRestore(store).ExecuteAsync(
        @"C:\export.json",
        TestContext.Current.CancellationToken);

    var inspected = Assert.IsType<OperationResult<InspectedWorkspaceRestore>.Success>(result).Value;
    Assert.Equal(1, inspected.Summary.ModCount);
    Assert.Equal(1, inspected.Summary.CollectionCount);
    Assert.Equal(1, inspected.Summary.MembershipCount);
    Assert.Equal(snapshot.Workspace.Id, inspected.Summary.WorkspaceId);
    Assert.Equal(0, store.RestoreNewCalls);
    Assert.Equal(0, store.ReplaceCalls);
  }

  [Fact]
  public async Task Inspect_InvalidExport_ReturnsFailure()
  {
    var store = new FakeTransferStore
    {
      ReadExportResult = new OperationResult<WorkspaceTransferSnapshot>.Failure(new OperationError(
          "workspace.transfer.format.unsupported",
          "Unsupported.",
          false,
          "Retry."))
    };

    var result = await new InspectWorkspaceRestore(store).ExecuteAsync(
        @"C:\export.json",
        TestContext.Current.CancellationToken);

    Assert.IsType<OperationResult<InspectedWorkspaceRestore>.Failure>(result);
  }

  [Fact]
  public async Task RestoreNew_ReReadsExportBeforeRestore()
  {
    var snapshot = SampleSnapshot();
    var store = new FakeTransferStore
    {
      ReadExportResult = new OperationResult<WorkspaceTransferSnapshot>.Success(snapshot),
      RestoreNewResult = new OperationResult<Workspace>.Success(CreateWorkspace())
    };
    var inspected = new InspectedWorkspaceRestore(@"C:\export.json", snapshot, CreateSummary(snapshot));

    await new RestoreWorkspace(store).RestoreNewAsync(
        inspected,
        @"C:\dest.tww3c",
        TestContext.Current.CancellationToken);

    Assert.Equal(1, store.ReadExportCalls);
    Assert.Equal(1, store.RestoreNewCalls);
  }

  [Fact]
  public async Task RestoreNew_WhenExportChanged_ReturnsSourceChanged()
  {
    var original = SampleSnapshot();
    var changed = original with
    {
      Workspace = original.Workspace with { DisplayName = "Changed" }
    };
    var store = new FakeTransferStore
    {
      ReadExportResult = new OperationResult<WorkspaceTransferSnapshot>.Success(changed)
    };
    var inspected = new InspectedWorkspaceRestore(@"C:\export.json", original, CreateSummary(original));

    var failure = Assert.IsType<OperationResult<Workspace>.Failure>(
        await new RestoreWorkspace(store).RestoreNewAsync(
            inspected,
            @"C:\dest.tww3c",
            TestContext.Current.CancellationToken));

    Assert.Equal("workspace.restore.source.changed", failure.Error.Code);
    Assert.Equal(0, store.RestoreNewCalls);
  }

  [Fact]
  public async Task Replace_WhenNotConfirmed_PerformsNoBackupOrWrite()
  {
    var snapshot = SampleSnapshot();
    var store = new FakeTransferStore
    {
      ReadExportResult = new OperationResult<WorkspaceTransferSnapshot>.Success(snapshot)
    };
    var inspected = new InspectedWorkspaceRestore(@"C:\export.json", snapshot, CreateSummary(snapshot));

    var failure = Assert.IsType<OperationResult<Workspace>.Failure>(
        await new RestoreWorkspace(store).ReplaceAsync(
            inspected,
            @"C:\dest.tww3c",
            confirmed: false,
            TestContext.Current.CancellationToken));

    Assert.Equal("workspace.restore.unconfirmed", failure.Error.Code);
    Assert.Equal(0, store.ReadExportCalls);
    Assert.Equal(0, store.ReplaceCalls);
  }

  [Fact]
  public async Task Replace_RevalidatesBeforeCallingStore()
  {
    var snapshot = SampleSnapshot();
    var store = new FakeTransferStore
    {
      ReadExportResult = new OperationResult<WorkspaceTransferSnapshot>.Success(snapshot),
      ReplaceResult = new OperationResult<Workspace>.Success(CreateWorkspace())
    };
    var inspected = new InspectedWorkspaceRestore(@"C:\export.json", snapshot, CreateSummary(snapshot));

    await new RestoreWorkspace(store).ReplaceAsync(
        inspected,
        @"C:\dest.tww3c",
        confirmed: true,
        TestContext.Current.CancellationToken);

    Assert.Equal(1, store.ReadExportCalls);
    Assert.Equal(1, store.ReplaceCalls);
  }

  [Fact]
  public async Task Replace_WhenExportChanged_ReturnsSourceChanged()
  {
    var original = SampleSnapshot();
    var changed = original with
    {
      Workspace = original.Workspace with { DisplayName = "Changed" }
    };
    var store = new FakeTransferStore
    {
      ReadExportResult = new OperationResult<WorkspaceTransferSnapshot>.Success(changed)
    };
    var inspected = new InspectedWorkspaceRestore(@"C:\export.json", original, CreateSummary(original));

    var failure = Assert.IsType<OperationResult<Workspace>.Failure>(
        await new RestoreWorkspace(store).ReplaceAsync(
            inspected,
            @"C:\dest.tww3c",
            confirmed: true,
            TestContext.Current.CancellationToken));

    Assert.Equal("workspace.restore.source.changed", failure.Error.Code);
    Assert.Equal(0, store.ReplaceCalls);
  }

  private static WorkspaceTransferSnapshot SampleSnapshot() =>
      WorkspaceTransferValidationTestsExtensions.ValidSnapshot();

  private static WorkspaceRestoreSummary CreateSummary(WorkspaceTransferSnapshot snapshot) =>
      new(
          snapshot.Workspace.Id,
          snapshot.Workspace.DisplayName,
          snapshot.Format,
          snapshot.Mods.Count,
          snapshot.Collections.Count,
          snapshot.Memberships.Count);

  private static Workspace CreateWorkspace()
  {
    var workspaceId = WorkspaceId.Parse("11111111-1111-4111-8111-111111111111") is Domain.Validation.ValidationResult<WorkspaceId>.Success parsedId
        ? parsedId.Value
        : throw new InvalidOperationException();
    var name = WorkspaceName.Create("My Workspace") is Domain.Validation.ValidationResult<WorkspaceName>.Success parsedName
        ? parsedName.Value
        : throw new InvalidOperationException();
    return Workspace.Create(
        workspaceId,
        name,
        DateTimeOffset.Parse("2026-07-25T10:00:00Z"),
        DateTimeOffset.Parse("2026-07-25T11:00:00Z")) is Domain.Validation.ValidationResult<Workspace>.Success parsedWorkspace
        ? parsedWorkspace.Value
        : throw new InvalidOperationException();
  }

  private sealed class FakeTransferStore : IWorkspaceTransferStore
  {
    public OperationResult<WorkspaceTransferSnapshot> ReadExportResult { get; init; } =
        new OperationResult<WorkspaceTransferSnapshot>.Failure(new OperationError(
            "workspace.restore.source.invalid",
            "Not configured.",
            false,
            "Retry."));

    public IReadOnlyList<OperationResult<WorkspaceTransferSnapshot>> ReadExportResults { get; init; } = [];

    public OperationResult<Workspace> RestoreNewResult { get; init; } =
        new OperationResult<Workspace>.Failure(new OperationError(
            "workspace.restore.failed",
            "Not configured.",
            false,
            "Retry."));

    public OperationResult<Workspace> ReplaceResult { get; init; } =
        new OperationResult<Workspace>.Failure(new OperationError(
            "workspace.restore.failed",
            "Not configured.",
            false,
            "Retry."));

    public int ReadExportCalls { get; private set; }
    public int RestoreNewCalls { get; private set; }
    public int ReplaceCalls { get; private set; }

    public Task<OperationResult<WorkspaceTransferSnapshot>> ReadSnapshotAsync(
        string workspacePath,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<OperationResult<WorkspaceTransferSnapshot>> ReadExportAsync(
        string exportPath,
        CancellationToken cancellationToken)
    {
      ReadExportCalls++;
      if (ReadExportResults.Count >= ReadExportCalls)
      {
        return Task.FromResult(ReadExportResults[ReadExportCalls - 1]);
      }

      return Task.FromResult(ReadExportResult);
    }

    public Task<OperationResult<string>> WriteExportAsync(
        WorkspaceTransferSnapshot snapshot,
        string exportPath,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<OperationResult<Workspace>> RestoreNewAsync(
        WorkspaceTransferSnapshot snapshot,
        string destinationPath,
        CancellationToken cancellationToken)
    {
      RestoreNewCalls++;
      return Task.FromResult(RestoreNewResult);
    }

    public Task<OperationResult<Workspace>> ReplaceAsync(
        WorkspaceTransferSnapshot snapshot,
        string destinationPath,
        CancellationToken cancellationToken)
    {
      ReplaceCalls++;
      return Task.FromResult(ReplaceResult);
    }
  }
}
