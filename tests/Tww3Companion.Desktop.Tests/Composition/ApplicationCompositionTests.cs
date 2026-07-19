using System.Text.Json;
using Tww3Companion.Application.Startup;
using Tww3Companion.Desktop.Composition;
using Tww3Companion.Desktop.Startup;
using Xunit;

namespace Tww3Companion.Desktop.Tests.Composition;

public sealed class ApplicationCompositionTests
{
    private const string AlreadyRunningMessage =
        "TWW3 Companion is already running for this Windows user. Close the existing installed or portable copy and try again.";

    [Fact]
    public void CompositionUsesTheApprovedStartupOrder()
    {
        var composition = ApplicationComposition.CreateForTest();

        Assert.Equal(
            [
                "detect application mode",
                "initialize and probe managed paths",
                "acquire single-instance lease",
                "configure logging",
                "load settings",
                "construct Infrastructure adapters",
                "construct Application use cases",
                "construct the shared shell view model and nested views",
                "evaluate the current work area and show Compatibility or Home"
            ],
            composition.StartupSteps);
    }

    [Fact]
    public void ManagedPathFailureShowsNativeBlockingDialogAndDoesNotWriteLogEntry()
    {
        using var directory = new TemporaryDirectory();
        var dialog = new RecordingNativeStartupDialog();
        var options = new CompositionTestOptions
        {
            ExecutableDirectory = directory.Path,
            LocalApplicationDataDirectory = directory.Path,
            NativeStartupDialog = dialog,
            ManagedPathInitializer = new FailingManagedPathInitializer()
        };

        var exitCode = ApplicationComposition.RunStartupForTest(options);

        Assert.Equal(1, exitCode);
        Assert.NotEmpty(dialog.Messages);
        Assert.False(Directory.EnumerateFiles(directory.Path, "*.log", SearchOption.AllDirectories).Any());
    }

    [Fact]
    public void SingleInstanceFailureShowsAlreadyRunningMessage()
    {
        var dialog = new RecordingNativeStartupDialog();
        var options = new CompositionTestOptions
        {
            NativeStartupDialog = dialog,
            SingleInstanceGuard = new RejectingSingleInstanceGuard()
        };

        var exitCode = ApplicationComposition.RunStartupForTest(options);

        Assert.Equal(1, exitCode);
        Assert.Equal(AlreadyRunningMessage, Assert.Single(dialog.Messages));
    }

    [Fact]
    public void RuntimeEvaluatesWorkAreaBeforeShellCanBePresented()
    {
        using var directory = new TemporaryDirectory();
        var runtime = ApplicationComposition.CreateRuntimeForTest(new CompositionTestOptions
        {
            ExecutableDirectory = directory.Path,
            LocalApplicationDataDirectory = directory.Path,
            WorkAreaWidth = 1000,
            WorkAreaHeight = 620
        });

        using (runtime)
        {
            Assert.NotNull(runtime);
            Assert.Equal(Tww3Companion.Desktop.ViewModels.ShellScreen.Compatibility, runtime.ShellViewModel.CurrentScreen);
        }
    }

