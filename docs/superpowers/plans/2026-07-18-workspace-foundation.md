# Workspace Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first v0.1 vertical slice: a self-contained Windows desktop application that starts safely, shows Home, creates and opens an empty `.tww3c` Workspace, and enters the approved empty Workspace shell.

**Architecture:** Four production projects enforce inward dependencies: Desktop and Infrastructure depend on Application and Domain, while Domain is independent. Application defines lifecycle ports and typed results; Infrastructure implements SQLite, filesystem, settings, logging, backup, and Windows process behaviour; Desktop composes them through Avalonia MVVM.

**Tech Stack:** .NET SDK 10.0.302, C# 14, `net10.0`, Avalonia 12.1.0, Microsoft.Data.Sqlite.Core 10.0.10 with SQLitePCLRaw.bundle_winsqlite3 2.1.11, Microsoft.Extensions.Logging 10.0.10, Serilog 4.4.0, xUnit v3 3.2.2, VSTest adapter 3.1.5.

**Design:** [Workspace Foundation Design](../specs/2026-07-18-workspace-foundation-design.md)

## Global Constraints

- Target Windows 10 or later on x64; publish self-contained for `win-x64`.
- Pin SDK 10.0.302 in `global.json`; use `rollForward: latestPatch` and do not use preview SDKs.
- Pin every NuGet version centrally in `Directory.Packages.props`.
- Keep Domain free of UI, SQLite, filesystem, process, and logging dependencies.
- Keep SQLite and Serilog packages out of Domain and Application.
- Use direct parameterised SQL only; do not add Entity Framework Core or Dapper.
- Generate UUID version 4 values and store canonical lowercase hyphenated text.
- Use `.tww3c` as the Workspace extension and `com.ozzifan.tww3-companion.workspace` as the application identifier.
- Schema version 1 contains application identity, migration history, and exactly one Workspace metadata row only.
- Keep installed and portable managed data isolated; `portable.flag` selects portable mode.
- Retain five managed automatic backups per Workspace UUID.
- Do not expose Import or later destinations in this slice.
- Every behaviour change follows red-green-refactor and ends with a focused commit.
- Run commands from `E:\TWW3-Companion` unless a step states otherwise.

## File Map

```text
global.json                              pinned SDK and test runner selection
Directory.Build.props                    common compiler/build policy
Directory.Packages.props                 central NuGet versions
Tww3Companion.sln                        project graph
src/Tww3Companion.Domain/                identities and Workspace rules
src/Tww3Companion.Application/           lifecycle ports, results, use cases
src/Tww3Companion.Infrastructure/        SQLite, files, settings, logs, mutex
src/Tww3Companion.Desktop/               Avalonia composition, Views, ViewModels
tests/Tww3Companion.Domain.Tests/         Domain unit tests
tests/Tww3Companion.Application.Tests/    use-case and contract tests
tests/Tww3Companion.Infrastructure.Tests/ real-file and SQLite integration tests
tests/Tww3Companion.Desktop.Tests/        ViewModel, accessibility-state, process tests
```

---

### Task 1: Pin the toolchain and scaffold the solution

**Files:**
- Create: `global.json`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `Tww3Companion.sln`
- Create: `src/Tww3Companion.Domain/Tww3Companion.Domain.csproj`
- Create: `src/Tww3Companion.Application/Tww3Companion.Application.csproj`
- Create: `src/Tww3Companion.Infrastructure/Tww3Companion.Infrastructure.csproj`
- Create: `src/Tww3Companion.Desktop/Tww3Companion.Desktop.csproj`
- Create: four matching `tests/*/*.csproj` files
- Modify: `docs/superpowers/specs/2026-07-18-workspace-foundation-design.md`

**Interfaces:**
- Produces: the approved project dependency graph and reproducible restore surface.
- Consumes: no application interfaces.

- [ ] **Step 1: Install and verify the required SDK**

Run:

```powershell
winget install --id Microsoft.DotNet.SDK.10 --exact --source winget --accept-package-agreements --accept-source-agreements
dotnet --list-sdks
```

Expected: the list contains `10.0.302`. If winget reports it is already installed, continue only when `dotnet --list-sdks` confirms that exact feature-band patch.

- [ ] **Step 2: Create the SDK and central build files**

Write `global.json`:

```json
{
  "sdk": {
    "version": "10.0.302",
    "rollForward": "latestPatch",
    "allowPrerelease": false
  },
  "test": {
    "runner": "VSTest"
  }
}
```

Write `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>14.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
  </PropertyGroup>
</Project>
```

Write `Directory.Packages.props` with central package management enabled and these exact versions:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Avalonia" Version="12.1.0" />
    <PackageVersion Include="Avalonia.Desktop" Version="12.1.0" />
    <PackageVersion Include="Avalonia.Themes.Fluent" Version="12.1.0" />
    <PackageVersion Include="Microsoft.Data.Sqlite.Core" Version="10.0.10" />
    <PackageVersion Include="SQLitePCLRaw.bundle_winsqlite3" Version="2.1.11" />
    <PackageVersion Include="Microsoft.Extensions.Logging" Version="10.0.10" />
    <PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.10" />
    <PackageVersion Include="Serilog" Version="4.4.0" />
    <PackageVersion Include="Serilog.Extensions.Logging" Version="10.0.0" />
    <PackageVersion Include="Serilog.Sinks.File" Version="7.0.0" />
    <PackageVersion Include="xunit.v3" Version="3.2.2" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.5" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.8.1" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Scaffold projects and references**

Run:

