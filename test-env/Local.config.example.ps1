# =============================================================================
# Local test-env config -- EDITABLE DEFAULTS for the start/install scripts.
#
# This .example file is committed. Your real config lives next to it as
# Local.config.ps1, which is git-ignored (so your SteamID etc. never get pushed
# to the public repo). Create it with:   .\config.ps1
#
# It's just a PowerShell hashtable -- edit values, save, restart the server.
# =============================================================================
@{
    # SteamID64s granted owner (auth level 2 -- full F1 console) on every local
    # server. Applied to users.cfg on each start.
    Owners = @(
        # '76561190000000000'   # <- your SteamID64
    )

    # SteamID64s granted moderator (auth level 1).
    Moderators = @()

    # Seeded into every server on start.
    RconPassword = 'changeme_local_only'
    MaxPlayers   = 10
    WorldSize    = 3000
    Seed         = 1337
    ServerLevel  = 'Procedural Map'
    Tickrate     = 30

    # Carbon channel used for the 'release' branch install: Production | Edge.
    # ('Preview' is gone -- Carbon stopped publishing preview_build.)
    # ('staging' branch always uses Carbon's rustbeta_staging_build to match the
    # staging game build -- this setting does not affect it.)
    CarbonReleaseChannel = 'Production'

    # --- Debug instance (carbon-debug, -Branch Debug) ----------------------
    # Always the Rust STAGING channel: it runs a LOCAL Carbon staging build with the
    # Mono debugger on, so you can attach Visual Studio and step Carbon itself. Build
    # your Carbon fork ON the rust_beta/staging branch, then:
    #   .\install.ps1 -Branch Debug   ->   .\start.ps1 -Branch Debug   -> attach
    #   .\redeploy.ps1 -Build         # build (RUST_STAGING) + deploy + restart, each iteration
    #
    # Path to the build overlay (release\.tmp\Debug). Relative paths resolve from this
    # test-env folder; the default assumes the Carbon fork sits beside the plugins repo.
    CarbonLocalBuildPath = '..\..\..\carbon\Carbon\release\.tmp\Debug'

    # Optional: Carbon repo root, used by `redeploy.ps1 -Build` to run the build.
    # Leave unset to auto-derive it from CarbonLocalBuildPath (walks up to the folder
    # containing tools\build\win\build.bat).
    # CarbonRepoPath = '..\..\..\carbon\Carbon'

    # Mono soft-debugger endpoint baked into carbon-debug's doorstop_config.ini --
    # attach Visual Studio (Tools for Unity) here. Suspend freezes boot until a
    # debugger attaches (use it to catch early-init/bootstrap bugs).
    CarbonDebugAddress = '127.0.0.1:55555'
    CarbonDebugSuspend = $false
}
