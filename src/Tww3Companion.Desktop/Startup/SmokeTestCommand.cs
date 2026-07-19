using System.Text.Json;
using Tww3Companion.Application.Common;
using Tww3Companion.Application.Startup;
using Tww3Companion.Desktop.Composition;
using Tww3Companion.Domain.Workspaces;
using Tww3Companion.Infrastructure.Startup;

namespace Tww3Companion.Desktop.Startup;

public static class SmokeTestCommand
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static int Run(string[] args)
    {
        if (Environment.GetEnvironmentVariable("TWW3_COMPANION_TEST_MODE") != "1")
        {
            return 2;
        }

        return args switch
        {
            ["--smoke-test", var directory] => RunSmokeTest(directory),
            ["--hold-single-instance", var milliseconds] => HoldSingleInstance(milliseconds),
            _ => 2
        };
    }

    private static int RunSmokeTest(string directory)
    {
        Directory.CreateDirectory(directory);
        using var runtime = ApplicationComposition.CreateSmokeTestRuntime(CreateSingleInstanceGuard());
        if (runtime is null)
        {
            return 1;
        }

        var workspacePath = Path.Combine(directory, "smoke.tww3c");
        var createResult = runtime.CreateWorkspace
            .ExecuteAsync("Smoke Workspace", workspacePath, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        if (createResult is not OperationResult<Workspace>.Success created)
        {
            return 1;
        }

        var openResult = runtime.OpenWorkspace
            .ExecuteAsync(workspacePath, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        if (openResult is not OperationResult<Workspace>.Success opened
            || opened.Value.Id != created.Value.Id
            || opened.Value.Name.ToString() != "Smoke Workspace")
        {
            return 1;
        }

        var resultPath = Path.Combine(directory, "smoke-result.json");
        File.WriteAllText(
            resultPath,
            JsonSerializer.Serialize(
                new
                {
                    WorkspaceId = opened.Value.Id.ToString(),
                    DisplayName = opened.Value.Name.ToString(),
                    ApplicationMode = runtime.ManagedPaths.Mode.ToString(),
                    ManagedRoot = runtime.ManagedPaths.RootDirectory
                },
                SerializerOptions));
        return 0;
    }

    private static int HoldSingleInstance(string millisecondsText)
    {
        var managedRoot = Environment.GetEnvironmentVariable("TWW3_COMPANION_TEST_MANAGED_ROOT");
        if (string.IsNullOrWhiteSpace(managedRoot))
        {
            throw new ArgumentException("TWW3_COMPANION_TEST_MANAGED_ROOT is required for --hold-single-instance.");
        }

        if (!int.TryParse(millisecondsText, out var milliseconds) || milliseconds < 0)
        {
            throw new ArgumentException("--hold-single-instance requires a non-negative millisecond duration.");
        }

        Directory.CreateDirectory(managedRoot);
        using var lease = CreateSingleInstanceGuard().TryAcquire();
        if (lease is null)
        {
            return 1;
        }

        File.WriteAllText(Path.Combine(managedRoot, "lease-acquired.signal"), "acquired");
        Thread.Sleep(milliseconds);
        return 0;
    }

    private static ISingleInstanceGuard CreateSingleInstanceGuard() =>
        OperatingSystem.IsWindows()
            ? new WindowsSingleInstanceLease()
            : new ProcessLocalSingleInstanceGuard();

    private sealed class ProcessLocalSingleInstanceGuard : ISingleInstanceGuard
    {
        public ISingleInstanceLease? TryAcquire() => new Lease();

        private sealed class Lease : ISingleInstanceLease
        {
            public void Dispose()
            {
            }
        }
    }
}
