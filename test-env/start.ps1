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
    # -logfile is REQUIRED to keep the console clean: RustDedicated is a Unity dedicated-server
    # build, and those write the engine log to stdout when no -logfile is given. Rust's own
    # ServerConsole (input line + fps/gc status bar) renders every line too, so without this
    # flag ALL console output appears twice (one plain copy, one ServerConsole-rendered copy).
    # With it, the raw engine stream goes to the file and the console shows one copy.
    New-Item -ItemType Directory -Force -Path $p.LogDir | Out-Null
    $argList = @(
        '-batchmode', '-nographics',
        '-logfile', (q (Join-Path $p.LogDir 'RustDedicated.log')),
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

    # Open the instance's OWN console window (via cmd, so it's a separate window even
    # from VS Code / Windows Terminal) and run the exe as a DIRECT child of cmd
    # (`cmd /s /c ""<exe>" <args>"`, NOT `cmd /c start`). `start` detaches the exe into
    # a fresh console whose handles Mono's console driver chokes on with Carbon DEBUG
    # flavored builds -- the CI staging zip (Carbon.Windows.Debug.zip) and the local
    # carbon-debug build alike -- spamming 'SetConsoleCursorInfo failed' from
    # ServerConsole.LogThread. Sharing cmd's console works for every flavor, still
    # gives a real type-able server console, and for carbon-debug it's also the
    # console the Mono debugger can drive.
    # /s + wrapping the whole command in an OUTER quote pair is required: the args
    # contain several quoted values (map, hostname, password), so without it cmd's
    # quote-stripping mangles the exe path and the server never starts.
    $cmdLine = '/s /c ""{0}" {1}"' -f $p.Exe, $serverArgs
    Start-Process -FilePath 'cmd.exe' -ArgumentList $cmdLine -WorkingDirectory $p.Server | Out-Null

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