    [Fact]
    public void RuntimeWithoutTestModeIgnoresManagedRootEnvironmentVariable()
    {
        using var executableDirectory = new TemporaryDirectory();
        using var localApplicationData = new TemporaryDirectory();
        using var managed = new TemporaryDirectory();
        var previousTestMode = Environment.GetEnvironmentVariable("TWW3_COMPANION_TEST_MODE");
        var previousManagedRoot = Environment.GetEnvironmentVariable("TWW3_COMPANION_TEST_MANAGED_ROOT");

        Environment.SetEnvironmentVariable("TWW3_COMPANION_TEST_MODE", null);
        Environment.SetEnvironmentVariable("TWW3_COMPANION_TEST_MANAGED_ROOT", managed.Path);
        try
        {
            var runtime = ApplicationComposition.CreateRuntimeForTest(new CompositionTestOptions
            {
                ExecutableDirectory = executableDirectory.Path,
                LocalApplicationDataDirectory = localApplicationData.Path
            });

            using (runtime)
            {
                Assert.NotNull(runtime);
                Assert.Equal(
                    Path.Combine(localApplicationData.Path, "TWW3 Companion"),
                    runtime.ManagedPaths.RootDirectory);
                Assert.NotEqual(managed.Path, runtime.ManagedPaths.RootDirectory);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("TWW3_COMPANION_TEST_MODE", previousTestMode);
            Environment.SetEnvironmentVariable("TWW3_COMPANION_TEST_MANAGED_ROOT", previousManagedRoot);
        }
    }

    [Fact]
    public void StartupNoLongerReliesOnMainWindowOpenedToChooseInitialScreen()
    {
        var desktopDirectory = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Tww3Companion.Desktop"));
        var windowCode = File.ReadAllText(Path.Combine(desktopDirectory, "Views", "MainWindow.axaml.cs"));
        var appCode = File.ReadAllText(Path.Combine(desktopDirectory, "App.axaml.cs"));

        Assert.DoesNotContain("Opened +=", windowCode);
        Assert.Contains("AttachTopLevel", appCode);
        Assert.True(
            appCode.IndexOf("AttachTopLevel", StringComparison.Ordinal) <
            appCode.IndexOf("desktop.MainWindow", StringComparison.Ordinal));
    }

    [Fact]
    public void SmokeTestWritesResultJsonAtDirectoryRoot()
    {
        using var directory = new TemporaryDirectory();
        using var managed = new TemporaryDirectory();
        var previousTestMode = Environment.GetEnvironmentVariable("TWW3_COMPANION_TEST_MODE");
        var previousManagedRoot = Environment.GetEnvironmentVariable("TWW3_COMPANION_TEST_MANAGED_ROOT");

        Environment.SetEnvironmentVariable("TWW3_COMPANION_TEST_MODE", "1");
        Environment.SetEnvironmentVariable("TWW3_COMPANION_TEST_MANAGED_ROOT", managed.Path);
        try
        {
            var exitCode = SmokeTestCommand.Run(["--smoke-test", directory.Path]);

            Assert.Equal(0, exitCode);
            var resultPath = Path.Combine(directory.Path, "smoke-result.json");
            Assert.True(File.Exists(resultPath));

            using var document = JsonDocument.Parse(File.ReadAllText(resultPath));
            Assert.True(document.RootElement.TryGetProperty("workspaceId", out _));
            Assert.Equal("Smoke Workspace", document.RootElement.GetProperty("displayName").GetString());
            Assert.True(document.RootElement.TryGetProperty("applicationMode", out _));
            Assert.True(document.RootElement.TryGetProperty("managedRoot", out _));
        }
        finally
        {
            Environment.SetEnvironmentVariable("TWW3_COMPANION_TEST_MODE", previousTestMode);
            Environment.SetEnvironmentVariable("TWW3_COMPANION_TEST_MANAGED_ROOT", previousManagedRoot);
        }
    }

    [Fact]
    public void SmokeTestUsesApplicationCompositionRootInsteadOfManualStartupWiring()
    {
        var desktopDirectory = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "Tww3Companion.Desktop"));
        var source = File.ReadAllText(Path.Combine(desktopDirectory, "Startup", "SmokeTestCommand.cs"));

        Assert.Contains("ApplicationComposition.CreateSmokeTestRuntime", source);
        Assert.DoesNotContain("new ManagedPathInitializer", source);
        Assert.DoesNotContain("new JsonApplicationSettingsStore", source);
    }

    [Fact]
    public void HoldSingleInstanceFailsCleanlyWhenManagedRootEnvironmentVariableIsMissing()
    {
        var previousTestMode = Environment.GetEnvironmentVariable("TWW3_COMPANION_TEST_MODE");
        var previousManagedRoot = Environment.GetEnvironmentVariable("TWW3_COMPANION_TEST_MANAGED_ROOT");

        Environment.SetEnvironmentVariable("TWW3_COMPANION_TEST_MODE", "1");
        Environment.SetEnvironmentVariable("TWW3_COMPANION_TEST_MANAGED_ROOT", null);
        try
        {
            var exception = Assert.Throws<ArgumentException>(() =>
                SmokeTestCommand.Run(["--hold-single-instance", "100"]));

            Assert.Contains("TWW3_COMPANION_TEST_MANAGED_ROOT", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("TWW3_COMPANION_TEST_MODE", previousTestMode);
            Environment.SetEnvironmentVariable("TWW3_COMPANION_TEST_MANAGED_ROOT", previousManagedRoot);
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(Path);

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    private sealed class RecordingNativeStartupDialog : NativeStartupDialog
    {
        public List<string> Messages { get; } = [];

        public override void ShowBlockingError(string message) => Messages.Add(message);
    }

    private sealed class RejectingSingleInstanceGuard : ISingleInstanceGuard
    {
        public ISingleInstanceLease? TryAcquire() => null;
    }

    private sealed class FailingManagedPathInitializer
    {
        public Task<int> InitializeAsync(CompositionTestOptions options, CancellationToken cancellationToken) =>
            Task.FromResult(1);
    }
}
