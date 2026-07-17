using Tww3Companion.Application.Common;
using Tww3Companion.Application.Settings;
using Tww3Companion.Infrastructure.Settings;
using Tww3Companion.Infrastructure.Tests.Support;
using Xunit;

namespace Tww3Companion.Infrastructure.Tests.Settings;

public sealed class JsonApplicationSettingsStoreTests
{
    [Fact]
    public async Task LoadAsync_ReturnsDefaultsWhenFileIsMissing()
    {
        using var directory = new TemporaryDirectory();
        var store = new JsonApplicationSettingsStore(Path.Combine(directory.Path, "settings.json"));

        var settings = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new ApplicationSettings(1, "System", null, []), settings);
    }

    [Fact]
    public async Task LoadAsync_LeavesInvalidJsonUnchanged()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        await File.WriteAllTextAsync(path, "{not-json", TestContext.Current.CancellationToken);
        var store = new JsonApplicationSettingsStore(path);

        await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal("{not-json", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(directory.Path, "settings.invalid.*.json"));
    }

    [Fact]
    public async Task SaveAsync_PreservesPreviouslyInvalidJsonWithUtcTimestamp()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        await File.WriteAllTextAsync(path, "invalid", TestContext.Current.CancellationToken);
        var instant = new DateTimeOffset(2026, 7, 18, 1, 2, 3, 456, TimeSpan.Zero);
        var store = new JsonApplicationSettingsStore(path, utcNow: () => instant);
        await store.LoadAsync(TestContext.Current.CancellationToken);
        var settings = new ApplicationSettings(1, "Dark", null, []);

        var result = await store.SaveAsync(settings, TestContext.Current.CancellationToken);

        Assert.IsType<OperationResult<ApplicationSettings>.Success>(result);
        Assert.Equal("invalid", await File.ReadAllTextAsync(Path.Combine(directory.Path, "settings.invalid.20260718T010203456Z.json"), TestContext.Current.CancellationToken));
        var saved = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
        Assert.Contains("\"theme\": \"Dark\"", saved);
        Assert.DoesNotContain("SchemaVersion", saved);
    }

    [Fact]
    public async Task SaveAsync_DoesNotReplaceInvalidOriginalWhenPreservationFails()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "settings.json");
        await File.WriteAllTextAsync(path, "invalid", TestContext.Current.CancellationToken);
        var fileSystem = new PreservationFailingFileSystem();
        var store = new JsonApplicationSettingsStore(path, fileSystem);
        await store.LoadAsync(TestContext.Current.CancellationToken);

        var result = await store.SaveAsync(new ApplicationSettings(1, "Dark", null, []), TestContext.Current.CancellationToken);

        var failure = Assert.IsType<OperationResult<ApplicationSettings>.Failure>(result);
        Assert.False(failure.Error.PersistentChangeCommitted);
        Assert.Equal("invalid", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        Assert.False(fileSystem.WriteCalled);
    }
}

internal sealed class PreservationFailingFileSystem : IAtomicFileSystem
{
    public bool WriteCalled { get; private set; }
    public Stream CreateWriteProbe(string directory) => Stream.Null;
    public void MoveWithoutOverwrite(string source, string destination) => throw new IOException("seeded failure");
    public Task WriteAllTextAtomicallyAsync(string path, string content, CancellationToken token)
    {
        WriteCalled = true;
        return Task.CompletedTask;
    }
}
