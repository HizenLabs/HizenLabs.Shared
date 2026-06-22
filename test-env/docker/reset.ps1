# =============================================================================
# Stateless reset. With baked images and NO volumes, recreating a container is a
# guaranteed-fresh install: fresh game (baked), fresh mod (baked), reseeded
# config/permissions, cleared logs, brand-new world save.
#
#   .\reset.ps1 carbon-release          # recreate that service, fresh
#   .\reset.ps1 carbon-release -Hard    # also wipe host plugins/config/logs
#
# Your deployed plugins (servers\rust-<instance>\plugins) are PRESERVED unless
# you pass -Hard. config/ and logs/ are reseeded/cleared on every boot anyway.
# =============================================================================
param(
    [Parameter(Mandatory=$true)][string]$Instance,
    [switch]$Hard
)

$composeServices = @('carbon-release','oxide-release','carbon-staging','oxide-staging')
Push-Location $PSScriptRoot
try {
$container = "rust-$Instance"
$srv = Join-Path $PSScriptRoot "servers\$container"
$isCompose = $composeServices -contains $Instance

# Stop first so nothing is writing while we (optionally) wipe host folders.
if ($isCompose) {
    docker compose stop $Instance 2>$null | Out-Null
} else {
    docker stop $container 2>$null | Out-Null
}

if ($Hard) {
    Write-Host "HARD reset: clearing host folders for $container (plugins, config, logs)..." -ForegroundColor Yellow
    foreach ($d in @('plugins','config','logs')) {
        $p = Join-Path $srv $d
        if (Test-Path $p) {
            Get-ChildItem -Path $p -Force -ErrorAction SilentlyContinue |
                Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

if ($isCompose) {
    Write-Host "Recreating compose service '$Instance' fresh..." -ForegroundColor Cyan
    docker compose up -d --force-recreate $Instance
} else {
    # run-extra containers were started with custom args this script does not
    # know, so a full recreate must come from run-extra.sh. Remove the stale
    # container so the next run-extra is clean.
    Write-Host "Removing run-extra container '$container'..." -ForegroundColor Cyan
    docker rm -f $container 2>$null | Out-Null
    Write-Host "Re-run it with:  ./run-extra.sh <name> <carbon|oxide> <release|staging> <port_base>" -ForegroundColor DarkGray
}

Write-Host "Done. No volumes exist, so a recreate is already a clean install." -ForegroundColor Green
} finally { Pop-Location }
