# =============================================================================
# Install / update local Rust test servers (SteamCMD + Carbon/Oxide), then export
# the managed-assembly refs the plugin build compiles against. Windows only.
#
#   .\install.ps1                              # all four instances
#   .\install.ps1 -Mod Carbon                  # both carbon instances
#   .\install.ps1 -Mod Carbon -Branch Release  # just carbon-release
#   .\install.ps1 -Mod Oxide -Branch Staging -Force   # force a full re-validate
#
# -Branch: All (default) | Staging | Release | Debug   (Release = release branch)
# -Mod:    All (default) | Oxide   | Carbon
#
# 'staging' instances use the Rust staging game branch (-beta staging) plus the
# matching mod staging build; 'release' uses the public branch + production mod.
# 'Debug' is a carbon-only instance (carbon-debug): public game branch + your
# LOCAL Carbon build (CarbonLocalBuildPath) with the Mono debugger enabled, for
# attaching Visual Studio to step Carbon itself. See redeploy.ps1 for the loop.
# Each instance is a full standalone install under .\servers\rust-<mod>-<branch>\.
# =============================================================================
[CmdletBinding()]
param(
    [ValidateSet('All', 'Staging', 'Release', 'Debug')][string]$Branch = 'All',
    [ValidateSet('All', 'Oxide', 'Carbon')][string]$Mod = 'All',
    [switch]$Force
)
. "$PSScriptRoot\_common.ps1"
$cfg = Import-LocalConfig

# SteamCMD is small and branch-agnostic; share one copy for every instance.
$steamRoot = Join-Path $PSScriptRoot '.steamcmd'
$steamExe  = Join-Path $steamRoot 'steamcmd.exe'
if (-not (Test-Path $steamExe)) {
    Write-Host "Installing SteamCMD..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Force -Path $steamRoot | Out-Null
    $zip = Join-Path $steamRoot 'steamcmd.zip'
    Invoke-WebRequest 'https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip' -OutFile $zip
    Expand-Archive -Path $zip -DestinationPath $steamRoot -Force
    Remove-Item $zip -Force
}

function Get-CarbonUrl {
    param([string]$BranchKey)   # release | staging
    if ($BranchKey -eq 'staging') {
        # Must match the staging game build -- resolve the Windows asset off the tag.
        $rel = Invoke-RestMethod 'https://api.github.com/repos/CarbonCommunity/Carbon/releases/tags/rustbeta_staging_build' -Headers @{ 'User-Agent' = 'hizen-testenv' }
        $asset = $rel.assets | Where-Object { $_.name -match 'Carbon\.Windows.*\.zip$' } | Select-Object -First 1
        if (-not $asset) { throw "No Carbon Windows asset on rustbeta_staging_build. Assets: $($rel.assets.name -join ', ')" }
        return $asset.browser_download_url
    }
    switch ($cfg.CarbonReleaseChannel) {
        'Edge'    { 'https://github.com/CarbonCommunity/Carbon/releases/download/edge_build/Carbon.Windows.Debug.zip' }
        'Preview' { 'https://github.com/CarbonCommunity/Carbon.Core/releases/download/preview_build/Carbon.Windows.Debug.zip' }
        default   { 'https://github.com/CarbonCommunity/Carbon.Core/releases/download/production_build/Carbon.Windows.Release.zip' }
    }
}

function Get-OxideUrl {
    param([string]$BranchKey)
    if ($BranchKey -eq 'staging') {
        return 'https://downloads.oxidemod.com/artifacts/Oxide.Rust/staging/Oxide.Rust-win.zip'
    }
    return 'https://umod.org/games/rust/download?tag=public'
}

foreach ($inst in Resolve-Instances -Mod $Mod -Branch $Branch) {
    $p = Get-InstancePaths $inst
    Write-Host "`n=== $inst ===" -ForegroundColor Cyan
    New-Item -ItemType Directory -Force -Path $p.Server | Out-Null
    $temp = Join-Path $p.Root 'temp'
    New-Item -ItemType Directory -Force -Path $temp | Out-Null

    # --- game ---------------------------------------------------------------
    # staging instances use the Rust staging beta; debug is always staging too (it's
    # the debugger/local-build flavor of staging); release uses the public branch.
    $gameBranch = if ($p.Branch -eq 'debug') { $script:CarbonDebugGameBeta } else { $p.Branch }
    $betaArgs = if ($gameBranch -eq 'staging') { @('-beta', 'staging') } else { @() }
    $validate = if ($Force) { 'validate' } else { '' }
    Write-Host "SteamCMD app_update 258550 ($($p.Branch) -> rust:$gameBranch)..." -ForegroundColor DarkGray
    & $steamExe +force_install_dir (Resolve-Path $p.Server).Path +login anonymous +app_update 258550 @betaArgs $validate +quit
    if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne 7) {
        throw "SteamCMD failed for $inst (exit $LASTEXITCODE)."
    }
    if (-not (Test-Path $p.Exe)) { throw "RustDedicated.exe missing after install for $inst." }

    # --- mod ----------------------------------------------------------------
    if ($p.Branch -eq 'debug') {
        # Debug instance: deploy the locally-built Carbon overlay instead of a
        # downloaded release, and switch on the Mono soft-debugger for VS attach.
        Deploy-LocalCarbon -Paths $p -Cfg $cfg
        Set-DoorstopMonoDebug -Paths $p -Address (Get-CarbonDebugAddress -Cfg $cfg) -Suspend ([bool]$cfg.CarbonDebugSuspend)
    }
    else {
        if ($p.Mod -eq 'carbon') {
            $url = Get-CarbonUrl -BranchKey $p.Branch
        } else {
            $url = Get-OxideUrl -BranchKey $p.Branch
        }
        $archive = Join-Path $temp ([System.IO.Path]::GetFileName(($url -split '\?')[0]))
        if ([string]::IsNullOrWhiteSpace([System.IO.Path]::GetExtension($archive))) { $archive = "$archive.zip" }
        Write-Host "Installing $($p.Mod) ($($p.Branch)): $url" -ForegroundColor DarkGray
        Invoke-WebRequest $url -OutFile $archive
        Expand-Archive -Path $archive -DestinationPath $p.Server -Force   # overlays onto the install
        Remove-Item $archive -Force
    }

    # --- refs: single folder the plugin build references (RustManagedDir) ----
    # Game Managed/*.dll, plus Carbon's managed set (Carbon.* API) so carbon
    # plugins compile. Oxide ships its DLLs into Managed already.
    Write-Host "Exporting refs..." -ForegroundColor DarkGray
    if (Test-Path $p.Refs) { Remove-Item $p.Refs -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $p.Refs | Out-Null
    Copy-Item (Join-Path $p.Managed '*.dll') $p.Refs -Force
    $carbonManaged = Join-Path $p.ModDir 'managed'
    if ($p.Mod -eq 'carbon' -and (Test-Path $carbonManaged)) {
        Get-ChildItem $carbonManaged -Recurse -Filter '*.dll' |
            Copy-Item -Destination $p.Refs -Force
    }

    Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host "Installed $inst -> $($p.Server)" -ForegroundColor Green
}

Write-Host "`nDone. Start with:  .\start.ps1 -Mod Carbon -Branch Release" -ForegroundColor Green
