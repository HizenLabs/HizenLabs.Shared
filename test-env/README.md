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

## Scripts

| Script | What it does |
|--------|--------------|
| `config.ps1`  | Create/open your git-ignored `Local.config.ps1`; `-Apply` seeds owners into installs. |
| `install.ps1` | Install/update game + mod for the selected instances and export compile refs. `-Force` re-validates. |
| `start.ps1`   | Launch selected instances, each in its own console window. Seeds owners on every start. |
| `stop.ps1`    | Stop selected instances (by tracked PID). |
| `wipe.ps1`    | Fresh **world**: delete save/map/identity; keeps install + plugins. `-Reinstall` re-pulls game+mod. |
| `clean.ps1`   | Dev **refresh**: remove deployed plugins + their configs/data/lang; keeps the world. `-KeepPlugins` to spare plugins. |

The **server console** is just the window `start.ps1` opens -- type commands there.
Closing it (or `stop.ps1`) stops that server; for a clean save type `quit` first.

## Config

`Local.config.ps1` (git-ignored, created from `Local.config.example.ps1`) holds
your `Owners`/`Moderators` SteamID64s, rcon password and world defaults (size,
seed, max players, level, tickrate) plus the Carbon release channel. Owners are
written to each server's `users.cfg` on every `start.ps1`.

## How the plugin build ties in

`install.ps1` exports each instance's managed assemblies to
`servers\rust-<mod>-<branch>\refs`, which is what `Directory.Build.props` points
`RustManagedDir` at -- so a Release/Carbon build compiles against the
carbon-release install, etc. The deploy target copies bundled plugins into that
install's `server\<mod>\plugins`, where the running server hot-reloads them.