```powershell
dotnet new sln --name Tww3Companion --format sln
dotnet new classlib -n Tww3Companion.Domain -o src/Tww3Companion.Domain
dotnet new classlib -n Tww3Companion.Application -o src/Tww3Companion.Application
dotnet new classlib -n Tww3Companion.Infrastructure -o src/Tww3Companion.Infrastructure
dotnet new install Avalonia.Templates::12.1.0
dotnet new avalonia.app -n Tww3Companion.Desktop -o src/Tww3Companion.Desktop
dotnet new classlib -n Tww3Companion.Domain.Tests -o tests/Tww3Companion.Domain.Tests
dotnet new classlib -n Tww3Companion.Application.Tests -o tests/Tww3Companion.Application.Tests
dotnet new classlib -n Tww3Companion.Infrastructure.Tests -o tests/Tww3Companion.Infrastructure.Tests
dotnet new classlib -n Tww3Companion.Desktop.Tests -o tests/Tww3Companion.Desktop.Tests
dotnet sln Tww3Companion.sln add (Get-ChildItem src,tests -Recurse -Filter *.csproj | ForEach-Object FullName)
```

Edit project references so Application references Domain; Infrastructure references Application and Domain; Desktop references Application, Domain, and Infrastructure; each test project references only its production project plus lower-level projects required by the test fixture. Remove template-level `TargetFramework`, `Nullable`, and `ImplicitUsings` properties already governed by `Directory.Build.props`.

Add exact package-reference ownership without versions:

```xml
<!-- Tww3Companion.Application -->
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />

<!-- Tww3Companion.Infrastructure -->
<PackageReference Include="Microsoft.Data.Sqlite.Core" />
<PackageReference Include="SQLitePCLRaw.bundle_winsqlite3" />
<PackageReference Include="Serilog" />
<PackageReference Include="Serilog.Extensions.Logging" />
<PackageReference Include="Serilog.Sinks.File" />

<!-- Tww3Companion.Desktop -->
<PackageReference Include="Avalonia" />
<PackageReference Include="Avalonia.Desktop" />
<PackageReference Include="Avalonia.Themes.Fluent" />
<PackageReference Include="Microsoft.Extensions.Logging" />
```

Add package references without versions:

```xml
<!-- every test project -->
<PackageReference Include="xunit.v3" />
<PackageReference Include="xunit.runner.visualstudio" PrivateAssets="all" />
<PackageReference Include="Microsoft.NET.Test.Sdk" />
```

Set every test project to `<IsTestProject>true</IsTestProject>` and delete generated `Class1.cs` files.

Create `tests/Tww3Companion.Application.Tests/Architecture/DependencyRulesTests.cs`. Locate the repository root by walking parents until `Directory.Packages.props` exists, parse production `.csproj` files with `XDocument`, and assert these exact allow-lists:

```csharp
var allowedProjectReferences = new Dictionary<string, string[]>
{
    ["Tww3Companion.Domain"] = [],
    ["Tww3Companion.Application"] = ["Tww3Companion.Domain"],
    ["Tww3Companion.Infrastructure"] = ["Tww3Companion.Application", "Tww3Companion.Domain"],
    ["Tww3Companion.Desktop"] = ["Tww3Companion.Application", "Tww3Companion.Domain", "Tww3Companion.Infrastructure"]
};

var forbiddenPackages = new Dictionary<string, string[]>
{
    ["Tww3Companion.Domain"] = ["Avalonia", "Microsoft.Data.Sqlite", "Microsoft.Extensions.Logging", "Serilog"],
    ["Tww3Companion.Application"] = ["Avalonia", "Microsoft.Data.Sqlite", "Serilog"]
};
```

Match package prefixes so `Avalonia.Desktop` and `Serilog.Sinks.File` are covered.

- [ ] **Step 4: Add the supporting runner pin to the approved spec**

Change the test dependency bullet to:

```markdown
- xUnit v3 package 3.2.2, `xunit.runner.visualstudio` 3.1.5, and `Microsoft.NET.Test.Sdk` 18.8.1 for automated tests.
```

- [ ] **Step 5: Verify the empty graph**

Run:

```powershell
dotnet restore Tww3Companion.sln
dotnet build Tww3Companion.sln --no-restore
dotnet test Tww3Companion.sln --no-build
```

Expected: restore and build succeed with zero warnings; test exits successfully with no test failures.

- [ ] **Step 6: Commit**

```powershell
git add global.json Directory.Build.props Directory.Packages.props Tww3Companion.sln src tests docs/superpowers/specs/2026-07-18-workspace-foundation-design.md
git commit -m "build: scaffold workspace foundation solution"
```

---

### Task 2: Implement Workspace domain identity and validation

**Files:**
- Create: `src/Tww3Companion.Domain/Workspaces/WorkspaceId.cs`
- Create: `src/Tww3Companion.Domain/Workspaces/WorkspaceName.cs`
- Create: `src/Tww3Companion.Domain/Workspaces/Workspace.cs`
- Create: `src/Tww3Companion.Domain/Validation/ValidationError.cs`
- Create: `tests/Tww3Companion.Domain.Tests/Workspaces/WorkspaceIdTests.cs`
- Create: `tests/Tww3Companion.Domain.Tests/Workspaces/WorkspaceNameTests.cs`
- Create: `tests/Tww3Companion.Domain.Tests/Workspaces/WorkspaceTests.cs`

**Interfaces:**
- Produces: `WorkspaceId.Parse(string)`, `WorkspaceId.New()`, `WorkspaceName.Create(string)`, and immutable `Workspace` metadata.
- Consumes: no external services.

- [ ] **Step 1: Write failing value-object tests**

Cover canonical lowercase UUID text, invalid UUID rejection, trimmed non-empty names, a 200-Unicode-scalar maximum, and a 201-scalar rejection. Use explicit examples:

