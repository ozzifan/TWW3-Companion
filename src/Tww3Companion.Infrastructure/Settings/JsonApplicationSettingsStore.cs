using System.Text.Json;
using Tww3Companion.Application.Common;
using Tww3Companion.Application.Settings;

namespace Tww3Companion.Infrastructure.Settings;

public sealed class JsonApplicationSettingsStore : IApplicationSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string path;
    private readonly IAtomicFileSystem fileSystem;
    private readonly Func<DateTimeOffset> utcNow;
    private bool invalidFilePendingPreservation;

    public JsonApplicationSettingsStore(
        string path,
        IAtomicFileSystem? fileSystem = null,
        Func<DateTimeOffset>? utcNow = null)
    {
        this.path = path;
        this.fileSystem = fileSystem ?? new AtomicFileSystem();
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<ApplicationSettings> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return Defaults();
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var settings = await JsonSerializer.DeserializeAsync<ApplicationSettings>(
                stream,
                SerializerOptions,
                cancellationToken);
            return settings ?? MarkInvalidAndReturnDefaults();
        }
        catch (JsonException)
        {
            return MarkInvalidAndReturnDefaults();
        }
    }

    public async Task<OperationResult<ApplicationSettings>> SaveAsync(
        ApplicationSettings settings,
        CancellationToken cancellationToken)
    {
        var preservationCommitted = false;
        try
        {
            if (invalidFilePendingPreservation)
            {
                var timestamp = utcNow().ToUniversalTime().ToString("yyyyMMdd'T'HHmmssfff'Z'");
                var preservedPath = Path.Combine(
                    Path.GetDirectoryName(path)!,
                    $"settings.invalid.{timestamp}.json");
                fileSystem.MoveWithoutOverwrite(path, preservedPath);
                invalidFilePendingPreservation = false;
                preservationCommitted = true;
            }

            var json = JsonSerializer.Serialize(settings, SerializerOptions);
            await fileSystem.WriteAllTextAtomicallyAsync(path, json, cancellationToken);
            return new OperationResult<ApplicationSettings>.Success(settings);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new OperationResult<ApplicationSettings>.Failure(new OperationError(
                "settings.save.failed",
                "Application settings could not be saved.",
                preservationCommitted,
                "Retry or open the settings folder and correct its permissions."));
        }
    }

    private ApplicationSettings MarkInvalidAndReturnDefaults()
    {
        invalidFilePendingPreservation = true;
        return Defaults();
    }

    private static ApplicationSettings Defaults() => new(1, "System", null, []);
}
