# Rust plugin test environment

A reusable, containerized Rust (Facepunch) dedicated-server matrix for testing
plugins against **Carbon** and **Oxide**, on both the **release** and **staging**
game branches. Windows + Docker Desktop (WSL2), PowerShell. Built on LinuxGSM,
Ubuntu 24.04 base.

## Model: baked + stateless

Everything is baked into the image at **build time** -- the game install, the
mod framework, and (for staging) the staging overlay. The running container is
**stateless**:

- **No SteamCMD on boot, no named volumes.** The install lives in the image.
- **The map is pre-generated and baked too.** The procedural map (from `SEED`/`WORLD_SIZE`/`SERVER_LEVEL`) is generated once at build by booting the server, and cached in the image. Containers boot straight into the map instead of regenerating it. Change any of those values in `.env` -> rebuild to re-bake (build arg `PREGEN_MAP=false` skips the bake and regenerates at first boot).
- **Reset = recreate the container.** With no volumes, `up --force-recreate`
  (or `down && up`) is a guaranteed-fresh install every time. This is the whole
  point: plugins must be validated on clean installs, and persistent state while
  testing hides exactly the bugs you are trying to find.
- **One image per (framework x branch):** `rust-test-env:carbon-release`,
  `:oxide-release`, `:carbon-staging`, `:oxide-staging`. Release and staging are
  different game builds; Oxide rewrites managed assemblies that Carbon leaves
  intact -- so nothing is shared past the base layers, by design.
- **Refresh cadence:** rebuild when you want newer game/mod builds (staging
  moves daily; release in lockstep). Otherwise the images are frozen.

## Per-server folders

Each server gets its own folder under `./servers/<container-name>/`, nothing
shared:

```
servers/
  rust-carbon-release/{plugins,config,logs}
  rust-oxide-release/{plugins,config,logs}
  rust-carbon-staging/{plugins,config,logs}
  rust-oxide-staging/{plugins,config,logs}
```

| Mount      | Maps to                         | Lifecycle                                    |
|------------|---------------------------------|----------------------------------------------|
| `plugins/` | `/plugins-src` (read-only)      | Your deploy target. Poll-synced into the live plugin dir (~2s, hot-reload). **Persists** across restarts. |
| `config/`  | framework config dir            | **Reseeded from the baked snapshot and wiped on every boot.** |
| `logs/`    | `/home/linuxgsm/log`            | **Cleared on every boot.** |

Permissions / admin seed stay baked in the image and are reapplied on every boot
(so they are fresh even on a plain `docker restart`, not just a recreate).

## Quick start

```powershell
# 1. (first time) copy env
Copy-Item .env.example .env       # tweak UID/GID/RCON if needed

# 2. Build the image(s) you want (large, one-time / monthly)
docker compose build carbon-release
docker compose --profile all build          # all four

# 3. Run a subset
docker compose up -d carbon-release
docker compose --profile release up -d      # both release
docker compose --profile all     up -d      # all four

# 4. Deploy plugins -> servers\rust-carbon-release\plugins\  (your deploy script)

# 5. Connect (in the Rust client F1 console)
#    client.connect 127.0.0.1:28200   # carbon-release
#    client.connect 127.0.0.1:28210   # oxide-release
```

## Ports

127.0.0.1 only. Per instance: `game=BASE/udp  rcon=BASE+1/tcp  query=BASE+2/udp  app=BASE+3/tcp`.

| Instance        | BASE  |
|-----------------|-------|
| carbon-release  | 28200 |
| oxide-release   | 28210 |
| carbon-staging  | 28220 |
| oxide-staging   | 28230 |

## Reset

```powershell
.\reset.ps1 carbon-release          # recreate fresh (plugins preserved)
.\reset.ps1 carbon-release -Hard    # also wipe host plugins/config/logs
```

A plain reset recreates the container: fresh baked game + mod, reseeded
config/perms, cleared logs, new world save. Your deployed plugins survive unless
you pass `-Hard`.

## Rebuild after a wipe / game update

`start.ps1` uses cached layers, so after a Rust wipe (game + mod updated, but the
Dockerfile unchanged) it won't pull anything new. Use `rebuild.ps1` from the
project root to force a fresh bake (re-download game, reinstall mod, redo staging
overlay, regenerate map), then recreate the containers:

```powershell
.\rebuild.ps1                            # all four, fresh bake
.\rebuild.ps1 -Mod Carbon -Branch Staging
.\rebuild.ps1 -NoCache                   # nuke ALL cache incl. OS/base layers
```

It bumps a `CACHE_BUST` build arg to invalidate only the bake layers while
keeping the OS/LinuxGSM base cached (faster than `--no-cache`).

## Extra instances

```bash
./run-extra.sh carbon-b carbon release 28240
```

Reuses the baked `rust-test-env:carbon-release` image (builds it if missing),
its own `./servers/rust-carbon-b/` mounts, ports 28240-28243.

## Status

```powershell
.\status.ps1     # baked images + all rust- containers and their state/ports
```

> **Superseded:** this Docker flow has been replaced by the local-install
> scripts in the parent `test-env\` folder (`install.ps1`/`start.ps1`/...).
> See `..\README.md`. This folder is kept for reference.

## Files

| File               | Purpose                                                        |
|--------------------|----------------------------------------------------------------|
| `Dockerfile`       | Thin shared base, then per-(MOD x BRANCH) bake via build args. |
| `build-install.sh` | Build-time installer: game + mod + staging overlay + seed snapshot. |
| `entrypoint.sh`    | Stateless boot: reseed config/perms, clear logs, render config, sync plugins, launch. |
| `docker-compose.yml` | Four profile-gated services, per-server mounts, no volumes.   |
| `run-extra.sh`     | Ad-hoc extra instance from a baked image.                      |
| `reset.ps1` / `status.ps1` | Recreate-fresh / overview.                             |
| `servers/`         | Per-server `plugins` / `config` / `logs` mounts.               |

## Notes / gotchas (carried over)

- `docker compose exec` needs `-u linuxgsm` (container default user is root for
  the boot-time chown of mount targets).
- LinuxGSM v26 `mods-install` ignores its positional arg and reads the mod name
  on stdin -- handled in `build-install.sh`.
- `WORLD_SIZE >= 2000` avoids `InsideTerrain` spawn kicks; anti-cheat left ON.
- Editing tools can choke on em-dash chars / truncate -- these files are written
  via shell heredocs, plain ASCII.
- Keep these images local: baking Facepunch server binaries into an image you
  push to a public registry is a redistribution question.
