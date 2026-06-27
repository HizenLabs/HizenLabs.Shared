# =============================================================================
# Shared helpers for the local Rust test-env scripts. Dot-sourced by the others:
#     . "$PSScriptRoot\_common.ps1"
#
# Preserves the old docker matrix -- {carbon,oxide} x {release,staging}, same
# instance names (rust-<mod>-<branch>) and ports -- but everything lives in a
# plain local install under .\servers\ instead of a container.
# =============================================================================
$ErrorActionPreference = 'Stop'

$script:Mods     = @('carbon', 'oxide')
$script:Branches = @('release', 'staging')

# game=BASE/udp  rcon=BASE+1/tcp  query=BASE+2/udp  app=BASE+3/tcp  (matches docker)
# carbon-debug is an opt-in extra instance (see Resolve-Instances) running a LOCAL
# Carbon build with the Mono debugger on; it sits past the matrix at 28240.
$script:PortBase = @{
    'carbon-release' = 28200
    'oxide-release'  = 28210
    'carbon-staging' = 28220
    'oxide-staging'  = 28230
    'carbon-debug'   = 28240
}

function Get-TestEnvRoot { $PSScriptRoot }

# -Mod/-Branch (All|Carbon|Oxide / All|Release|Staging|Debug) -> instance keys like
# 'carbon-release'. Same expansion start.ps1/stop.ps1 used under docker.
# 'Debug' is the special carbon-only local-build instance (carbon-debug); it is
# opt-in and deliberately NOT part of 'All' so a bare start/install never touches it.
function Resolve-Instances {
    param(
        [ValidateSet('All', 'Oxide', 'Carbon')][string]$Mod = 'All',
        [ValidateSet('All', 'Staging', 'Release', 'Debug')][string]$Branch = 'All'
    )
    if ($Branch -eq 'Debug') { return @('carbon-debug') }
    $mods = if ($Mod -eq 'All') { $script:Mods } else { @($Mod.ToLowerInvariant()) }
    $brs  = if ($Branch -eq 'All') { $script:Branches } else { @($Branch.ToLowerInvariant()) }
    @(foreach ($m in $mods) { foreach ($b in $brs) { "$m-$b" } })
}

# All the paths/ports for one instance, in one object so the scripts stay terse.
function Get-InstancePaths {
    param([Parameter(Mandatory)][string]$Instance)   # e.g. carbon-release
    $mod, $branch = $Instance -split '-', 2
    $root     = Join-Path (Get-TestEnvRoot) "servers\rust-$Instance"
    $server   = Join-Path $root 'server'
    $identity = "rust-$Instance"
    $modDir   = Join-Path $server $mod                          # carbon\ or oxide\
    $cfgSub   = if ($mod -eq 'carbon') { 'configs' } else { 'config' }
    [pscustomobject]@{
        Instance    = $Instance
        Mod         = $mod
        Branch      = $branch
        Root        = $root
        Server      = $server
        Refs        = Join-Path $root 'refs'
        Exe         = Join-Path $server 'RustDedicated.exe'
        Managed     = Join-Path $server 'RustDedicated_Data\Managed'
        Identity    = $identity
        IdentityDir = Join-Path $server "server\$identity"       # where Rust writes the save
        ModDir      = $modDir
        ModPlugins  = Join-Path $modDir 'plugins'
        ModConfigs  = Join-Path $modDir $cfgSub
        ModData     = Join-Path $modDir 'data'
        ModLang     = Join-Path $modDir 'lang'
        LogDir      = Join-Path $server 'logs'
        PidFile     = Join-Path $root '.pid'
        PortBase    = $script:PortBase[$Instance]
    }
}

# Loads Local.config.ps1 (git-ignored) or falls back to the committed example.
function Import-LocalConfig {
    $root    = Get-TestEnvRoot
    $local   = Join-Path $root 'Local.config.ps1'
    $example = Join-Path $root 'Local.config.example.ps1'
    if (Test-Path $local) {
        return (& $local)
    }
    Write-Host "No Local.config.ps1 -- using example defaults. Run .\config.ps1 to make your own." -ForegroundColor DarkYellow
    return (& $example)
}