```csharp
[Fact]
public void Parse_UppercaseUuid_ReturnsCanonicalLowercaseText()
{
    var id = WorkspaceId.Parse("6F9619FF-8B86-D011-B42D-00C04FC964FF");
    Assert.Equal("6f9619ff-8b86-d011-b42d-00c04fc964ff", id.ToString());
}

[Theory]
[InlineData("")]
[InlineData("   ")]
public void Create_BlankName_ReturnsValidationError(string value)
{
    var result = WorkspaceName.Create(value);
    Assert.False(result.IsSuccess);
    Assert.Equal("workspace.name.required", result.Error.Code);
}
```

- [ ] **Step 2: Run tests and confirm red**

Run `dotnet test tests/Tww3Companion.Domain.Tests/Tww3Companion.Domain.Tests.csproj`.

Expected: compilation fails because the Workspace types do not exist.

- [ ] **Step 3: Implement the minimal immutable types**

Use readonly record structs for `WorkspaceId` and `WorkspaceName`. Count Unicode scalars with `value.EnumerateRunes().Count()`. Use `Guid.TryParseExact(value, "D", out var guid)` and format with `guid.ToString("D").ToLowerInvariant()`.

Define `Workspace` exactly as:

```csharp
public sealed record Workspace(
    WorkspaceId Id,
    WorkspaceName Name,
    DateTimeOffset CreatedUtc,
    DateTimeOffset ModifiedUtc);
```

Reject `ModifiedUtc < CreatedUtc` through `Workspace.Create` with code `workspace.modified.before-created`.

- [ ] **Step 4: Run tests and confirm green**

Run `dotnet test tests/Tww3Companion.Domain.Tests/Tww3Companion.Domain.Tests.csproj`.

Expected: all Domain tests pass.

- [ ] **Step 5: Commit**

```powershell
git add src/Tww3Companion.Domain tests/Tww3Companion.Domain.Tests
git commit -m "feat: add workspace domain identity"
```

---

### Task 3: Define Application lifecycle ports and typed results

**Files:**
- Create: `src/Tww3Companion.Application/Common/OperationError.cs`
- Create: `src/Tww3Companion.Application/Common/OperationResult.cs`
- Create: `src/Tww3Companion.Application/Abstractions/IClock.cs`
- Create: `src/Tww3Companion.Application/Abstractions/IUuidGenerator.cs`
- Create: `src/Tww3Companion.Application/Workspaces/IWorkspaceStore.cs`
- Create: `src/Tww3Companion.Application/Workspaces/CreateWorkspace.cs`
- Create: `src/Tww3Companion.Application/Workspaces/OpenWorkspace.cs`
- Create: `src/Tww3Companion.Application/Settings/IApplicationSettingsStore.cs`
- Create: `src/Tww3Companion.Application/Settings/ApplicationSettings.cs`
- Create: `tests/Tww3Companion.Application.Tests/Workspaces/CreateWorkspaceTests.cs`
- Create: `tests/Tww3Companion.Application.Tests/Workspaces/OpenWorkspaceTests.cs`

**Interfaces:**
- Produces: `IWorkspaceStore.CreateAsync`, `IWorkspaceStore.OpenAsync`, `CreateWorkspace.ExecuteAsync`, and `OpenWorkspace.ExecuteAsync`.
- Consumes: Domain Workspace types; clock, UUID, storage, and settings ports.

- [ ] **Step 1: Write failing use-case tests with hand-written fakes**

Verify blank names fail before storage, existing targets return `workspace.target.exists`, failed opens do not add recents, and successful create/open adds a recent path only after storage succeeds.

Use these signatures:

```csharp
public interface IWorkspaceStore
{
    Task<OperationResult<Workspace>> CreateAsync(
        string path,
        Workspace workspace,
        CancellationToken cancellationToken);

    Task<OperationResult<Workspace>> OpenAsync(
        string path,
        CancellationToken cancellationToken);
}

public sealed record OperationError(
    string Code,
    string Message,
    bool PersistentChangeCommitted,
    string SafeNextAction);
```

- [ ] **Step 2: Run tests and confirm red**

Run `dotnet test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj`.

Expected: compilation fails for missing Application contracts.

- [ ] **Step 3: Implement contracts and use cases**

`CreateWorkspace.ExecuteAsync(string displayName, string path, CancellationToken)` validates Domain values, creates UUID/time values, calls the store once, and records the recent path only on success. `OpenWorkspace.ExecuteAsync` calls the store then records recents only on success.

Use immutable settings:

```csharp
public sealed record RecentWorkspace(string Path, DateTimeOffset LastOpenedUtc);

public sealed record ApplicationSettings(
    int SchemaVersion,
    string Theme,
    WindowPlacement? WindowPlacement,
    IReadOnlyList<RecentWorkspace> RecentWorkspaces);

public sealed record WindowPlacement(
    double X,
    double Y,
    double Width,
    double Height,
    bool IsMaximized);
```

Store at most 10 recents, de-duplicate paths with `StringComparer.OrdinalIgnoreCase`, and order newest first.

- [ ] **Step 4: Run Application tests and full build**

Run:

```powershell
dotnet test tests/Tww3Companion.Application.Tests/Tww3Companion.Application.Tests.csproj
dotnet build Tww3Companion.sln
```

Expected: all tests pass and build has zero warnings.

- [ ] **Step 5: Commit**

```powershell
git add src/Tww3Companion.Application tests/Tww3Companion.Application.Tests
git commit -m "feat: define workspace lifecycle application ports"
```

---

### Task 4: Implement application mode, managed paths, settings, and logging

