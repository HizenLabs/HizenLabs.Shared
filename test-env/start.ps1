# =============================================================================
# Start local Rust test servers. Each instance launches in its OWN console window
# (that window IS the server console -- type commands there; close/Ctrl+C stops
# that server). Owners from Local.config.ps1 are seeded on every start.
#
#   .\start.ps1                              # all installed instances
#   .\start.ps1 -Mod Carbon                  # both carbon instances
#   .\start.ps1 -Mod Carbon -Branch Release  # just carbon-release
#
# -Branch: All (default) | Staging | Release | Debug   (Release = release branch)
# -Mod:    All (default) | Oxide   | Carbon
#
# Connect from the Rust client F1 console:  client.connect 127.0.0.1:<game-port>
#   carbon-release 28200  oxide-release 28210  carbon-staging 28220  oxide-staging 28230
#   carbon-debug 28240 (local Carbon build + Mono debugger; -Branch Debug)
# Not installed yet? Run .\install.ps1 first (or pass -Install here).
# =============================================================================
[CmdletBinding()]
param(
    [ValidateSet('All', 'Staging', 'Release', 'Debug')][string]$Branch = 'All',
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

    $game = $p.PortBase; $rcon = $p.PortBase + 1; $query = $p.PortBase + 2; $app = $p.PortBase + 3
    $hostName = "{0} {1} (Local :{2})" -f $p.Mod, $p.Branch, $game

    # Double-quote only the values that contain spaces.
    function q { param($s) '"' + $s + '"' }
    $argList = @(
        '-batchmode', '-nographics',
        '+server.identity', $p.Identity,
        '+server.ip', '0.0.0.0',
        '+server.port', $game,
        '+server.queryport', $query,
        '+server.level', (q $cfg.ServerLevel),
        '+server.seed', $cfg.Seed,
        '+server.worldsize', $cfg.WorldSize,
        '+server.maxplayers', $cfg.MaxPlayers,
        '+server.tickrate', $cfg.Tickrate,
        '+server.hostname', (q $hostName),
        '+rcon.ip', '127.0.0.1',
        '+rcon.port', $rcon,
        '+rcon.password', (q $cfg.RconPassword),
        '+rcon.web', '1',
        '+app.listenip', '127.0.0.1',
        '+app.port', $app
    )

    $serverArgs = $argList -join ' '

    if ($p.Branch -eq 'debug') {
        # Open its OWN console window (via cmd, so it's a separate window even from VS
        # Code / Windows Terminal), but run the exe as a DIRECT child of cmd (`cmd /c
        # "<exe>"`, NOT `cmd /c start`). The exe then shares cmd's normal console -- a
        # clean console the Mono debugger can drive. `cmd /c start` instead detaches the
        # exe into a fresh console whose handles the debugger chokes on (Rust's
        # ServerConsole -> 'SetConsoleCursorInfo failed'); a bare Start-Process <exe> has
        # no window of its own here. This gives a real server console AND a live debugger.
        $cmdLine = '/c "{0}" {1}' -f $p.Exe, $serverArgs
        Start-Process -FilePath 'cmd.exe' -ArgumentList $cmdLine -WorkingDirectory $p.Server | Out-Null
    }
    else {
        # Launch through `cmd start` so the server gets a REAL new console window (its
        # live, type-able console), even from VS Code / Windows Terminal. The quoted
        # title stops `start` from treating the quoted exe path as the title. We can't
        # get the PID from `start`, so find it by identity right after.
        $cmdLine = '/c start "rust-{0}" /D "{1}" "{2}" {3}' -f $inst, $p.Server, $p.Exe, $serverArgs
        Start-Process -FilePath 'cmd.exe' -ArgumentList $cmdLine | Out-Null
    }

    $proc = $null
    for ($i = 0; $i -lt 20 -and -not $proc; $i++) {
        Start-Sleep -Milliseconds 500
        $proc = Find-ServerProcess -Paths $p
    }
    if ($proc) {
        Set-Content -Path $p.PidFile -Value $proc.ProcessId
        Write-Host ("Started {0} (PID {1}) on :{2}  rcon :{3}  owners:{4}" -f `
            $inst, $proc.ProcessId, $game, $rcon, $owners) -ForegroundColor Green
        if ($p.Branch -eq 'debug') {
            Write-Host ("  Attach a Mono debugger (VS -> Tools for Unity) to {0}" -f (Get-CarbonDebugAddress -Cfg $cfg)) -ForegroundColor Cyan
        }
    } else {
        Write-Host ("Launched {0} but couldn't confirm the process -- check its window for a startup error." -f $inst) -ForegroundColor Yellow
    }
}
