# =============================================================================
# Build (if needed) and start Rust test servers.
#
#   .\start.ps1                            # all mods, all branches (default)
#   .\start.ps1 -Mod Carbon                # carbon, both branches
#   .\start.ps1 -Branch Release               # both mods, release only
#   .\start.ps1 -Mod Oxide -Branch Staging # oxide-staging only
#
# -Branch: All (default) | Staging | Release      (Release = release game branch)
# -Mod:    All (default) | Oxide   | Carbon
#
# Connect from the Rust client F1 console:  client.connect 127.0.0.1:<game-port>
#   carbon-release 28200  oxide-release 28210  carbon-staging 28220  oxide-staging 28230
# =============================================================================
[CmdletBinding()]
param(
    [ValidateSet('All','Staging','Release')][string]$Branch = 'All',
    [ValidateSet('All','Oxide','Carbon')][string]$Mod = 'All'
)

$branches = switch ($Branch) {
    'All'     { @('release','staging') }
    'Release'    { @('release') }
    'Staging' { @('staging') }
}
$mods = switch ($Mod) {
    'All'    { @('carbon','oxide') }
    'Oxide'  { @('oxide') }
    'Carbon' { @('carbon') }
}

$services = @(foreach ($m in $mods) { foreach ($b in $branches) { "$m-$b" } })

Write-Host ("Starting (build if needed): " + ($services -join ', ')) -ForegroundColor Cyan

Push-Location (Join-Path $PSScriptRoot 'docker')
try {
    docker compose up -d --build $services
} finally {
    Pop-Location
}
