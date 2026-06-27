# =============================================================================
# Wipe the WORLD: delete the save, map cache and identity data so the next start
# generates a brand-new map. Keeps the installed game, mod and your deployed
# plugins. (Stops the server first if it's running.)
#
#   .\wipe.ps1 -Mod Carbon -Branch Release   # wipe one instance's world
#   .\wipe.ps1                               # wipe all instances' worlds
#   .\wipe.ps1 -Mod Carbon -Reinstall        # also re-pull/validate game+mod
#
# -Branch: All (default) | Staging | Release
# -Mod:    All (default) | Oxide   | Carbon
# -Reinstall: run install.ps1 (-Force validate) after wiping.
#
# To also clear configs/plugins (dev refresh), use .\clean.ps1.
# =============================================================================
[CmdletBinding()]
param(
    [ValidateSet('All', 'Staging', 'Release', 'Debug')][string]$Branch = 'All',
    [ValidateSet('All', 'Oxide', 'Carbon')][string]$Mod = 'All',
    [switch]$Reinstall
)
. "$PSScriptRoot\_common.ps1"

foreach ($inst in Resolve-Instances -Mod $Mod -Branch $Branch) {
    $p = Get-InstancePaths $inst
    if (-not (Test-Path $p.Server)) { continue }

    if (Get-ServerProcess -Paths $p) {
        & "$PSScriptRoot\stop.ps1" -Mod $p.Mod -Branch $p.Branch
    }

    # The identity dir holds the save + per-server cfg; the *.map cache sits in
    # the install root. Removing both forces fresh world generation on next boot.
    if (Test-Path $p.IdentityDir) { Remove-Item $p.IdentityDir -Recurse -Force }
    Get-ChildItem $p.Server -Filter '*.map' -File -ErrorAction SilentlyContinue | Remove-Item -Force
    Get-ChildItem $p.Server -Filter '*.sav*' -File -ErrorAction SilentlyContinue | Remove-Item -Force
    Write-Host "Wiped world for $inst." -ForegroundColor Green

    if ($Reinstall) {
        & "$PSScriptRoot\install.ps1" -Mod $p.Mod -Branch $p.Branch -Force
    }
}