# Finds the live RustDedicated for an instance by the unique +server.identity on
# its command line (we launch via `cmd start`, so we can't capture the PID at
# spawn time). Returns a CIM object with .ProcessId, or $null.
function Find-ServerProcess {
    param([Parameter(Mandatory)]$Paths)
    $needle = "+server.identity $($Paths.Identity)"
    Get-CimInstance Win32_Process -Filter "Name='RustDedicated.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -and $_.CommandLine -match [regex]::Escape($needle) } |
        Select-Object -First 1
}

# Returns the running RustDedicated process for an instance, or $null. Trusts the
# pidfile first, then falls back to matching by identity (and re-records the pid),
# so it self-heals after a `cmd start` launch or a deleted pidfile.
function Get-ServerProcess {
    param([Parameter(Mandatory)]$Paths)
    if (Test-Path $Paths.PidFile) {
        $procId = (Get-Content $Paths.PidFile -ErrorAction SilentlyContinue | Select-Object -First 1)
        $proc = if ($procId) { Get-Process -Id $procId -ErrorAction SilentlyContinue } else { $null }
        if ($proc -and $proc.ProcessName -eq 'RustDedicated') { return $proc }
        Remove-Item $Paths.PidFile -ErrorAction SilentlyContinue
    }
    $cim = Find-ServerProcess -Paths $Paths
    if ($cim) {
        Set-Content -Path $Paths.PidFile -Value $cim.ProcessId
        return (Get-Process -Id $cim.ProcessId -ErrorAction SilentlyContinue)
    }
    return $null
}

