<#
.SYNOPSIS
    Cut a new AeroVRC release: bump version, republish the standalone exe,
    commit, tag, and publish a GitHub release with the exe attached.

.EXAMPLE
    .\release.ps1 2.1.0
    .\release.ps1 2.1.0 -Notes "Fixes the stats chart flicker and trims RAM."

.NOTES
    - Commit (and push) your code changes BEFORE running this. It only bumps the
      version and tags the release; it won't sweep in unrelated edits.
    - Requires: local .NET 9 SDK, gh CLI (authenticated), git.
#>
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    # Release notes text. If omitted, GitHub auto-generates notes from commits.
    [string]$Notes,

    # Skip the self-contained rebuild (only re-tag / re-release the current exe).
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$ProjectDir = $PSScriptRoot
$Tag        = "v$Version"
$Csproj     = Join-Path $ProjectDir 'AeroVRC.csproj'
$ScExe      = Join-Path $ProjectDir 'publish\sc\AeroVRC.exe'

# Pick a dotnet that actually has an SDK (the machine-wide one may be runtime-only).
# Detect via the filesystem (an "sdk\<version>" folder next to dotnet.exe) instead
# of spawning "dotnet --list-sdks", which can report empty in some launch contexts.
function Resolve-DotnetWithSdk {
    $cands = @(
        "$env:LOCALAPPDATA\Microsoft\dotnet-sdk9\dotnet.exe",
        (Get-Command dotnet -ErrorAction SilentlyContinue).Source,
        "$env:ProgramFiles\dotnet\dotnet.exe"
    ) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -Unique
    foreach ($c in $cands) {
        $sdkDir = Join-Path (Split-Path $c -Parent) 'sdk'
        if (Test-Path $sdkDir) {
            $hasVer = Get-ChildItem -LiteralPath $sdkDir -Directory -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($hasVer) { return $c }
        }
    }
    return $null
}
$dotnet = Resolve-DotnetWithSdk
$gh = "C:\Program Files\GitHub CLI\gh.exe"
if (-not (Test-Path $gh)) { $gh = (Get-Command gh -ErrorAction SilentlyContinue).Source }
if (-not $dotnet) { throw "No .NET SDK found (only a runtime?). Install the .NET 9 SDK." }
if (-not $gh)     { throw "gh CLI not found." }

Write-Host "== Releasing AeroVRC $Tag ==" -ForegroundColor Cyan

# Guard: the tag must not already exist on the remote.
$existing = & $gh release view $Tag --repo CalamityTheWatcher/AeroVRC --json tagName 2>$null
if ($LASTEXITCODE -eq 0) { throw "Release $Tag already exists. Use a new version number." }

# 1) Bump <Version> in the csproj.
Write-Host "-> Setting <Version> to $Version"
$xml = [xml](Get-Content $Csproj)
$pg  = $xml.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1
if (-not $pg) { throw "No <Version> element found in $Csproj." }
$pg.Version = $Version
$xml.Save($Csproj)

# 2) Close any running instance so the single-file exe isn't locked.
$running = Get-Process AeroVRC -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "-> Closing $($running.Count) running AeroVRC instance(s)"
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 800
}

# 3) Republish the self-contained (zero-dependency) exe.
if (-not $NoBuild) {
    Write-Host "-> Publishing self-contained exe (this takes a moment)..."
    & $dotnet publish -c Release -r win-x64 --self-contained true `
        /p:PublishSingleFile=true /p:EnableCompressionInSingleFile=true `
        -o publish\sc --source https://api.nuget.org/v3/index.json | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "publish failed." }
}
if (-not (Test-Path $ScExe)) { throw "Expected exe not found at $ScExe" }

# 4) Commit the version bump and push main.
Write-Host "-> Committing version bump and pushing main"
& git -C $ProjectDir add AeroVRC.csproj
& git -C $ProjectDir commit -m "Release $Tag" | Out-Null
& git -C $ProjectDir push

# 5) Create the GitHub release (tags at current main HEAD) with the exe attached.
Write-Host "-> Creating GitHub release $Tag"
$asset = "$ScExe#AeroVRC.exe (standalone, no .NET needed)"
if ($Notes) {
    & $gh release create $Tag --repo CalamityTheWatcher/AeroVRC --title "AeroVRC $Tag" --latest --notes $Notes $asset
} else {
    & $gh release create $Tag --repo CalamityTheWatcher/AeroVRC --title "AeroVRC $Tag" --latest --generate-notes $asset
}
if ($LASTEXITCODE -ne 0) { throw "gh release create failed." }

Write-Host "== Done: https://github.com/CalamityTheWatcher/AeroVRC/releases/tag/$Tag ==" -ForegroundColor Green
