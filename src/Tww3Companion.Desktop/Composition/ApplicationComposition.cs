using Avalonia.Controls;
using Microsoft.Extensions.Logging;
using Tww3Companion.Application.Abstractions;
using Tww3Companion.Application.Common;
using Tww3Companion.Application.Importing;
using Tww3Companion.Application.Settings;
using Tww3Companion.Application.Startup;
using Tww3Companion.Application.Workspaces;
using Tww3Companion.Desktop.Services;
using Tww3Companion.Desktop.Startup;
using Tww3Companion.Desktop.ViewModels;
using Tww3Companion.Infrastructure.Logging;
using Tww3Companion.Infrastructure.Paths;
using Tww3Companion.Infrastructure.Settings;
using Tww3Companion.Infrastructure.Startup;
using Tww3Companion.Infrastructure.Storage;

namespace Tww3Companion.Desktop.Composition;

public sealed class ApplicationComposition
{
  private static readonly string[] ApprovedStartupSteps =
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
  ];

  private ApplicationComposition()
  {
    StartupSteps = ApprovedStartupSteps;
  }

  public IReadOnlyList<string> StartupSteps { get; }

  public static ApplicationComposition CreateForTest() => new();

  public static int RunStartupForTest(CompositionTestOptions? options = null)
  {
    options ??= new CompositionTestOptions();
    var runtime = CreateRuntimeForTest(options);
    runtime?.Dispose();
    return runtime is null ? 1 : 0;
  }

  public static ApplicationRuntime? CreateRuntimeForTest(CompositionTestOptions options) =>
      CreateRuntime(
          new CompositionOptions(
              options.ExecutableDirectory,
              options.LocalApplicationDataDirectory,
              options.NativeStartupDialog,
              options.SingleInstanceGuard,
              options.ManagedPathInitializer,
              options.WorkAreaWidth,
              options.WorkAreaHeight,
              options.WorkspaceDisposalCoordinator));

  public static ApplicationRuntime? CreateSmokeTestRuntime(ISingleInstanceGuard singleInstanceGuard) =>
      CreateRuntime(new CompositionOptions(
          AppContext.BaseDirectory,
          Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
          new NativeStartupDialog(),
          singleInstanceGuard,
          ManagedPathInitializer: null,
          WorkAreaWidth: null,
          WorkAreaHeight: null,
          WorkspaceDisposalCoordinator: null));

  public static int RunDesktopStartup(
      string[] args,
      ISingleInstanceGuard guard,
      IStartupNotification startupDialog,
      Func<ApplicationRuntime, int> startApplication)
  {
    var runtime = CreateRuntime(new CompositionOptions(
        AppContext.BaseDirectory,
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        startupDialog,
        guard,
        ManagedPathInitializer: null,
        WorkAreaWidth: null,
        WorkAreaHeight: null,
        WorkspaceDisposalCoordinator: null));
    if (runtime is null)
    {
      return 1;
    }

    using (runtime)
    {
      return startApplication(runtime);
    }
  }

  internal static ApplicationRuntime? CreateRuntime(CompositionOptions options)
  {
    var paths = DetectManagedPaths(options.ExecutableDirectory, options.LocalApplicationDataDirectory);
    var initialized = InitializeManagedPaths(options, paths);
    if (initialized is OperationResult<ManagedPaths>.Failure managedPathFailure)
    {
      options.StartupDialog.ShowBlockingError(managedPathFailure.Error.Message);
      return null;
    }

    var lease = options.SingleInstanceGuard.TryAcquire();
    if (lease is null)
    {
      options.StartupDialog.ShowBlockingError(SingleInstanceStartup.AlreadyRunningMessage);
      return null;
    }

    var loggingProvider = LoggingConfiguration.CreateProvider(paths);
    var settingsStore = new JsonApplicationSettingsStore(paths.SettingsFile);
    var settings = settingsStore.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
    var lifecycle = CreateWorkspaceLifecycle(settingsStore);
    var workspaceDisposalCoordinator = options.WorkspaceDisposalCoordinator ?? new WorkspaceDisposalCoordinator();

    TopLevel? topLevel = null;
    var workspaceDialogService = new WorkspaceDialogService(() => topLevel);
    var shell = ShellViewModel.Create(
        settings,
        settingsStore,
        workspaceDialogService,
        lifecycle.CreateWorkspace,
        lifecycle.OpenWorkspace,
        paths.WorkspacesDirectory,
        Path.GetDirectoryName(paths.SettingsFile)!,
        workspaceDisposalCoordinator,
        new EngineShellImportService(new ImportEngine(new CompositionWorkspaceImportStore())));
    if (options.WorkAreaWidth is { } width && options.WorkAreaHeight is { } height)
    {
      shell.EvaluateWorkArea(width, height);
    }

    return new ApplicationRuntime(
        shell,
        paths,
        lifecycle.CreateWorkspace,
        lifecycle.OpenWorkspace,
        () => topLevel = null,
        control => topLevel = control,
        lease,
        loggingProvider);
  }

  internal static WorkspaceLifecycle CreateWorkspaceLifecycle(IApplicationSettingsStore settingsStore)
  {
    var workspaceStore = new SqliteWorkspaceStore();
    var clock = new SystemClock();
    return new WorkspaceLifecycle(
        new CreateWorkspace(workspaceStore, settingsStore, clock, new GuidUuidGenerator()),
        new OpenWorkspace(workspaceStore, settingsStore, clock));
  }

  internal static ManagedPaths DetectManagedPaths(string executableDirectory, string localApplicationDataDirectory)
  {
    var testManagedRoot = Environment.GetEnvironmentVariable("TWW3_COMPANION_TEST_MANAGED_ROOT");
    if (Environment.GetEnvironmentVariable("TWW3_COMPANION_TEST_MODE") != "1")
    {
      return ManagedPaths.Detect(executableDirectory, localApplicationDataDirectory);
    }

    if (File.Exists(Path.Combine(executableDirectory, "portable.flag")))
    {
      return ManagedPaths.Detect(executableDirectory, localApplicationDataDirectory);
    }

    return string.IsNullOrWhiteSpace(testManagedRoot)
        ? ManagedPaths.Detect(executableDirectory, localApplicationDataDirectory)
        : ManagedPaths.ForRoot(ApplicationMode.Installed, testManagedRoot);
  }

  private static OperationResult<ManagedPaths> InitializeManagedPaths(CompositionOptions options, ManagedPaths paths)
  {
    var initializer = options.ManagedPathInitializer ?? new ManagedPathInitializer();
    return initializer.InitializeAsync(paths, CancellationToken.None).GetAwaiter().GetResult();
  }

  private sealed class SystemClock : IClock
  {
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
  }

  private sealed class GuidUuidGenerator : IUuidGenerator
  {
    public string NewUuid() => Guid.NewGuid().ToString("D").ToLowerInvariant();
  }

  internal sealed record WorkspaceLifecycle(
      CreateWorkspace CreateWorkspace,
      OpenWorkspace OpenWorkspace);

  private sealed class EngineShellImportService(IImportEngine engine) : IShellImportService
  {
    public Task<ImportPreview> BuildPreviewAsync(
        ImportTargetContext targetContext,
        IReadOnlyList<object> candidates,
        CancellationToken cancellationToken = default) =>
        engine.BuildPreviewAsync(targetContext, candidates, cancellationToken);

    public Task<ImportOutcome> ApplyAsync(
        ImportPreview preview,
        bool confirm,
        CancellationToken cancellationToken = default) =>
        engine.ApplyAsync(preview, confirm, cancellationToken);
  }

  private sealed class CompositionWorkspaceImportStore : IWorkspaceImportStore
  {
    public Task<IReadOnlyList<ImportCandidate>> ReadCandidatesAsync(
        ImportTargetContext targetContext,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ImportCandidate>>([]);

    public Task<bool> ModExistsAsync(
        ImportTargetContext.CurrentWorkspace targetContext,
        string modId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<ImportOutcome> CommitNewWorkspaceAtomicallyAsync(
        ImportPreview preview,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ImportOutcome(preview.TargetContext, preview.Candidates, Applied: preview.Applied));

    public Task<ImportPreview> SavePreviewAsync(
        ImportTargetContext targetContext,
        IReadOnlyList<ImportCandidate> candidates,
        IReadOnlyList<ImportResolution> resolutions,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ImportPreview(targetContext, candidates, Applied: false));

    public Task<ImportOutcome> CommitAtomicallyAsync(
        ImportPreview preview,
        bool confirm,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ImportOutcome(preview.TargetContext, preview.Candidates, confirm));
  }
}

public sealed class CompositionTestOptions
{
  public string ExecutableDirectory { get; init; } = AppContext.BaseDirectory;
  public string LocalApplicationDataDirectory { get; init; } = Path.GetTempPath();
  public IStartupNotification NativeStartupDialog { get; init; } = new NativeStartupDialog();
  public ISingleInstanceGuard SingleInstanceGuard { get; init; } = new AcquiringTestSingleInstanceGuard();
  public IManagedPathInitializer? ManagedPathInitializer { get; init; }
  public double? WorkAreaWidth { get; init; }
  public double? WorkAreaHeight { get; init; }
  public IWorkspaceDisposalCoordinator? WorkspaceDisposalCoordinator { get; init; }

  private sealed class AcquiringTestSingleInstanceGuard : ISingleInstanceGuard
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

internal sealed record CompositionOptions(
    string ExecutableDirectory,
    string LocalApplicationDataDirectory,
    IStartupNotification StartupDialog,
    ISingleInstanceGuard SingleInstanceGuard,
    IManagedPathInitializer? ManagedPathInitializer,
    double? WorkAreaWidth,
    double? WorkAreaHeight,
    IWorkspaceDisposalCoordinator? WorkspaceDisposalCoordinator);

public sealed class ApplicationRuntime(
    ShellViewModel shellViewModel,
    ManagedPaths managedPaths,
    CreateWorkspace createWorkspace,
    OpenWorkspace openWorkspace,
    Action clearTopLevel,
    Action<TopLevel> attachTopLevel,
    ISingleInstanceLease singleInstanceLease,
    ILoggerProvider loggingProvider) : IDisposable
{
  public ShellViewModel ShellViewModel { get; } = shellViewModel;
  public ManagedPaths ManagedPaths { get; } = managedPaths;
  public CreateWorkspace CreateWorkspace { get; } = createWorkspace;
  public OpenWorkspace OpenWorkspace { get; } = openWorkspace;

  public void AttachTopLevel(TopLevel topLevel)
  {
    attachTopLevel(topLevel);
    EvaluateAttachedWorkArea(topLevel);
  }

  public void Dispose()
  {
    clearTopLevel();
    loggingProvider.Dispose();
    singleInstanceLease.Dispose();
  }

  private void EvaluateAttachedWorkArea(TopLevel topLevel)
  {
    var primary = topLevel.Screens?.Primary;
    if (primary is null)
    {
      return;
    }

    ShellViewModel.EvaluateWorkArea(
        primary.WorkingArea.Width / primary.Scaling,
        primary.WorkingArea.Height / primary.Scaling);
  }
}
