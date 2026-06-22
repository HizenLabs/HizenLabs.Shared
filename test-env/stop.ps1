# =============================================================================
# Stop Rust test servers (graceful; containers kept so a later start is fast).
# Use docker\reset.ps1 for a fresh recreate.
#
#   .\stop.ps1                            # all mods, all branches (default)
#   .\stop.ps1 -Mod Carbon -Branch Staging
#
# -Branch: All (default) | Staging | Prod      (Prod = release game branch)
# -Mod:    All (default) | Oxide   | Carbon
# =============================================================================
[CmdletBinding()]
param(
    [ValidateSet('All','Staging','Prod')][string]$Branch = 'All',
    [ValidateSet('All','Oxide','Carbon')][string]$Mod = 'All'
)

$branches = switch ($Branch) {
    'All'     { @('release','staging') }
    'Prod'    { @('release') }
    'Staging' { @('staging') }
}
$mods = switch ($Mod) {
    'All'    { @('carbon','oxide') }
    'Oxide'  { @('oxide') }
    'Carbon' { @('carbon') }
}

$services = @(foreach ($m in $mods) { foreach ($b in $branches) { "$m-$b" } })

Write-Host ("Stopping: " + ($services -join ', ')) -ForegroundColor Yellow

Push-Location (Join-Path $PSScriptRoot 'docker')
try {
    docker compose stop $services
} finally {
    Pop-Location
}
