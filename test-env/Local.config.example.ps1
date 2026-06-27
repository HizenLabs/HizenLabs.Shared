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

    # Carbon channel used for the 'release' branch install: Production | Preview | Edge.
    # ('staging' branch always uses Carbon's rustbeta_staging_build to match the
    # staging game build -- this setting does not affect it.)
    CarbonReleaseChannel = 'Production'

    # --- Debug instance (carbon-debug, -Branch Debug) ----------------------
    # Runs a LOCAL Carbon build with the Mono debugger on, so you can attach
    # Visual Studio and step Carbon itself. Build the overlay in the Carbon repo
    # (tools\build\win\build_debug_noarchive.bat -> release\.tmp\Debug), then:
    #   .\install.ps1 -Branch Debug   ->   .\start.ps1 -Branch Debug   -> attach
    #   .\redeploy.ps1 -Restart       # fast loop after each rebuild
    #
    # Path to that overlay. Relative paths resolve from this test-env folder; the
    # default assumes the Carbon fork sits beside the plugins repo under hizenlabs\.
    CarbonLocalBuildPath = '..\..\..\carbon\Carbon\release\.tmp\Debug'

    # Optional: Carbon repo root, used by `redeploy.ps1 -Build` to run the build.
    # Leave unset to auto-derive it from CarbonLocalBuildPath (walks up to the
    # folder containing tools\build\win\build_debug_noarchive.bat).
    # CarbonRepoPath = '..\..\..\carbon\Carbon'

    # Rust game branch carbon-debug installs -- MUST match the Rust channel your
    # local Carbon build targets, or the game assemblies/hooks won't line up:
    #   Carbon fork on rust_beta/staging -> 'staging'    (default)
    #   Carbon fork on main              -> 'public'
    CarbonDebugGameBranch = 'staging'

    # Mono soft-debugger endpoint baked into carbon-debug's doorstop_config.ini --
    # attach Visual Studio (Tools for Unity) here. Suspend freezes boot until a
    # debugger attaches (use it to catch early-init/bootstrap bugs).
    CarbonDebugAddress = '127.0.0.1:55555'
    CarbonDebugSuspend = $false
}
