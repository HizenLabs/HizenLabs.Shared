# =============================================================================
# Manage your local, git-ignored config (Local.config.ps1).
#
#   .\config.ps1            # create Local.config.ps1 from the example + open it
#   .\config.ps1 -Apply     # write current Owners/Moderators into installed servers
#
# Local.config.ps1 holds your SteamIDs, rcon password and server defaults. It is
# git-ignored so it never lands in the public repo. Owners are also seeded
# automatically on every start.ps1; -Apply is for pushing changes to an already
# installed (or running, after a restart) server without starting it.
#
# -Apply honors -Mod/-Branch to target a subset.
# =============================================================================
[CmdletBinding()]
param(
    [switch]$Apply,
    [ValidateSet('All', 'Staging', 'Release')][string]$Branch = 'All',
    [ValidateSet('All', 'Oxide', 'Carbon')][string]$Mod = 'All'
)
. "$PSScriptRoot\_common.ps1"

$local   = Join-Path $PSScriptRoot 'Local.config.ps1'
$example = Join-Path $PSScriptRoot 'Local.config.example.ps1'

if (-not (Test-Path $local)) {
    Copy-Item $example $local
    Write-Host "Created $local from the example." -ForegroundColor Green
}

if (-not $Apply) {
    Write-Host "Edit your IDs/defaults, then:  .\config.ps1 -Apply   (or just .\start.ps1)" -ForegroundColor Cyan
    Start-Process notepad.exe $local
    return
}

$cfg = & $local
foreach ($inst in Resolve-Instances -Mod $Mod -Branch $Branch) {
    $p = Get-InstancePaths $inst
    if (-not (Test-Path $p.Server)) { continue }   # not installed
    $n = Set-ServerUsers -Paths $p -Cfg $cfg
    Write-Host "Applied $n owner/mod id(s) to $inst." -ForegroundColor Green
}
Write-Host "Note: a running server needs a restart to pick up users.cfg changes." -ForegroundColor DarkGray