# Writes ownerid/moderatorid lines into the instance's users.cfg from config, so
# owners are applied on every start without a manual step.
function Set-ServerUsers {
    param([Parameter(Mandatory)]$Paths, [Parameter(Mandatory)]$Cfg)
    $cfgDir = Join-Path $Paths.IdentityDir 'cfg'
    New-Item -ItemType Directory -Force -Path $cfgDir | Out-Null
    $lines = @()
    foreach ($o in @($Cfg.Owners))     { if ($o) { $lines += "ownerid $o `"owner`" `"local`"" } }
    foreach ($m in @($Cfg.Moderators)) { if ($m) { $lines += "moderatorid $m `"mod`" `"local`"" } }
    Set-Content -Path (Join-Path $cfgDir 'users.cfg') -Value $lines -Encoding ASCII
    return $lines.Count
}

# --- Debug instance (local Carbon build) helpers -----------------------------

# Resolves the local Carbon overlay path (the 'noarchive' Debug output, e.g.
# Carbon\release\.tmp\Debug) from config. Relative paths resolve from test-env.
function Resolve-LocalCarbonPath {
    param([Parameter(Mandatory)]$Cfg)
    $path = $Cfg.CarbonLocalBuildPath
    if ([string]::IsNullOrWhiteSpace($path)) {
        throw "CarbonLocalBuildPath is not set in Local.config.ps1 -- it's required for the Debug instance. Run .\config.ps1 and add it."
    }
    if (-not [System.IO.Path]::IsPathRooted($path)) {
        $path = Join-Path (Get-TestEnvRoot) $path
    }
    return [System.IO.Path]::GetFullPath($path)
}

# Returns the configured Mono debugger endpoint (ip:port), defaulting to
# 127.0.0.1:55555 when Local.config.ps1 doesn't set CarbonDebugAddress.
function Get-CarbonDebugAddress {
    param([Parameter(Mandatory)]$Cfg)
    if ($Cfg.CarbonDebugAddress) { return $Cfg.CarbonDebugAddress }
    return '127.0.0.1:55555'
}

# carbon-debug is ALWAYS the Rust staging channel -- it exists to debug local Carbon
# staging builds. One source of truth for install.ps1 (game beta) and the build: the
# Steam beta branch + the Carbon compile define / release tag CI uses for rust_beta/staging.
$script:CarbonDebugGameBeta = 'staging'
$script:CarbonDebugDefine   = 'RUST_STAGING'
$script:CarbonDebugTag      = 'rustbeta_staging_build'

# Locates the Carbon repo root: explicit CarbonRepoPath if set, else derived by
# walking up from CarbonLocalBuildPath to the folder holding the build script.
function Get-CarbonRepoPath {
    param([Parameter(Mandatory)]$Cfg)
    if ($Cfg.CarbonRepoPath) {
        $p = $Cfg.CarbonRepoPath
        if (-not [System.IO.Path]::IsPathRooted($p)) { $p = Join-Path (Get-TestEnvRoot) $p }
        return [System.IO.Path]::GetFullPath($p)
    }
    $dir = Resolve-LocalCarbonPath -Cfg $Cfg
    while ($dir) {
        if (Test-Path ([System.IO.Path]::Combine($dir, 'tools', 'build', 'win', 'build.bat'))) { return $dir }
        $parent = Split-Path $dir -Parent
        if (-not $parent -or $parent -eq $dir) { break }
        $dir = $parent
    }
    throw "Couldn't find the Carbon repo from CarbonLocalBuildPath ('$(Resolve-LocalCarbonPath -Cfg $Cfg)'). Set CarbonRepoPath in Local.config.ps1."
}

# Builds the Carbon Debug overlay via the repo's build.bat with the STAGING define/tag
# (RUST_STAGING / rustbeta_staging_build). NOT build_debug_noarchive.bat -- that hardcodes
# EDGE/edge_build, which is wrong for staging (the define gates real patch/compile code).
# Throws on a non-zero exit so callers can abort BEFORE stopping a running server. The
# build compiles against the Rust refs in the repo's rust\ folder, so run
# tools\build\win\update_staging.bat first if your refs aren't staging yet.
function Invoke-CarbonDebugBuild {
    param([Parameter(Mandatory)]$Cfg)
    $repo = Get-CarbonRepoPath -Cfg $Cfg
    $bat  = [System.IO.Path]::Combine($repo, 'tools', 'build', 'win', 'build.bat')
    if (-not (Test-Path $bat)) { throw "Build script not found: $bat" }
    Write-Host "Building Carbon (Debug / $script:CarbonDebugDefine / $script:CarbonDebugTag) in $repo ..." -ForegroundColor Cyan
    & cmd.exe /c "`"$bat`" Debug $script:CarbonDebugDefine $script:CarbonDebugTag -noarchive"
    if ($LASTEXITCODE -ne 0) { throw "Carbon build failed (exit $LASTEXITCODE) -- not touching the running server." }
}

# Copies a locally-built Carbon overlay (winhttp.dll + doorstop_config.ini + carbon\)
# over the instance's server install -- the same overlay the downloaded zip applies.
function Deploy-LocalCarbon {
    param([Parameter(Mandatory)]$Paths, [Parameter(Mandatory)]$Cfg)
    $src = Resolve-LocalCarbonPath -Cfg $Cfg
    if (-not (Test-Path (Join-Path $src 'carbon\managed'))) {
        throw "No Carbon build at '$src' (expected carbon\managed\). Build it: tools\build\win\build.bat Debug RUST_STAGING rustbeta_staging_build -noarchive  (or just .\redeploy.ps1 -Build)"
    }
    Write-Host "Deploying local Carbon: $src" -ForegroundColor DarkGray
    Copy-Item -Path (Join-Path $src '*') -Destination $Paths.Server -Recurse -Force
}

# Turns on the Unity/Mono soft-debugger in the instance's doorstop_config.ini so
# Visual Studio (Tools for Unity) can attach. Idempotent -- rewrites [UnityMono] keys.
function Set-DoorstopMonoDebug {
    param(
        [Parameter(Mandatory)]$Paths,
        [string]$Address = '127.0.0.1:55555',
        [bool]$Suspend = $false
    )
    $ini = Join-Path $Paths.Server 'doorstop_config.ini'
    if (-not (Test-Path $ini)) { throw "doorstop_config.ini missing at $ini -- is this a Carbon (doorstop) install?" }
    $suspendVal = if ($Suspend) { 'true' } else { 'false' }
    $text = Get-Content $ini -Raw
    $text = [regex]::Replace($text, '(?im)^(\s*debug_enabled\s*=\s*).*$', '${1}true')
    $text = [regex]::Replace($text, '(?im)^(\s*debug_suspend\s*=\s*).*$', ('${1}' + $suspendVal))
    $text = [regex]::Replace($text, '(?im)^(\s*debug_address\s*=\s*).*$', ('${1}' + $Address))
    Set-Content -Path $ini -Value $text -Encoding ASCII
    Write-Host "Mono debugger enabled on $Address (suspend=$suspendVal)." -ForegroundColor DarkGray
}

# VSTU can't read Carbon's *embedded* PDBs, so we extract them side-by-side after
# every deploy. Critical: Deploy-LocalCarbon overwrites the DLLs but leaves old
# .pdb files in place -> a fresh DLL next to a stale PDB = the mismatch warning on
# breakpoints. Clearing + re-extracting on each deploy keeps them in lockstep.
function Update-CarbonDebugSymbols {
    param([Parameter(Mandatory)]$Paths, [Parameter(Mandatory)]$Cfg)
    $managed = Join-Path $Paths.Server 'carbon\managed'
    $tool = $Cfg.ExtractPdbPath
    if ([string]::IsNullOrWhiteSpace($tool)) {
        Write-Host "ExtractPdbPath not set in Local.config.ps1 -- skipping PDB extraction." -ForegroundColor DarkYellow
        return
    }
    if (-not [System.IO.Path]::IsPathRooted($tool)) { $tool = Join-Path (Get-TestEnvRoot) $tool }

    # nuke stale pdbs first so a removed/renamed assembly can't leave a mismatch behind
    Get-ChildItem $managed -Recurse -Filter *.pdb -File -ErrorAction SilentlyContinue |
        Remove-Item -Force -ErrorAction SilentlyContinue

    Write-Host "Extracting embedded PDBs -> $managed" -ForegroundColor DarkGray
    if ($tool -like '*.csproj') { & dotnet run --project $tool -c Release -- $managed recurse | Out-Host }
    else                        { & $tool $managed recurse | Out-Host }
    if ($LASTEXITCODE -ne 0) { Write-Warning "PDB extraction exited $LASTEXITCODE." }
}

# Carbon rewrites/merges carbon\config.json and (left to its own devices) will
# self-update its managed DLLs + hooks over your locally-built ones on boot. Since
# Deploy-LocalCarbon overlays the whole carbon\ tree each redeploy, re-assert the
# debug-instance settings every time so they can't drift back.
function Set-CarbonDebugConfig {
    param([Parameter(Mandatory)]$Paths)
    $cfgPath = Join-Path $Paths.Server 'carbon\config.json'
    if (-not (Test-Path $cfgPath)) {
        Write-Host "No carbon\config.json yet -- it's created on first boot; re-run this after." -ForegroundColor DarkYellow
        return
    }
    $json = Get-Content $cfgPath -Raw | ConvertFrom-Json

    # Kill both updaters: the main self-update AND the separate hook updater.
    if ($json.PSObject.Properties.Name -contains 'SelfUpdating') {
        $json.SelfUpdating.Enabled     = $false
        $json.SelfUpdating.HookUpdates = $false
    }
	
	$json.DeveloperMode = $true

    ($json | ConvertTo-Json -Depth 32) | Set-Content $cfgPath -Encoding UTF8
    Write-Host "Carbon self-update + hook-update disabled in config.json." -ForegroundColor DarkGray
}

