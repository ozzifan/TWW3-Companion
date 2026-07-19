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
    if ($null -ne $LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw "Smoke command failed with exit code $LASTEXITCODE" }

    $resultPath = Join-Path $workspace 'smoke-result.json'
    $resultDeadline = [DateTime]::UtcNow.AddSeconds(10)
    while (-not (Test-Path -LiteralPath $resultPath) -and [DateTime]::UtcNow -lt $resultDeadline) { Start-Sleep -Milliseconds 100 }
    if (-not (Test-Path -LiteralPath $resultPath)) { throw "Smoke result was not written: $resultPath" }
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

    $secondWorkspace = Join-Path $work 'second-workspace'
    & $exe --smoke-test $secondWorkspace
    if (Test-Path -LiteralPath (Join-Path $secondWorkspace 'smoke-result.json')) { throw 'Second process unexpectedly succeeded.' }
    $holder.WaitForExit(20000) | Out-Null
}
finally {
    if ($null -ne $holder -and -not $holder.HasExited) { Stop-Process -Id $holder.Id -Force }
    Remove-Item Env:TWW3_COMPANION_TEST_MODE -ErrorAction SilentlyContinue
    Remove-Item Env:TWW3_COMPANION_TEST_MANAGED_ROOT -ErrorAction SilentlyContinue
}

Write-Host "Smoke test passed for $Mode mode."