**Files:**
- Create: `src/Tww3Companion.Infrastructure/Paths/ApplicationMode.cs`
- Create: `src/Tww3Companion.Infrastructure/Paths/ManagedPaths.cs`
- Create: `src/Tww3Companion.Infrastructure/Paths/ManagedPathInitializer.cs`
- Create: `src/Tww3Companion.Infrastructure/Settings/JsonApplicationSettingsStore.cs`
- Create: `src/Tww3Companion.Infrastructure/Logging/LoggingConfiguration.cs`
- Create: `tests/Tww3Companion.Infrastructure.Tests/Paths/ManagedPathInitializerTests.cs`
- Create: `tests/Tww3Companion.Infrastructure.Tests/Settings/JsonApplicationSettingsStoreTests.cs`
- Create: `tests/Tww3Companion.Infrastructure.Tests/Logging/LoggingConfigurationTests.cs`
- Create: `tests/Tww3Companion.Infrastructure.Tests/Support/TemporaryDirectory.cs`

**Interfaces:**
- Produces: `ManagedPaths.Detect(executableDirectory, localAppData)`, `ManagedPathInitializer.InitializeAsync`, and `JsonApplicationSettingsStore`.
- Consumes: `IApplicationSettingsStore` and Microsoft logging abstractions.

- [ ] **Step 1: Write failing path and settings tests**

Test that `portable.flag` selects `Data\` paths; installed mode selects `%LOCALAPPDATA%\TWW3 Companion`; missing directories are created; a read-only/failing filesystem returns `startup.managed-path.unwritable`; invalid JSON remains unchanged; successful preservation uses `settings.invalid.yyyyMMddTHHmmssfffZ.json`; and failed preservation returns an unsaved result without replacing the original.

Abstract destructive filesystem calls behind this narrow interface so failures are deterministic:

```csharp
public interface IAtomicFileSystem
{
    Task WriteAllTextAtomicallyAsync(string path, string content, CancellationToken token);
    void MoveWithoutOverwrite(string source, string destination);
    Stream CreateWriteProbe(string directory);
}
```

- [ ] **Step 2: Run Infrastructure tests and confirm red**

Expected: compilation fails for missing path/settings classes.

- [ ] **Step 3: Implement managed paths and settings**

Use `portable.flag`, create `Data`, `Backups`, `Logs`, and `Workspaces`, and remove every write probe in `finally`. Serialize settings with `System.Text.Json` using camelCase and indented output. Keep invalid settings in place until a later write preserves it successfully.

- [ ] **Step 4: Configure bounded privacy-aware logging**

Configure Serilog through `SerilogLoggerProvider` with:

```csharp
.WriteTo.File(
    path: Path.Combine(paths.LogsDirectory, "tww3-companion-.log"),
    rollingInterval: RollingInterval.Day,
    fileSizeLimitBytes: 10 * 1024 * 1024,
    rollOnFileSizeLimit: true,
    retainedFileCountLimit: 7,
    shared: false)
```

Do not add enrichers that capture usernames, machine names, command lines, or full paths. Test that log messages use a per-session opaque path identifier supplied by the caller and do not contain a seeded secret filename or display name.

- [ ] **Step 5: Run focused and full tests**

Run:

```powershell
dotnet test tests/Tww3Companion.Infrastructure.Tests/Tww3Companion.Infrastructure.Tests.csproj
dotnet test Tww3Companion.sln
```

Expected: all tests pass.

- [ ] **Step 6: Commit**

```powershell
git add src/Tww3Companion.Infrastructure tests/Tww3Companion.Infrastructure.Tests
git commit -m "feat: add managed settings and logging infrastructure"
```

---

### Task 5: Implement SQLite schema version 1 and Workspace create/open

**Files:**
- Create: `src/Tww3Companion.Infrastructure/Storage/SqliteConnectionFactory.cs`
- Create: `src/Tww3Companion.Infrastructure/Storage/Schema/SchemaVersion.cs`
- Create: `src/Tww3Companion.Infrastructure/Storage/Schema/SchemaV1.cs`
- Create: `src/Tww3Companion.Infrastructure/Storage/SqliteWorkspaceStore.cs`
- Create: `src/Tww3Companion.Infrastructure/Storage/WorkspaceFileValidator.cs`
- Create: `tests/Tww3Companion.Infrastructure.Tests/Storage/SqliteConnectionFactoryTests.cs`
- Create: `tests/Tww3Companion.Infrastructure.Tests/Storage/SqliteWorkspaceStoreTests.cs`
- Create: `tests/Tww3Companion.Infrastructure.Tests/Storage/WorkspaceFileValidatorTests.cs`

**Interfaces:**
- Produces: Infrastructure implementation of `IWorkspaceStore`.
- Consumes: Domain Workspace, Application result contracts, `IAtomicFileSystem`.

- [ ] **Step 1: Write failing SQLite integration tests**

Verify the Windows SQLite provider is initialized before the first connection; every opened connection returns `1` for `PRAGMA foreign_keys`; schema v1 contains only `application_metadata`, `schema_migrations`, and `workspace`; create/open round-trips identity and timestamps; existing targets are untouched; unrelated SQLite, corrupt, duplicate-row, invalid UUID, blank-name, and newer-schema files return distinct codes.

- [ ] **Step 2: Run tests and confirm red**

Run `dotnet test tests/Tww3Companion.Infrastructure.Tests/Tww3Companion.Infrastructure.Tests.csproj --filter Storage`.

Expected: compilation fails for missing storage classes.

- [ ] **Step 3: Implement the connection factory and exact schema**

Initialize the bundled provider once through `SQLitePCL.Batteries_V2.Init()` before opening any connection. This selects Windows 10-or-later's serviced native `winsqlite3.dll`; users do not install a separate SQLite runtime. Open connections through one factory and execute `PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;` before returning them.

Use this schema inside one transaction:

```sql
CREATE TABLE application_metadata (
    singleton INTEGER PRIMARY KEY CHECK (singleton = 1),
    application_id TEXT NOT NULL CHECK (application_id = 'com.ozzifan.tww3-companion.workspace'),
    schema_version INTEGER NOT NULL CHECK (schema_version >= 1)
);

