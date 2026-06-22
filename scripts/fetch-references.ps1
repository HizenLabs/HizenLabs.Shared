#requires -Version 5.1
<#
.SYNOPSIS
    Fetches the Rust Dedicated Server managed assemblies (the game DLLs) needed to
    build HizenLabs plugins. Downloads SteamCMD if it isn't already present, installs
    or updates the dedicated server, then copies the managed assemblies into a
    git-ignored .\managed.

    The game DLLs are copyrighted Facepunch assets and are never committed.
.PARAMETER Dest
    Destination directory for the managed assemblies. Defaults to <repo>\managed.
#>
[CmdletBinding()]
param(
    [string]$Dest
)

$ErrorActionPreference = 'Stop'
$AppId = 258550  # Rust Dedicated Server

$repo       = Split-Path -Parent $PSScriptRoot
$tools      = Join-Path $repo '.tools'
$steamDir   = Join-Path $tools 'steamcmd'
$steamExe   = Join-Path $steamDir 'steamcmd.exe'
$installDir = Join-Path $repo '.rds'
if (-not $Dest) { $Dest = Join-Path $repo 'managed' }

# 1. Ensure SteamCMD is available (download + extract if missing).
if (-not (Test-Path $steamExe)) {
    Write-Host "Downloading SteamCMD..."
    New-Item -ItemType Directory -Force -Path $steamDir | Out-Null
    $zip = Join-Path $tools 'steamcmd.zip'
    Invoke-WebRequest -Uri 'https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip' -OutFile $zip
    Expand-Archive -Path $zip -DestinationPath $steamDir -Force
    Remove-Item $zip -Force
}

# 2. Install or update the dedicated server (anonymous login). Downloads several GB.
Write-Host "Installing/updating Rust Dedicated Server (app $AppId). This can take a while."
& $steamExe +force_install_dir $installDir +login anonymous +app_update $AppId validate +quit

# 3. Copy the managed assemblies into the reference folder.
$managedSrc = Join-Path $installDir 'RustDedicated_Data\Managed'
if (-not (Test-Path $managedSrc)) {
    throw "Expected managed assemblies at '$managedSrc' but the folder was not found."
}

New-Item -ItemType Directory -Force -Path $Dest | Out-Null
Copy-Item -Path (Join-Path $managedSrc '*.dll') -Destination $Dest -Force

$count = (Get-ChildItem -Path $Dest -Filter *.dll).Count
Write-Host "Done. $count assemblies copied to '$Dest'."
