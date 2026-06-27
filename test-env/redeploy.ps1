# =============================================================================
# Redeploy your locally-built Carbon onto the carbon-debug instance -- the fast
# inner loop after changing Carbon (no SteamCMD, no full re-install).
#
#   .\redeploy.ps1 -Build     # ONE-LINER: build Carbon, then stop/deploy/restart
#   .\redeploy.ps1 -Restart   # redeploy an already-built overlay, then restart
#   .\redeploy.ps1            # redeploy only (restarts if it was already running)
#
# -Build runs the Carbon repo's tools\build\win\build_debug_noarchive.bat for you
# (repo found via CarbonLocalBuildPath, or CarbonRepoPath). It builds BEFORE
# stopping the server, so a failed build leaves the running instance untouched.
# -Build implies a restart. CarbonLocalBuildPath points at release\.tmp\Debug.
#
# After it restarts, attach Visual Studio (Tools for Unity) to CarbonDebugAddress
# (default 127.0.0.1:55555). Carbon's DLLs are file-locked while the server runs,
# so this stops the instance before overwriting them.
# =============================================================================
[CmdletBinding()]
param([switch]$Build, [switch]$Restart)
. "$PSScriptRoot\_common.ps1"
$cfg = Import-LocalConfig

$p = Get-InstancePaths 'carbon-debug'
if (-not (Test-Path $p.Exe)) {
    throw "carbon-debug isn't installed yet. First run:  .\install.ps1 -Branch Debug"
}

# Build first (if asked) -- a failure throws here, before we stop the server.
if ($Build) {
    Invoke-CarbonDebugBuild -Cfg $cfg
}

$wasRunning = [bool](Get-ServerProcess -Paths $p)
if ($wasRunning) {
    & "$PSScriptRoot\stop.ps1" -Branch Debug
}

Deploy-LocalCarbon -Paths $p -Cfg $cfg
$dbgAddr = Get-CarbonDebugAddress -Cfg $cfg
Set-DoorstopMonoDebug -Paths $p -Address $dbgAddr -Suspend ([bool]$cfg.CarbonDebugSuspend)
Write-Host "Redeployed local Carbon to carbon-debug." -ForegroundColor Green

if ($Restart -or $Build -or $wasRunning) {
    & "$PSScriptRoot\start.ps1" -Branch Debug
} else {
    Write-Host "Start it with:  .\start.ps1 -Branch Debug   (then attach to $dbgAddr)" -ForegroundColor Cyan
}