CREATE TABLE schema_migrations (
    version INTEGER PRIMARY KEY CHECK (version >= 1),
    applied_utc TEXT NOT NULL
);

CREATE TABLE workspace (
    singleton INTEGER PRIMARY KEY CHECK (singleton = 1),
    id TEXT NOT NULL UNIQUE,
    display_name TEXT NOT NULL CHECK (length(trim(display_name)) > 0),
    created_utc TEXT NOT NULL,
    modified_utc TEXT NOT NULL,
    CHECK (modified_utc >= created_utc)
);
```

Insert singleton metadata, migration 1, and Workspace in the same transaction.

Persist timestamps with `DateTimeOffset.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)` and require `TimeSpan.Zero` when loading. This keeps the textual timestamp comparison valid and rejects non-UTC stored values.

- [ ] **Step 4: Implement atomic create and validated open**

Create `<final-name>.<operation-uuid>.tmp` beside the destination, close all handles, validate through the normal open path, then call `MoveWithoutOverwrite`. On failure, remove only the operation-owned temporary file. Never infer display name from filename.

Map SQLite errors to stable codes: `workspace.file.invalid`, `workspace.file.corrupt`, `workspace.file.locked`, `workspace.schema.newer`, `workspace.identity.invalid`, and `workspace.access.denied`.

- [ ] **Step 5: Run storage and full tests**

Expected: all storage tests and the full solution pass with zero warnings.

- [ ] **Step 6: Commit**

```powershell
git add src/Tww3Companion.Infrastructure/Storage tests/Tww3Companion.Infrastructure.Tests/Storage
git commit -m "feat: create and open sqlite workspaces"
```

---

### Task 6: Add transactional migrations and five-backup retention

**Files:**
- Create: `src/Tww3Companion.Infrastructure/Storage/Migrations/IMigration.cs`
- Create: `src/Tww3Companion.Infrastructure/Storage/Migrations/MigrationRunner.cs`
- Create: `src/Tww3Companion.Infrastructure/Storage/Backups/WorkspaceBackupService.cs`
- Create: `src/Tww3Companion.Infrastructure/Storage/Backups/BackupReason.cs`
- Create: `tests/Tww3Companion.Infrastructure.Tests/Storage/MigrationRunnerTests.cs`
- Create: `tests/Tww3Companion.Infrastructure.Tests/Storage/WorkspaceBackupServiceTests.cs`
- Create: `tests/Tww3Companion.Infrastructure.Tests/Storage/Fixtures/SchemaVersionZeroFixture.cs`

**Interfaces:**
- Produces: `MigrationRunner.MigrateAsync(path, targetVersion, token)` and `WorkspaceBackupService.CreateAsync`.
- Consumes: connection factory, managed paths, clock, and atomic filesystem.

- [ ] **Step 1: Write failing migration and retention tests**

Use only a test-supplied version-zero migration. Verify SQLite-safe backup occurs before mutation, success records the migration, failure rolls back, the original and backup remain usable, cleanup happens after success, only five attributable backups remain, and unrelated files are never removed.

- [ ] **Step 2: Run focused tests and confirm red**

Expected: compilation fails for migration/backup types.

- [ ] **Step 3: Implement ordered migrations**

Define:

```csharp
public interface IMigration
{
    int FromVersion { get; }
    int ToVersion { get; }
    Task ApplyAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken token);
}
```

Reject gaps, duplicates, downgrade requests, and target versions newer than the application. Run the selected chain inside one transaction and validate before commit.

- [ ] **Step 4: Implement SQLite-safe backup and cleanup**

Use `SqliteConnection.BackupDatabase`, not filesystem copy. Store backups below `Backups/<workspace-uuid>/yyyyMMddTHHmmssfffZ.<reason>.tww3c`. Enumerate only filenames matching the exact UUID directory and accepted reason pattern. Delete oldest excess backups only after migration succeeds.

- [ ] **Step 5: Run focused and full tests**

Expected: backup, migration, rollback, retention, and all prior tests pass.

- [ ] **Step 6: Commit**

```powershell
git add src/Tww3Companion.Infrastructure/Storage tests/Tww3Companion.Infrastructure.Tests/Storage
git commit -m "feat: add workspace migrations and managed backups"
```

---

### Task 7: Enforce the Windows user single-instance lease

**Files:**
- Create: `src/Tww3Companion.Application/Startup/ISingleInstanceGuard.cs`
- Create: `src/Tww3Companion.Application/Startup/ISingleInstanceLease.cs`
- Create: `src/Tww3Companion.Infrastructure/Startup/WindowsSingleInstanceLease.cs`
- Create: `tests/Tww3Companion.Infrastructure.Tests/Startup/WindowsSingleInstanceLeaseTests.cs`
- Create: `tests/Tww3Companion.Desktop.Tests/Support/SingleInstanceProbe.cs`

**Interfaces:**
- Produces: `ISingleInstanceGuard.TryAcquire()` returning an owned disposable lease or `null`.
- Consumes: current Windows SID provider.

- [ ] **Step 1: Write failing same-process and child-process tests**

Verify the first lease succeeds, the second lease for the same SID fails, disposal permits reacquisition, installed/portable mode does not alter the mutex name, and a child process exits before touching a sentinel settings file.

- [ ] **Step 2: Run tests and confirm red**

Expected: compilation fails for missing lease types.

- [ ] **Step 3: Implement the named mutex**

Use `WindowsIdentity.GetCurrent().User!.Value` and mutex name `Local\TWW3Companion.SingleInstance.<sid>`. Treat `AbandonedMutexException` as successful ownership. Dispose releases the mutex only when the current instance owns it.

Use these interfaces:

```csharp
public interface ISingleInstanceGuard
{
    ISingleInstanceLease? TryAcquire();
}

