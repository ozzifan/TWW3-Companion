using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Tww3Companion.Application.Abstractions;
using Tww3Companion.Application.Common;
using Tww3Companion.Infrastructure.Paths;
using Tww3Companion.Infrastructure.Settings;

namespace Tww3Companion.Infrastructure.Storage.Backups;

public sealed partial class WorkspaceBackupService
{
    private readonly SqliteConnectionFactory connectionFactory;
    private readonly ManagedPaths managedPaths;
    private readonly IClock clock;
    private readonly IAtomicFileSystem fileSystem;

    public WorkspaceBackupService(SqliteConnectionFactory connectionFactory, ManagedPaths managedPaths, IClock clock)
        : this(connectionFactory, managedPaths, clock, new AtomicFileSystem()) { }

    public WorkspaceBackupService(SqliteConnectionFactory connectionFactory, ManagedPaths managedPaths, IClock clock, IAtomicFileSystem fileSystem)
    {
        this.connectionFactory = connectionFactory;
        this.managedPaths = managedPaths;
        this.clock = clock;
        this.fileSystem = fileSystem;
    }

    public async Task<OperationResult<string>> CreateAsync(
        string workspacePath,
        string workspaceUuid,
        BackupReason reason,
        CancellationToken token)
    {
        if (!IsCanonicalUuid(workspaceUuid))
            return Failure("workspace.backup.identity.invalid", "The Workspace identity is invalid.");

        var directory = Path.Combine(managedPaths.BackupsDirectory, workspaceUuid);
        var destination = Path.Combine(directory, $"{clock.UtcNow.UtcDateTime:yyyyMMdd'T'HHmmssfff'Z'}.{Reason(reason)}.tww3c");
        try
        {
            token.ThrowIfCancellationRequested();
            Directory.CreateDirectory(directory);
            using (fileSystem.CreateWriteProbe(directory)) { }
            if (File.Exists(destination))
                return Failure("workspace.backup.exists", "A managed backup with this timestamp already exists.");
            await using var source = await connectionFactory.OpenAsync(workspacePath, token);
            await using var backup = await connectionFactory.OpenAsync(destination, token);
            source.BackupDatabase(backup);
            return new OperationResult<string>.Success(destination);
        }
        catch (OperationCanceledException)
        {
            if (File.Exists(destination)) File.Delete(destination);
            return Failure("workspace.backup.cancelled", "Workspace backup was cancelled.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SqliteException)
        {
            if (File.Exists(destination)) File.Delete(destination);
            return Failure("workspace.backup.failed", "The Workspace backup could not be created.");
        }
    }

    public Task CleanupAsync(string workspaceUuid, CancellationToken token)
    {
        if (!IsCanonicalUuid(workspaceUuid)) return Task.CompletedTask;
        var directory = Path.Combine(managedPaths.BackupsDirectory, workspaceUuid);
        if (!Directory.Exists(directory)) return Task.CompletedTask;
        var attributable = Directory.EnumerateFiles(directory)
            .Where(path => IsManagedBackupName(Path.GetFileName(path)))
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .ToArray();
        foreach (var path in attributable.Take(Math.Max(0, attributable.Length - 5)))
        {
            token.ThrowIfCancellationRequested();
            File.Delete(path);
        }
        return Task.CompletedTask;
    }

    private static bool IsCanonicalUuid(string value) =>
        Guid.TryParseExact(value, "D", out var parsed) && value == parsed.ToString("D", CultureInfo.InvariantCulture);

    private static bool IsManagedBackupName(string name) =>
        ManagedBackupName().Match(name) is { Success: true } match &&
        DateTimeOffset.TryParseExact(
            match.Groups[1].Value,
            "yyyyMMdd'T'HHmmssfff'Z'",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out _);

    private static string Reason(BackupReason reason) => reason switch
    {
        BackupReason.PreMigration => "pre-migration",
        BackupReason.PreRestore => "pre-restore",
        _ => throw new ArgumentOutOfRangeException(nameof(reason))
    };

    private static OperationResult<string>.Failure Failure(string code, string message) =>
        new(new OperationError(code, message, false, "Return Home and retry the operation."));

    [GeneratedRegex(@"^(\d{8}T\d{9}Z)\.(pre-migration|pre-restore)\.tww3c$", RegexOptions.CultureInvariant)]
    private static partial Regex ManagedBackupName();
}
