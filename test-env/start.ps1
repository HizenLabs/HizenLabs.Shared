# =============================================================================
# Start local Rust test servers. Each instance launches in its OWN console window
# (that window IS the server console -- type commands there; close/Ctrl+C stops
# that server). Owners from Local.config.ps1 are seeded on every start.
#
#   .\start.ps1                              # all installed instances
#   .\start.ps1 -Mod Carbon                  # both carbon instances
#   .\start.ps1 -Mod Carbon -Branch Release  # just carbon-release
#
# -Branch: All (default) | Staging | Release      (Release = release game branch)
# -Mod:    All (default) | Oxide   | Carbon
#
# Connect from the Rust client F1 console:  client.connect 127.0.0.1:<game-port>
#   carbon-release 28200  oxide-release 28210  carbon-staging 28220  oxide-staging 28230
# Not installed yet? Run .\install.ps1 first (or pass -Install here).
# =============================================================================
[CmdletBinding()]
param(
    [ValidateSet('All', 'Staging', 'Release')][string]$Branch = 'All',
    [ValidateSet('All', 'Oxide', 'Carbon')][string]$Mod = 'All',
    [switch]$Install
)
. "$PSScriptRoot\_common.ps1"
$cfg = Import-LocalConfig

foreach ($inst in Resolve-Instances -Mod $Mod -Branch $Branch) {
    $p = Get-InstancePaths $inst

    if (-not (Test-Path $p.Exe)) {
        if ($Install) {
            & "$PSScriptRoot\install.ps1" -Mod $p.Mod -Branch $p.Branch
        } else {
            Write-Host "$inst not installed -- skipping. Install with:  .\install.ps1 -Mod $($p.Mod) -Branch $($p.Branch)" -ForegroundColor Yellow
            continue
        }
    }

    if (Get-ServerProcess -Paths $p) {
        Write-Host "$inst already running (PID $((Get-Content $p.PidFile))) -- skipping." -ForegroundColor DarkYellow
        continue
    }

    $owners = Set-ServerUsers -Paths $p -Cfg $cfg
    New-Item -ItemType Directory -Force -Path $p.LogDir | Out-Null
    $log = Join-Path $p.LogDir ("RustDedicated_{0}.log" -f (Get-Date -Format 'yyyyMMddHHmmss'))

    $game = $p.PortBase; $rcon = $p.PortBase + 1; $query = $p.PortBase + 2; $app = $p.PortBase + 3
    $hostName = "{0} {1} (Local :{2})" -f $p.Mod, $p.Branch, $game

    $argList = @(
        '-batchmode', '-nographics',
        '-logfile', "`"$log`"",
        '+server.identity', $p.Identity,
        '+server.ip', '0.0.0.0',
        '+server.port', $game,
        '+server.queryport', $query,
        '+server.level', "`"$($cfg.ServerLevel)`"",
        '+server.seed', $cfg.Seed,
        '+server.worldsize', $cfg.WorldSize,
        '+server.maxplayers', $cfg.MaxPlayers,
        '+server.tickrate', $cfg.Tickrate,
        '+server.hostname', "`"$hostName`"",
        '+rcon.ip', '127.0.0.1',
        '+rcon.port', $rcon,
        '+rcon.password', "`"$($cfg.RconPassword)`"",
        '+rcon.web', '1',
        '+app.listenip', '127.0.0.1',
        '+app.port', $app
    )

    # Launch directly (not via cmd) so -PassThru gives the REAL RustDedicated PID;
    # a console app started this way gets its own window = the live server console.
    $proc = Start-Process -FilePath $p.Exe -WorkingDirectory $p.Server -ArgumentList $argList -PassThru
    Set-Content -Path $p.PidFile -Value $proc.Id

    Write-Host ("Started {0} (PID {1}) on :{2}  rcon :{3}  owners:{4}  log: {5}" -f `
        $inst, $proc.Id, $game, $rcon, $owners, (Split-Path $log -Leaf)) -ForegroundColor Green
}
