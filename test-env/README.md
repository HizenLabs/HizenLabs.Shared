# Local test-env

Local Windows Rust test servers for plugin development. Same matrix as before --
`{carbon, oxide} x {release, staging}`, four instances on the same ports -- but
each is a plain on-disk install under `.\servers\`, no Docker.

> The `docker\` folder (and `rebuild.ps1`) is the **old container flow**, kept for
> reference. The scripts below replace it. Nothing under `servers\`, `.steamcmd\`
> or `Local.config.ps1` is committed (see `.gitignore`).

## First run

```powershell
cd shared\test-env

.\config.ps1                               # create Local.config.ps1 + open it
#   -> add your SteamID64 to Owners, save

.\install.ps1 -Mod Carbon -Branch Release  # SteamCMD + Carbon + export refs
.\start.ps1   -Mod Carbon -Branch Release  # launches in its own console window
```

Drop `-Mod`/`-Branch` to act on all four instances. `install.ps1` accepts the
same params; `start.ps1 -Install` installs first if missing.

Connect from the Rust client F1 console: `client.connect 127.0.0.1:<game-port>`
| instance | game | rcon | query | app |
|----------|------|------|-------|-----|
| carbon-release | 28200 | 28201 | 28202 | 28203 |
| oxide-release  | 28210 | 28211 | 28212 | 28213 |
| carbon-staging | 28220 | 28221 | 28222 | 28223 |
| oxide-staging  | 28230 | 28231 | 28232 | 28233 |
| carbon-debug   | 28240 | 28241 | 28242 | 28243 |

`carbon-debug` is an **opt-in** extra instance (`-Branch Debug`) that runs your
**local Carbon build** with the Mono debugger on — see "Debug Carbon itself" below.
It is deliberately excluded from `All`, so a bare `start.ps1`/`install.ps1` never
touches it.

## Scripts

| Script | What it does |
|--------|--------------|
| `config.ps1`  | Create/open your git-ignored `Local.config.ps1`; `-Apply` seeds owners into installs. |
| `install.ps1` | Install/update game + mod for the selected instances and export compile refs. `-Force` re-validates. |
| `start.ps1`   | Launch selected instances, each in its own console window. Seeds owners on every start. |
| `stop.ps1`    | Stop selected instances (by tracked PID). |
| `wipe.ps1`    | Fresh **world**: delete save/map/identity; keeps install + plugins. `-Reinstall` re-pulls game+mod. |
| `clean.ps1`   | Dev **refresh**: remove deployed plugins + their configs/data/lang; keeps the world. `-KeepPlugins` to spare plugins. |
| `redeploy.ps1`| **Debug loop**: `-Build` builds Carbon then stop/deploy/restart `carbon-debug` (one command); bare/`-Restart` redeploys an already-built overlay. Keeps the Mono debugger on. |

The **server console** is just the window `start.ps1` opens -- type commands there.
Closing it (or `stop.ps1`) stops that server; for a clean save type `quit` first.

## Debug Carbon itself

`carbon-debug` runs a Carbon build **you compiled locally**, with the Unity/Mono
soft-debugger enabled, so you can attach Visual Studio and step through Carbon
(not just plugins). Carbon is injected into `RustDedicated` via UnityDoorstop, so
this is **attach** debugging — there is no "run Carbon" target.

`carbon-debug` is **always staging**. It runs a local Carbon build from the
`rust_beta/staging` branch against a Rust `-beta staging` server — so the build's
compile define/tag must be staging too (`RUST_STAGING` / `rustbeta_staging_build`),
which is what Carbon CI uses for that branch. `redeploy.ps1 -Build` applies that for
you; **do not** use `build_debug_noarchive.bat` (it hardcodes `EDGE` — wrong for staging,
and the define gates real patch/compile code).

```powershell
# First time, in the Carbon fork (checked out on rust_beta/staging):
#   tools\build\win\bootstrap.bat staging                              # tooling + STAGING refs
#   tools\build\win\build.bat Debug RUST_STAGING rustbeta_staging_build -noarchive
# Then point Local.config.ps1 at the output (default already assumes hizenlabs\carbon\Carbon):
#   CarbonLocalBuildPath = '..\..\..\carbon\Carbon\release\.tmp\Debug'
.\install.ps1 -Branch Debug    # SteamCMD Rust staging + local Carbon + debugger
.\start.ps1   -Branch Debug    # launches on :28240; prints the attach address
#   -> in Visual Studio: Debug > Attach Unity Debugger > add 127.0.0.1:55555
```

Inner loop after a Carbon code change — one command:

```powershell
.\redeploy.ps1 -Build      # build Carbon (RUST_STAGING) -> stop -> deploy -> restart, re-attach VS
```

`-Build` finds the Carbon repo (via `CarbonLocalBuildPath`, or `CarbonRepoPath`) and
runs `build.bat Debug RUST_STAGING rustbeta_staging_build -noarchive` — building *before*
stopping the server, so a failed build never kills a working instance. It compiles
against the Rust refs in the repo's `rust\` folder, so run `tools\build\win\update_staging.bat`
once if your refs aren't staging yet. Without `-Build` it redeploys an already-built
overlay (`-Restart` to bounce it). `CarbonDebugSuspend = $true` freezes boot until you attach.

**Server log.** `carbon-debug` routes output to `logs\server.log` (RustDedicated's
live console otherwise throws a cosmetic `SetConsoleCursorInfo failed`). Tail it:
`Get-Content -Wait -Tail 100 servers\rust-carbon-debug\server\logs\server.log` — and
send server commands via RCON (`:28241`) or the Carbon web panel. To keep the
interactive in-window console instead, set `CarbonDebugLogFile = $false`.

## Config

`Local.config.ps1` (git-ignored, created from `Local.config.example.ps1`) holds
your `Owners`/`Moderators` SteamID64s, rcon password and world defaults (size,
seed, max players, level, tickrate) plus the Carbon release channel. Owners are
written to each server's `users.cfg` on every `start.ps1`. For `carbon-debug` it
also holds `CarbonLocalBuildPath` (where your local Carbon build is) and
`CarbonDebugAddress` / `CarbonDebugSuspend` (the Mono debugger endpoint).

## How the plugin build ties in

`install.ps1` exports each instance's managed assemblies to
`servers\rust-<mod>-<branch>\refs`, which is what `Directory.Build.props` points
`RustManagedDir` at -- so a Release/Carbon build compiles against the
carbon-release install, etc. The deploy target copies bundled plugins into that
install's `server\<mod>\plugins`, where the running server hot-reloads them.
