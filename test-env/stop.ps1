# =============================================================================
# Stop local Rust test servers started by start.ps1 (by tracked PID).
#
#   .\stop.ps1                              # all running instances
#   .\stop.ps1 -Mod Carbon -Branch Staging  # just that one
#
# -Branch: All (default) | Staging | Release      (Release = release game branch)
# -Mod:    All (default) | Oxide   | Carbon
#
# This terminates the process. It's a disposable test env (use .\wipe.ps1 for a
# fresh world), so a hard stop is fine; for a clean save instead, type `quit` in
# the server's console window before stopping.
# =============================================================================
[CmdletBinding()]
param(
    [ValidateSet('All', 'Staging', 'Release', 'Debug')][string]$Branch = 'All',
    [ValidateSet('All', 'Oxide', 'Carbon')][string]$Mod = 'All'
)
. "$PSScriptRoot\_common.ps1"

foreach ($inst in Resolve-Instances -Mod $Mod -Branch $Branch) {
    $p = Get-InstancePaths $inst
    $proc = Get-ServerProcess -Paths $p
    if (-not $proc) { continue }

    Write-Host "Stopping $inst (PID $($proc.Id))..." -ForegroundColor Yellow
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    $proc.WaitForExit(15000) | Out-Null
    Remove-Item $p.PidFile -ErrorAction SilentlyContinue
    Write-Host "Stopped $inst." -ForegroundColor Green
}
