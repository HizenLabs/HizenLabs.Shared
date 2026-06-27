# =============================================================================
# Open the in-container server console (tmux attach) of a running Rust test
# server. Attach is one-at-a-time; when the params match more than one running
# server you get a numbered picker.
#
#   .\console.ps1                              # pick from all running servers
#   .\console.ps1 -Mod Carbon                  # carbon, both branches
#   .\console.ps1 -Branch Release              # both mods, release only
#   .\console.ps1 -Mod Carbon -Branch Release  # straight in (single match)
#   .\console.ps1 -ReadOnly                    # watch only, no keyboard input
#
# -Branch: All (default) | Staging | Release      (Release = release game branch)
# -Mod:    All (default) | Oxide   | Carbon
#
# Attaches AS linuxgsm directly (docker exec -it -u linuxgsm ... tmux attach),
# so the PTY survives. The Docker Desktop "exec" shell loses the PTY at the
# `su - linuxgsm` layer, which is why tmux is unusable there.
#
#   DETACH, server keeps running:   Ctrl+B  then  D
#
# Ctrl+C does NOT detach -- it is forwarded into the server console. Likewise,
# don't type `quit` at the console: that shuts the server down. Use Ctrl+B D.
# =============================================================================
[CmdletBinding()]
param(
    [ValidateSet('All','Staging','Release')][string]$Branch = 'All',
    [ValidateSet('All','Oxide','Carbon')][string]$Mod = 'All',
    [switch]$ReadOnly
)

# Numbered console picker. No GUI / no module dependency, so it works in
# Windows PowerShell 5.1, PowerShell 7, and over a plain SSH/term. (Out-GridView
# exists but needs the desktop GUI; PSReadLine has a menu but only for
# completion -- a Read-Host prompt is the portable choice.) Auto-returns the
# sole item; $null on cancel.
function Select-FromList {
    param([string]$Title, [string[]]$Items)
    if ($Items.Count -eq 1) { return $Items[0] }
    Write-Host "`n$Title" -ForegroundColor Cyan
    for ($i = 0; $i -lt $Items.Count; $i++) {
        Write-Host ("  [{0}] {1}" -f ($i + 1), $Items[$i])
    }
    while ($true) {
        $sel = Read-Host "Select 1-$($Items.Count) (Enter to cancel)"
        if ([string]::IsNullOrWhiteSpace($sel)) { return $null }
        $n = 0
        if ([int]::TryParse($sel, [ref]$n) -and $n -ge 1 -and $n -le $Items.Count) {
            return $Items[$n - 1]
        }
        Write-Host "  Not a valid choice." -ForegroundColor Yellow
    }
}

$branches = switch ($Branch) {
    'All'     { @('release','staging') }
    'Release' { @('release') }
    'Staging' { @('staging') }
}
$mods = switch ($Mod) {
    'All'    { @('carbon','oxide') }
    'Oxide'  { @('oxide') }
    'Carbon' { @('carbon') }
}

# Param set -> candidate container names, then keep only the ones running now.
$candidates = @(foreach ($m in $mods) { foreach ($b in $branches) { "rust-$m-$b" } })
$running = @(docker ps --filter 'name=rust-' --format '{{.Names}}')
$targets = @($candidates | Where-Object { $running -contains $_ })

if ($targets.Count -eq 0) {
    Write-Host "No running server matches -Mod $Mod -Branch $Branch." -ForegroundColor Yellow
    if ($running.Count -gt 0) {
        Write-Host ("Running now: " + ($running -join ', ')) -ForegroundColor DarkGray
    } else {
        Write-Host "Nothing is running. Start one with:  .\start.ps1" -ForegroundColor DarkGray
    }
    return
}

$container = Select-FromList -Title 'Attach to which server?' -Items $targets
if (-not $container) { Write-Host 'Cancelled.' -ForegroundColor DarkGray; return }

# LinuxGSM runs the server inside a tmux session; list them and (if several) pick.
$lsRaw = docker exec -u linuxgsm $container tmux ls 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "No tmux session in $container -- the server may still be booting." -ForegroundColor Yellow
    Write-Host ("tmux said: " + ($lsRaw -join ' ')) -ForegroundColor DarkGray
    return
}
$sessions = @($lsRaw | ForEach-Object { ($_ -split ':')[0] } | Where-Object { $_ })

$session = Select-FromList -Title "Which tmux session in $container?" -Items $sessions
if (-not $session) { Write-Host 'Cancelled.' -ForegroundColor DarkGray; return }

Write-Host ""
Write-Host ("Attaching to {0} [{1}] ..." -f $container, $session) -ForegroundColor Green
Write-Host "Detach (server keeps running):  Ctrl+B  then  D" -ForegroundColor Cyan
Write-Host ""

$attachArgs = @('attach')
if ($ReadOnly) { $attachArgs += '-r' }
$attachArgs += @('-t', $session)
docker exec -it -u linuxgsm $container tmux @attachArgs
