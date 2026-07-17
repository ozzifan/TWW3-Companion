using Tww3Companion.Application.Common;
using Tww3Companion.Application.Settings;
using Tww3Companion.Application.Workspaces;
using Tww3Companion.Domain.Validation;
using Tww3Companion.Domain.Workspaces;
using Xunit;

namespace Tww3Companion.Application.Tests.Workspaces;

public sealed class OpenWorkspaceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 18, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExecuteAsync_FailedOpen_DoesNotRecordRecent()
    {
        var store = new OpenFakeWorkspaceStore(new OperationResult<Workspace>.Failure(
            new OperationError("workspace.open.invalid", "Invalid workspace.", false, "Choose another file.")));
        var settings = new FakeSettingsStore();

        var result = await new OpenWorkspace(store, settings, new FakeClock(Now)).ExecuteAsync(
            @"C:\Workspaces\bad.tww3c", TestContext.Current.CancellationToken);

        Assert.IsType<OperationResult<Workspace>.Failure>(result);
        Assert.Equal(0, settings.SaveCalls);
    }

    [Fact]
    public async Task ExecuteAsync_Success_RecordsRecentAfterOpenAndKeepsTenUniqueNewestPaths()
    {
        var events = new List<string>();
        var path = @"C:\Workspaces\campaign.tww3c";
        var existing = Enumerable.Range(0, 10)
            .Select(index => new RecentWorkspace(index == 5 ? path.ToUpperInvariant() : $@"C:\Workspaces\{index}.tww3c", Now.AddDays(-index - 1)))
            .ToArray();
        var settings = new FakeSettingsStore(events) { Current = new(1, "Dark", null, existing) };
        var store = new OpenFakeWorkspaceStore(SuccessfulWorkspace(), events);

        var result = await new OpenWorkspace(store, settings, new FakeClock(Now)).ExecuteAsync(
            path, TestContext.Current.CancellationToken);

        Assert.IsType<OperationResult<Workspace>.Success>(result);
        Assert.Equal(new[] { "open", "save-settings" }, events);
        Assert.Equal(10, settings.Saved!.RecentWorkspaces.Count);
        Assert.Equal(path, settings.Saved.RecentWorkspaces[0].Path);
        Assert.Single(settings.Saved.RecentWorkspaces, recent => string.Equals(recent.Path, path, StringComparison.OrdinalIgnoreCase));
    }

    private static OperationResult<Workspace> SuccessfulWorkspace()
    {
        var id = Assert.IsType<ValidationResult<WorkspaceId>.Success>(WorkspaceId.Parse("6f9619ff-8b86-4d11-b42d-00c04fc964ff")).Value;
        var name = Assert.IsType<ValidationResult<WorkspaceName>.Success>(WorkspaceName.Create("Campaign")).Value;
        var workspace = Assert.IsType<ValidationResult<Workspace>.Success>(Workspace.Create(id, name, Now, Now)).Value;
        return new OperationResult<Workspace>.Success(workspace);
    }
}

internal sealed class OpenFakeWorkspaceStore(OperationResult<Workspace> result, List<string>? events = null) : IWorkspaceStore
{
    public Task<OperationResult<Workspace>> CreateAsync(string path, Workspace workspace, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<OperationResult<Workspace>> OpenAsync(string path, CancellationToken cancellationToken)
    {
        events?.Add("open");
        return Task.FromResult(result);
    }
}
