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
$script:PortBase = @{
    'carbon-release' = 28200
    'oxide-release'  = 28210
    'carbon-staging' = 28220
    'oxide-staging'  = 28230
}

function Get-TestEnvRoot { $PSScriptRoot }

# -Mod/-Branch (All|Carbon|Oxide / All|Release|Staging) -> instance keys like
# 'carbon-release'. Same expansion start.ps1/stop.ps1 used under docker.
function Resolve-Instances {
    param(
        [ValidateSet('All', 'Oxide', 'Carbon')][string]$Mod = 'All',
        [ValidateSet('All', 'Staging', 'Release')][string]$Branch = 'All'
    )
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