public interface ISingleInstanceLease : IDisposable;
```

- [ ] **Step 4: Run tests**

Expected: unit and child-process tests pass on Windows; non-Windows execution is skipped with an explicit platform reason.

- [ ] **Step 5: Commit**

```powershell
git add src/Tww3Companion.Application/Startup src/Tww3Companion.Infrastructure/Startup tests
git commit -m "feat: enforce single application instance"
```

---

### Task 8: Build and verify the minimum-size Avalonia shell first

**Files:**
- Create: `src/Tww3Companion.Desktop/ViewModels/ViewModelBase.cs`
- Create: `src/Tww3Companion.Desktop/ViewModels/ShellViewModel.cs`
- Create: `src/Tww3Companion.Desktop/Views/MainWindow.axaml`
- Modify: `src/Tww3Companion.Desktop/Views/MainWindow.axaml.cs`
- Modify: `src/Tww3Companion.Desktop/App.axaml`
- Modify: `src/Tww3Companion.Desktop/App.axaml.cs`
- Create: `src/Tww3Companion.Desktop/Views/CompatibilityView.axaml`
- Create: `src/Tww3Companion.Desktop/Services/WindowPlacementService.cs`
- Create: `tests/Tww3Companion.Desktop.Tests/ViewModels/ShellViewModelTests.cs`
- Create: `tests/Tww3Companion.Desktop.Tests/Views/MainWindowLayoutTests.cs`
- Create: `tests/Tww3Companion.Desktop.Tests/Services/WindowPlacementServiceTests.cs`

**Interfaces:**
- Produces: fixed three-region shell, compatibility state, theme state, and accessible empty state.
- Consumes: no Workspace lifecycle yet; uses immutable shell state.

- [ ] **Step 1: Write failing shell-state tests**

Verify Home is initial, Workspace state exposes only Mod Library and empty Collections, no Import/search/Profile/health destination exists, System is default theme, High Contrast overrides but does not replace the stored choice, undersized work area produces Exit/Continue Anyway state, and off-screen or invalid saved placement falls back to a fully visible 1280×800 window on the primary work area.

- [ ] **Step 2: Run Desktop tests and confirm red**

Expected: compilation fails for missing shell types.

- [ ] **Step 3: Implement the minimum shell**

Set `Width="1280"`, `Height="800"`, `MinWidth="1024"`, and `MinHeight="640"`. Use standard Avalonia controls: a fixed-width sidebar column, a flexible master column, and a detail column. Use `GridSplitter` only if it cannot violate minimum readable widths. Empty copy is exactly: `This Workspace contains no Mods or Collections yet. No data has been added.`

Implement visible focus, accessible names, logical tab order, System/Light/Dark switching, and High Contrast precedence without custom-drawn interactive controls.

`WindowPlacementService.Restore(saved, workAreas)` accepts placement only when width and height meet the logical minimum and the rectangle intersects one current work area enough to keep the title bar and primary content reachable. Otherwise it returns the default placement centered and clamped to the primary work area.

- [ ] **Step 4: Perform the mandatory minimum-size checkpoint**

Run the Desktop app at 1024×640 logical pixels with Windows text scaling at 100%, 125%, and 150%. Capture `screenshots/2026-07-18-shell-minimum-100.png`, `...-125.png`, and `...-150.png` only if they contain no personal data.

Expected: at supported effective work areas, three regions, primary actions, focus indicators, and text remain reachable without overlap. If they do not, stop this plan and return to RFC-0005 rather than changing the fixed-layout contract silently.

- [ ] **Step 5: Run tests and commit**

```powershell
dotnet test tests/Tww3Companion.Desktop.Tests/Tww3Companion.Desktop.Tests.csproj
git add src/Tww3Companion.Desktop tests/Tww3Companion.Desktop.Tests screenshots
git commit -m "feat: add accessible workspace shell"
```

---

### Task 9: Integrate Home, Create/Open, recents, settings failure, and finalizing state

**Files:**
- Create: `src/Tww3Companion.Desktop/ViewModels/HomeViewModel.cs`
- Create: `src/Tww3Companion.Desktop/ViewModels/RecentWorkspaceViewModel.cs`
- Create: `src/Tww3Companion.Desktop/ViewModels/OperationStatusViewModel.cs`
- Create: `src/Tww3Companion.Desktop/Views/HomeView.axaml`
- Create: `src/Tww3Companion.Desktop/Services/IWorkspaceDialogService.cs`
- Create: `src/Tww3Companion.Desktop/Services/WorkspaceDialogService.cs`
- Create: `src/Tww3Companion.Desktop/Composition/ApplicationComposition.cs`
- Create: `src/Tww3Companion.Desktop/Startup/SmokeTestCommand.cs`
- Create: `src/Tww3Companion.Desktop/Startup/NativeStartupDialog.cs`
- Create: `tests/Tww3Companion.Desktop.Tests/ViewModels/HomeViewModelTests.cs`
- Create: `tests/Tww3Companion.Desktop.Tests/ViewModels/OperationStatusViewModelTests.cs`
- Create: `tests/Tww3Companion.Desktop.Tests/Composition/ApplicationCompositionTests.cs`

**Interfaces:**
- Produces: complete foundation Home flow and composition root.
- Consumes: Create/Open use cases, settings store, logging, managed paths, single-instance lease, and shell ViewModel.

- [ ] **Step 1: Write failing Home tests**

Verify required display name, `.tww3c` filter/default extension, no silent overwrite, busy state prevents duplicate commands, cancellation is available pre-commit, Finalizing disables cancellation and shows `Finalizing — please wait`, failures state whether data changed, successful create/open navigates to shell, failures stay Home, and only success updates recents.

Also verify missing recents remain visible and removable; settings write failure retains in-memory state and exposes Retry/Open Settings Folder; and Home displays the synchronized-folder warning plus safe closed-file copy guidance.

- [ ] **Step 2: Run tests and confirm red**

Expected: compilation fails for Home types.

- [ ] **Step 3: Implement ViewModels and dialogs**

Expose `ICommand` properties `CreateWorkspaceCommand`, `OpenWorkspaceCommand`, `RemoveRecentCommand`, `RetrySettingsSaveCommand`, and `OpenSettingsFolderCommand`. Keep dialog and file-picker calls behind `IWorkspaceDialogService` so ViewModel tests remain native-window-free.

Do not add Import. Add a code comment on the Home navigation model referencing RFC-0005 and the next import slice so its absence is not mistaken for final scope.

- [ ] **Step 4: Compose startup in the required order**

Composition order is exact:

```text
detect application mode
→ initialize and probe managed paths
→ acquire single-instance lease
→ configure logging
→ load settings
→ construct Infrastructure adapters
→ construct Application use cases
→ construct ViewModels and Views
→ show compatibility screen or Home
```

If managed paths fail, show a native blocking error and exit. If lease acquisition fails, show `TWW3 Companion is already running for this Windows user. Close the existing installed or portable copy and try again.` and exit before settings access. `NativeStartupDialog` wraps Win32 `MessageBoxW` behind an interface so these pre-Avalonia paths are unit tested without displaying dialogs.

Add test hooks that refuse to run unless `TWW3_COMPANION_TEST_MODE=1`:

- `--smoke-test <directory>` uses the same composition root to create `Smoke Workspace` at `<directory>\smoke.tww3c`, closes it, reopens it, and writes `<directory>\smoke-result.json` containing `workspaceId`, `displayName`, `applicationMode`, and `managedRoot`. It returns exit code 0 only when the reopened UUID and display name match.
- `--hold-single-instance <milliseconds>` acquires the normal single-instance lease, writes `lease-acquired.signal` beneath `TWW3_COMPANION_TEST_MANAGED_ROOT`, holds for the requested duration, then exits.

In test mode only, `TWW3_COMPANION_TEST_MANAGED_ROOT` replaces the installed `%LOCALAPPDATA%\TWW3 Companion` root. Neither hook may bypass lifecycle use cases or call SQLite directly.

- [ ] **Step 5: Run focused and full tests**

Expected: Home, composition, all prior tests, build, and formatting pass.

- [ ] **Step 6: Commit**

```powershell
git add src/Tww3Companion.Desktop tests/Tww3Companion.Desktop.Tests
git commit -m "feat: connect workspace lifecycle to home"
```

---

### Task 10: Publish, smoke-test, and document the completed slice

**Files:**
- Create: `scripts/smoke-test-portable.ps1`
- Create: `.github/workflows/ci.yml`
- Create: `docs/development.md`
- Modify: `README.md`
- Modify: `ROADMAP.md`
- Modify: `CHANGELOG.md`
- Modify: `docs/project-history.md`

**Interfaces:**
- Produces: reproducible verification and self-contained portable artifact.
- Consumes: the completed solution.

- [ ] **Step 1: Write the smoke script before publishing**

The script accepts a publish directory and mode, copies the artifact into its operation-owned working directory, creates `portable.flag` only for Portable mode, runs the approved test hooks, and verifies all managed writes remain beneath the expected managed root plus its temporary Workspace directory. It leaves the operation-owned directory for inspection; callers may remove that exact directory after the run.

Write this script body:

```powershell
param(
    [Parameter(Mandatory)] [string] $PublishDirectory,
    [Parameter(Mandatory)] [string] $WorkingDirectory,
    [Parameter(Mandatory)] [ValidateSet('Installed', 'Portable')] [string] $Mode
)

