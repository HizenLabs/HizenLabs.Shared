# =============================================================================
# Force a FRESH rebuild of the baked images, then recreate the containers.
#
# Use after a Rust wipe / game update: the game, mod, staging overlay and map
# all need re-baking, but the Dockerfile itself didn't change -- so start.ps1's
# cached `up --build` would be a no-op. This bumps CACHE_BUST to re-run the bake
# (re-download game, reinstall mod, redo staging overlay, regenerate map) while
# keeping the OS/LinuxGSM base layers cached.
#
#   .\rebuild.ps1                            # all four, fresh bake + recreate
#   .\rebuild.ps1 -Mod Carbon -Branch Staging
#   .\rebuild.ps1 -NoCache                   # nuke ALL cache incl. OS/base layers
#
# -Branch: All (default) | Staging | Prod      (Prod = release game branch)
# -Mod:    All (default) | Oxide   | Carbon
# -NoCache: full no-cache rebuild from the FROM line down (slowest, rarely needed)
# =============================================================================
[CmdletBinding()]
param(
    [ValidateSet('All','Staging','Prod')][string]$Branch = 'All',
    [ValidateSet('All','Oxide','Carbon')][string]$Mod = 'All',
    [switch]$NoCache
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

Write-Host ("Rebuilding (fresh bake): " + ($services -join ', ')) -ForegroundColor Magenta

Push-Location (Join-Path $PSScriptRoot 'docker')
try {
    if ($NoCache) {
        docker compose build --no-cache $services
    } else {
        $env:CACHE_BUST = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds().ToString()
        docker compose build $services
    }
    docker compose up -d --force-recreate $services
} finally {
    Remove-Item Env:CACHE_BUST -ErrorAction SilentlyContinue
    Pop-Location
}

Write-Host "Rebuild complete." -ForegroundColor Green
