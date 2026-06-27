# =============================================================================
# Redeploy your locally-built Carbon onto the carbon-debug instance -- the fast
# inner loop after rebuilding Carbon (no SteamCMD, no full re-install).
#
#   .\redeploy.ps1            # stop if running, copy local build, keep debugger on
#   .\redeploy.ps1 -Restart   # ...then start it again and print the attach address
#
# Build the overlay first in the Carbon repo:
#   tools\build\win\build_debug_noarchive.bat        -> release\.tmp\Debug
# (CarbonLocalBuildPath in Local.config.ps1 points here.) Then attach Visual
# Studio (Tools for Unity) to CarbonDebugAddress (default 127.0.0.1:55555).
#
# Carbon's DLLs are file-locked while the server runs, so this stops the instance
# before overwriting them; if it was running (or you pass -Restart) it starts again.
# =============================================================================
[CmdletBinding()]
param([switch]$Restart)
. "$PSScriptRoot\_common.ps1"
$cfg = Import-LocalConfig

$p = Get-InstancePaths 'carbon-debug'
if (-not (Test-Path $p.Exe)) {
    throw "carbon-debug isn't installed yet. First run:  .\install.ps1 -Branch Debug"
}

$wasRunning = [bool](Get-ServerProcess -Paths $p)
if ($wasRunning) {
    & "$PSScriptRoot\stop.ps1" -Branch Debug
}

Deploy-LocalCarbon -Paths $p -Cfg $cfg
$dbgAddr = Get-CarbonDebugAddress -Cfg $cfg
Set-DoorstopMonoDebug -Paths $p -Address $dbgAddr -Suspend ([bool]$cfg.CarbonDebugSuspend)
Write-Host "Redeployed local Carbon to carbon-debug." -ForegroundColor Green

if ($Restart -or $wasRunning) {
    & "$PSScriptRoot\start.ps1" -Branch Debug
} else {
    Write-Host "Start it with:  .\start.ps1 -Branch Debug   (then attach to $dbgAddr)" -ForegroundColor Cyan
}