$ErrorActionPreference = 'Stop'
$publish = (Resolve-Path -LiteralPath $PublishDirectory).Path
$work = [System.IO.Path]::GetFullPath($WorkingDirectory)
if (Test-Path -LiteralPath $work) { throw "Working directory already exists: $work" }

$app = Join-Path $work 'app'
$workspace = Join-Path $work 'workspace'
$managed = Join-Path $work 'managed'
New-Item -ItemType Directory -Path $app, $workspace, $managed | Out-Null
Copy-Item -Path (Join-Path $publish '*') -Destination $app -Recurse

if ($Mode -eq 'Portable') {
    New-Item -ItemType File -Path (Join-Path $app 'portable.flag') | Out-Null
}

$exe = Join-Path $app 'Tww3Companion.Desktop.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw "Missing executable: $exe" }

$env:TWW3_COMPANION_TEST_MODE = '1'
$env:TWW3_COMPANION_TEST_MANAGED_ROOT = $managed
$holder = $null
try {
    & $exe --smoke-test $workspace
    if ($LASTEXITCODE -ne 0) { throw "Smoke command failed with exit code $LASTEXITCODE" }

    $resultPath = Join-Path $workspace 'smoke-result.json'
    $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    if ($result.displayName -ne 'Smoke Workspace') { throw 'Display name round-trip failed.' }
    $parsedGuid = [guid]::Empty
    if (-not [guid]::TryParse([string]$result.workspaceId, [ref]$parsedGuid)) { throw 'Workspace UUID is invalid.' }
    if ($result.applicationMode -ne $Mode) { throw 'Application mode mismatch.' }

    $expectedManaged = if ($Mode -eq 'Portable') { Join-Path $app 'Data' } else { $managed }
    if ([System.IO.Path]::GetFullPath($result.managedRoot) -ne [System.IO.Path]::GetFullPath($expectedManaged)) {
        throw 'Managed root escaped the expected mode-specific directory.'
    }
    foreach ($directory in @('Backups', 'Logs', 'Workspaces')) {
        if (-not (Test-Path -LiteralPath (Join-Path $expectedManaged $directory))) { throw "Missing managed directory: $directory" }
    }

    $holder = Start-Process -FilePath $exe -ArgumentList '--hold-single-instance','15000' -PassThru -WindowStyle Hidden
    $signal = Join-Path $managed 'lease-acquired.signal'
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    while (-not (Test-Path -LiteralPath $signal) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 100 }
    if (-not (Test-Path -LiteralPath $signal)) { throw 'Lease holder did not start.' }

    & $exe --smoke-test (Join-Path $work 'second-workspace')
    if ($LASTEXITCODE -eq 0) { throw 'Second process unexpectedly succeeded.' }
    $holder.WaitForExit(20000) | Out-Null
}
finally {
    if ($null -ne $holder -and -not $holder.HasExited) { Stop-Process -Id $holder.Id -Force }
    Remove-Item Env:TWW3_COMPANION_TEST_MODE -ErrorAction SilentlyContinue
    Remove-Item Env:TWW3_COMPANION_TEST_MANAGED_ROOT -ErrorAction SilentlyContinue
}

