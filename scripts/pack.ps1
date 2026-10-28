#requires -Version 7
<#
.SYNOPSIS
    Publishes OmniDeck self-contained and packages it with Velopack (vpk) for GitHub Releases.

.DESCRIPTION
    Produces, under Releases/:
      OmniDeck-win-Setup.exe, OmniDeck-win-Portable.zip, RELEASES, releases.win.json,
      *-full.nupkg, and *-delta.nupkg when the previous release was downloaded there first.
    Install and update are per-user (no UAC). Velopack installs into %LocalAppData%\OmniDeck and
    deletes that whole folder on uninstall or reinstall, which is why app data lives in
    %LocalAppData%\OmniDeck.Data (docs/ARCHITECTURE.md §5).

    vpk is pinned as a local .NET tool (.config/dotnet-tools.json) and must match the Velopack
    library version referenced by src/OmniDeck.App/OmniDeck.App.csproj.

.PARAMETER Version
    SemVer version without the leading 'v' (e.g. 0.1.0 or 0.0.1-alpha.1); matches the git tag.

.PARAMETER SkipPack
    Publish only, without vpk pack.

.EXAMPLE
    ./scripts/pack.ps1 -Version 0.0.1-alpha.1
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$')]
    [string] $Version,

    [string] $Runtime = 'win-x64',

    [string] $Configuration = 'Release',

    [switch] $SkipPack
)

$ErrorActionPreference = 'Stop'

# Velopack rejects versions below 0.0.1 (it compares the X.Y.Z part, ignoring the suffix):
# fail here, before a minute of publishing, rather than in vpk pack.
if ([version]($Version -replace '-.*$', '') -lt [version]'0.0.1') {
    throw "Velopack needs a version of at least 0.0.1 (got $Version)."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $repoRoot "publish/$Runtime"
$appProject = Join-Path $repoRoot 'src/OmniDeck.App'

# A stale publish folder would leak files from an earlier build into the package.
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

Write-Host "Publishing OmniDeck $Version ($Runtime, $Configuration)..." -ForegroundColor Cyan
# Only -p:Version: the SDK derives FileVersion (e.g. 0.0.1.0) and appends the commit SHA to
# InformationalVersion, which About shows. FileVersion=0.0.1-alpha.1 would fail the build (CS7035).
# ReadyToRun precompiles the app's own assemblies for faster startup.
dotnet publish $appProject `
    -c $Configuration -r $Runtime --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=true `
    -p:Version=$Version `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

# The notices must ship with every build (docs/legal/THIRD-PARTY-NOTICES.md), including the
# runtimes' own, copied by the OdCopyRuntimeNotices target in OmniDeck.App.csproj.
$required = 'OmniDeck.exe', 'legal/LICENSE.txt', 'legal/THIRD-PARTY-NOTICES.md',
    'legal/Microsoft.NETCore.App/LICENSE.TXT', 'legal/Microsoft.NETCore.App/THIRD-PARTY-NOTICES.TXT',
    'legal/Microsoft.WindowsDesktop.App/LICENSE'
foreach ($file in $required) {
    if (-not (Test-Path (Join-Path $publishDir $file))) {
        throw "Publish output is missing $file"
    }
}

if ($SkipPack) {
    Write-Host "Publish OK (pack skipped). Output: $publishDir" -ForegroundColor Green
    return
}

Push-Location $repoRoot
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore failed with exit code $LASTEXITCODE" }

    Write-Host "Packing with Velopack..." -ForegroundColor Cyan
    # --runtime selects the native x64 Setup.exe/Update.exe (vpk otherwise defaults to x86). Every
    # version uses the default "win" channel, so alpha -> beta -> 1.0 is an ordinary update.
    dotnet vpk pack `
        --runtime $Runtime `
        --packId OmniDeck `
        --packTitle OmniDeck `
        --packAuthors poli0981 `
        --packVersion $Version `
        --packDir $publishDir `
        --mainExe OmniDeck.exe
    if ($LASTEXITCODE -ne 0) { throw "vpk pack failed with exit code $LASTEXITCODE" }
}
finally {
    Pop-Location
}

Write-Host "Done. Artifacts are in $(Join-Path $repoRoot 'Releases')." -ForegroundColor Green
