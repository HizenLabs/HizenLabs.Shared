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
}
