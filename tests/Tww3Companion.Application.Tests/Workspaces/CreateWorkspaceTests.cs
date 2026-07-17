using Tww3Companion.Application.Abstractions;
using Tww3Companion.Application.Common;
using Tww3Companion.Application.Settings;
using Tww3Companion.Application.Workspaces;
using Tww3Companion.Domain.Workspaces;
using Xunit;

namespace Tww3Companion.Application.Tests.Workspaces;

public sealed class CreateWorkspaceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 18, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExecuteAsync_BlankName_FailsBeforeStorage()
    {
        var store = new FakeWorkspaceStore();
        var settings = new FakeSettingsStore();
        var useCase = CreateUseCase(store, settings);

        var result = await useCase.ExecuteAsync("  ", @"C:\Workspaces\new.tww3c", TestContext.Current.CancellationToken);

        var failure = Assert.IsType<OperationResult<Workspace>.Failure>(result);
        Assert.Equal("workspace.name.required", failure.Error.Code);
        Assert.Equal(0, store.CreateCalls);
        Assert.Equal(0, settings.SaveCalls);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingTarget_ReturnsTypedFailureWithoutRecordingRecent()
    {
        var store = new FakeWorkspaceStore
        {
            CreateResult = new OperationResult<Workspace>.Failure(new OperationError(
                "workspace.target.exists", "Target exists.", false, "Choose another path."))
        };
        var settings = new FakeSettingsStore();

        var result = await CreateUseCase(store, settings).ExecuteAsync(
            "Campaign", @"C:\Workspaces\existing.tww3c", TestContext.Current.CancellationToken);

        var failure = Assert.IsType<OperationResult<Workspace>.Failure>(result);
        Assert.Equal("workspace.target.exists", failure.Error.Code);
        Assert.Equal(1, store.CreateCalls);
        Assert.Equal(0, settings.SaveCalls);
    }

    [Fact]
    public async Task ExecuteAsync_Success_RecordsRecentOnlyAfterStorageSucceeds()
    {
        var events = new List<string>();
        var store = new FakeWorkspaceStore(events);
        var settings = new FakeSettingsStore(events);

        var result = await CreateUseCase(store, settings).ExecuteAsync(
            "  Campaign  ", @"C:\Workspaces\campaign.tww3c", TestContext.Current.CancellationToken);

        var success = Assert.IsType<OperationResult<Workspace>.Success>(result);
        Assert.Equal("Campaign", success.Value.Name.ToString());
        Assert.Equal(new[] { "create", "save-settings" }, events);
        Assert.Equal(@"C:\Workspaces\campaign.tww3c", settings.Saved!.RecentWorkspaces[0].Path);
        Assert.Equal(Now, settings.Saved.RecentWorkspaces[0].LastOpenedUtc);
    }

    private static CreateWorkspace CreateUseCase(FakeWorkspaceStore store, FakeSettingsStore settings) =>
        new(store, settings, new FakeClock(Now), new FakeUuidGenerator("6f9619ff-8b86-4d11-b42d-00c04fc964ff"));
}

internal sealed class FakeWorkspaceStore(List<string>? events = null) : IWorkspaceStore
{
    public int CreateCalls { get; private set; }
    public OperationResult<Workspace>? CreateResult { get; init; }

    public Task<OperationResult<Workspace>> CreateAsync(string path, Workspace workspace, CancellationToken cancellationToken)
    {
        CreateCalls++;
        events?.Add("create");
        return Task.FromResult(CreateResult ?? new OperationResult<Workspace>.Success(workspace));
    }

    public Task<OperationResult<Workspace>> OpenAsync(string path, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

internal sealed class FakeSettingsStore(List<string>? events = null) : IApplicationSettingsStore
{
    public ApplicationSettings Current { get; init; } = new(1, "System", null, []);
    public ApplicationSettings? Saved { get; private set; }
    public int SaveCalls { get; private set; }

    public Task<ApplicationSettings> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(Current);

    public Task<OperationResult<ApplicationSettings>> SaveAsync(ApplicationSettings settings, CancellationToken cancellationToken)
    {
        events?.Add("save-settings");
        SaveCalls++;
        Saved = settings;
        return Task.FromResult<OperationResult<ApplicationSettings>>(new OperationResult<ApplicationSettings>.Success(settings));
    }
}

internal sealed class FakeClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow => utcNow;
}

internal sealed class FakeUuidGenerator(string value) : IUuidGenerator
{
    public string NewUuid() => value;
}
