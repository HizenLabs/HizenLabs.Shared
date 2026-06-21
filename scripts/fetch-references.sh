#!/usr/bin/env bash
#
# Fetches the Rust Dedicated Server managed assemblies via SteamCMD into ./managed
# so net48 projects can reference the game DLLs. These are NOT committed (copyrighted
# Facepunch assets); managed/ is git-ignored.
#
# Requires: steamcmd on PATH (https://developer.valvesoftware.com/wiki/SteamCMD).
# Usage: scripts/fetch-references.sh [dest-dir]   (default: <repo>/managed)
set -euo pipefail

APPID=258550   # Rust Dedicated Server
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
DEST="${1:-$REPO_DIR/managed}"
INSTALL_DIR="$REPO_DIR/.rds"

if ! command -v steamcmd >/dev/null 2>&1; then
  echo "error: steamcmd not found on PATH." >&2
  echo "Install it, or set RustManagedDir in Directory.Build.User.props to an existing server's Managed folder." >&2
  exit 1
fi

echo "==> Installing/updating Rust Dedicated Server (app $APPID) into $INSTALL_DIR"
steamcmd +force_install_dir "$INSTALL_DIR" +login anonymous +app_update "$APPID" validate +quit

MANAGED_SRC="$INSTALL_DIR/RustDedicated_Data/Managed"
if [ ! -d "$MANAGED_SRC" ]; then
  echo "error: expected managed assemblies at $MANAGED_SRC" >&2
  exit 1
fi

echo "==> Copying managed assemblies to $DEST"
mkdir -p "$DEST"
cp -f "$MANAGED_SRC"/*.dll "$DEST"/
echo "==> Done. $(ls -1 "$DEST"/*.dll | wc -l) assemblies in $DEST"