Write-Host "Smoke test passed for $Mode mode."
```

Exit non-zero on missing executable, missing portable directories, identity mismatch, unexpected managed writes, or second-process success.

- [ ] **Step 2: Publish self-contained Windows x64**

Run:

```powershell
dotnet publish src/Tww3Companion.Desktop/Tww3Companion.Desktop.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o artifacts/portable/win-x64
```

Expected: publish succeeds and the artifact does not require a separately installed .NET runtime or SQLite tool.

- [ ] **Step 3: Run complete verification**

```powershell
dotnet format Tww3Companion.sln --verify-no-changes
dotnet build Tww3Companion.sln -c Release
dotnet test Tww3Companion.sln -c Release --no-build
& scripts/smoke-test-portable.ps1 -PublishDirectory artifacts/portable/win-x64 -WorkingDirectory artifacts/smoke-installed -Mode Installed
& scripts/smoke-test-portable.ps1 -PublishDirectory artifacts/portable/win-x64 -WorkingDirectory artifacts/smoke-portable -Mode Portable
git diff --check
```

Expected: every command exits 0; tests report zero failures; smoke test reports Workspace identity round-trip, settings isolation, and second-process rejection.

Create `.github/workflows/ci.yml` for `pull_request` and pushes to `main` on `windows-2025`. Use `actions/checkout`, `actions/setup-dotnet` with `dotnet-version: 10.0.302`, then run restore, format verification, Release build, Release tests, publish, and both smoke modes with the exact commands above. Upload no Workspace, settings, or log artifacts by default.

- [ ] **Step 4: Document exact developer and verification commands**

`docs/development.md` records SDK installation, restore, format, build, test, run, publish, and smoke commands; installed/portable paths; `.tww3c` meaning; safe manual copy guidance; and the known one-instance-per-user limitation.

README links to the development guide without claiming import is implemented. ROADMAP marks only the Workspace foundation slice complete while v0.1 remains in progress. CHANGELOG records the foundation behaviour under Unreleased. Project history records the first application-code milestone.

- [ ] **Step 5: Inspect repository scope and commit**

```powershell
git status --short
git diff --check
git add scripts/smoke-test-portable.ps1 .github/workflows/ci.yml docs/development.md README.md ROADMAP.md CHANGELOG.md docs/project-history.md
git commit -m "docs: complete workspace foundation slice"
```

Expected: only intentional source, test, script, screenshot, and documentation files are committed; `bin`, `obj`, `TestResults`, and `artifacts` remain ignored.

## Final Review Checklist

- [ ] Domain has no project or package dependency on Application, Infrastructure, Desktop, Avalonia, SQLite, or logging.
- [ ] Application has no dependency on Infrastructure, Desktop, Avalonia, SQLite, or Serilog.
- [ ] Every SQLite connection originates from `SqliteConnectionFactory` and reports foreign keys enabled.
- [ ] Schema v1 has only the three approved tables.
- [ ] Create never overwrites and failed operations remove only operation-owned temporary files.
- [ ] Open rejects unrelated, corrupt, invalid, locked, inaccessible, and newer-schema files without modification.
- [ ] Backup uses SQLite backup APIs, rollback preserves originals, and retention never touches unrelated files.
- [ ] Installed and portable managed directories are isolated and writable before user data opens.
- [ ] Logs are bounded and contain no seeded private values.
- [ ] Home excludes Import but contains the explicit sequencing comment and user-facing sync warning.
- [ ] Minimum-size, keyboard, focus, theme, High Contrast, and Narrator checks have evidence.
- [ ] Self-contained `win-x64` smoke test passes on a clean supported Windows environment.
- [ ] All commands in `docs/development.md` were rerun from a clean checkout or clean worktree.
