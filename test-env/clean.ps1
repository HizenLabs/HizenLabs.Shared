# =============================================================================
# Clean the mod state for a fresh dev iteration: removes deployed plugins and
# their configs / data / lang so plugins reload from defaults. Keeps the world
# (use .\wipe.ps1 for that) and the install. (Stops the server first if running.)
#
#   .\clean.ps1 -Mod Carbon -Branch Release   # refresh one instance
#   .\clean.ps1                               # refresh all instances
#   .\clean.ps1 -KeepPlugins                  # clear configs/data only, keep plugins
#
# -Branch: All (default) | Staging | Release
# -Mod:    All (default) | Oxide   | Carbon
# -KeepPlugins: leave deployed .cs plugins in place; only wipe configs/data/lang.
# =============================================================================
[CmdletBinding()]
param(
    [ValidateSet('All', 'Staging', 'Release', 'Debug')][string]$Branch = 'All',
    [ValidateSet('All', 'Oxide', 'Carbon')][string]$Mod = 'All',
    [switch]$KeepPlugins
)
. "$PSScriptRoot\_common.ps1"

function Clear-Dir {
    param([string]$Path)
    if (Test-Path $Path) {
        Get-ChildItem $Path -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    }
}

foreach ($inst in Resolve-Instances -Mod $Mod -Branch $Branch) {
    $p = Get-InstancePaths $inst
    if (-not (Test-Path $p.ModDir)) { continue }

    if (Get-ServerProcess -Paths $p) {
        & "$PSScriptRoot\stop.ps1" -Mod $p.Mod -Branch $p.Branch
    }

    Clear-Dir $p.ModConfigs
    Clear-Dir $p.ModData
    Clear-Dir $p.ModLang
    if (-not $KeepPlugins) { Clear-Dir $p.ModPlugins }

    $kept = if ($KeepPlugins) { ' (plugins kept)' } else { '' }
    Write-Host "Cleaned $inst$kept." -ForegroundColor Green
}
