#!/usr/bin/env bash
# =============================================================================
# BUILD-TIME installer (runs during `docker build`, as the linuxgsm user).
#
# Bakes a specific (framework x branch) into the image so the running container
# is stateless: no SteamCMD download, no mods-install, no named volume, and --
# crucially -- a PRE-GENERATED MAP so runtime boots load the cached map instead
# of regenerating it every time.
#
#   docker build -> this script -> game install + mod install + staging overlay
#                                   + pre-generate map (boot once) + seed snapshot
#
# Env passed from the Dockerfile ARGs:
#   MOD          carbon | oxide | vanilla
#   RUST_BRANCH  release | staging
#   SEED / WORLD_SIZE / SERVER_LEVEL   map params (MUST match runtime to reuse
#                                      the baked map; change them -> rebuild)
#   PREGEN_MAP   true|false  (bake the map by booting once; default true)
#   OXIDE_STAGING_URL  (Oxide staging overlay source)
# =============================================================================
set -euo pipefail

MOD="${MOD:-carbon}"
RUST_BRANCH="${RUST_BRANCH:-release}"
SEED="${SEED:-1337}"
WORLD_SIZE="${WORLD_SIZE:-3000}"
SERVER_LEVEL="${SERVER_LEVEL:-Procedural Map}"
PREGEN_MAP="${PREGEN_MAP:-true}"
OXIDE_STAGING_URL="${OXIDE_STAGING_URL:-https://downloads.oxidemod.com/artifacts/Oxide.Rust/staging/Oxide.Rust-linux.zip}"

HOME=/home/linuxgsm
cd "${HOME}"
LGSM=./rustserver
SF="${HOME}/serverfiles"
SEED_DIR="${HOME}/seed"
CFG_DIR="${HOME}/lgsm/config-lgsm/rustserver"
LOG_DIR="${HOME}/log"

log() { echo "[build-install] $*"; }

# ----- Resolve framework + branch (same mapping as entrypoint) ---------------
case "${RUST_BRANCH,,}" in
    release|public|"") LGSM_BRANCH="" ;;
    staging)           LGSM_BRANCH="staging" ;;
    *)                 LGSM_BRANCH="${RUST_BRANCH}" ;;
esac

case "${MOD,,}" in
    carbon|rustcarbon)    FRAMEWORK="carbon"; MODNAME="rustcarbon" ;;
    oxide|umod|rustoxide) FRAMEWORK="oxide";  MODNAME="rustoxide" ;;
    vanilla|none|"")      FRAMEWORK="";       MODNAME="" ;;
    *) echo "[build-install] Unknown MOD='${MOD}'"; exit 1 ;;
esac

log "Baking MOD=${MOD} BRANCH=${RUST_BRANCH} (framework='${FRAMEWORK}' lgsm_branch='${LGSM_BRANCH}')"

# ----- Minimal LinuxGSM config so auto-install pulls the right branch --------
mkdir -p "${CFG_DIR}"
cat > "${CFG_DIR}/common.cfg" <<CFG
# Build-time stub. Runtime entrypoint overwrites this with the full config.
branch="${LGSM_BRANCH}"
betapassword=""
updateonstart="off"
CFG

# ----- 1. Install the game (branch-specific) ---------------------------------
log "Installing Rust dedicated server (SteamCMD${LGSM_BRANCH:+ -beta ${LGSM_BRANCH}}). Large download."
${LGSM} auto-install

if [ ! -f "${SF}/RustDedicated" ]; then
    echo "[build-install] FATAL: RustDedicated missing after auto-install."
    exit 1
fi

# ----- 2. Install the mod framework ------------------------------------------
resolve_carbon_staging_url() {
    local json url
    json="$(curl -fsSL -H 'Accept: application/vnd.github+json' \
        "https://api.github.com/repos/CarbonCommunity/Carbon/releases/tags/rustbeta_staging_build")" || return 1
    url="$(printf '%s' "$json" | jq -r '.assets[] | select(.name|test("Carbon\\.Linux.*Release.*tar\\.gz")) | .browser_download_url' | head -n1)" || url=""
    if [ -z "$url" ] || [ "$url" = "null" ]; then
        url="$(printf '%s' "$json" | jq -r '.assets[] | select(.name|test("Carbon\\.Linux.*tar\\.gz")) | .browser_download_url' | head -n1)" || url=""
    fi
    if [ -z "$url" ] || [ "$url" = "null" ]; then
        echo "[build-install] No Carbon Linux tarball on rustbeta_staging_build. Assets:" >&2
        printf '%s' "$json" | jq -r '.assets[].name' >&2 || true
        return 1
    fi
    printf '%s' "$url"
}

# Carbon staging MUST match the staging game build, so a failure here is FATAL.
overlay_carbon_staging() {
    log "Resolving Carbon STAGING asset from GitHub API."
    local url
    url="$(resolve_carbon_staging_url)" || { echo "[build-install] FATAL: cannot resolve Carbon staging asset."; exit 1; }
    log "Carbon staging asset: ${url}"
    curl -fSL "${url}" -o /tmp/carbon-staging.tar.gz
    tar -xzf /tmp/carbon-staging.tar.gz -C "${SF}"
    rm -f /tmp/carbon-staging.tar.gz
    log "Carbon staging overlay applied."
}

# Oxide staging Linux build (official rolling artifact on downloads.oxidemod.com).
# Production Oxide will not match the staging game build, so failure here is FATAL.
overlay_oxide_staging() {
    log "Overlaying Oxide STAGING build from: ${OXIDE_STAGING_URL}"
    curl -fSL "${OXIDE_STAGING_URL}" -o /tmp/oxide-staging.zip
    unzip -o /tmp/oxide-staging.zip -d "${SF}" >/dev/null
    rm -f /tmp/oxide-staging.zip
    log "Oxide staging overlay applied."
}

if [ -n "${FRAMEWORK}" ]; then
    log "Installing ${MODNAME} via LinuxGSM mods-install."
    printf '%s\nexit\n' "${MODNAME}" | ${LGSM} mods-install \
        || log "WARN: mods-install ${MODNAME} returned non-zero (continuing)."

    if [ "${LGSM_BRANCH}" = "staging" ]; then
        [ "${FRAMEWORK}" = "carbon" ] && overlay_carbon_staging
        [ "${FRAMEWORK}" = "oxide" ]  && overlay_oxide_staging
    fi

    case "${FRAMEWORK}" in
        carbon) [ -d "${SF}/carbon/managed" ] || { echo "[build-install] FATAL: carbon/managed missing."; exit 1; } ;;
        oxide)  ls "${SF}"/RustDedicated_Data/Managed/Oxide.*.dll >/dev/null 2>&1 || { echo "[build-install] FATAL: Oxide DLLs missing."; exit 1; } ;;
    esac
else
    log "Vanilla (no mod framework)."
fi

# ----- 2b. Pre-generate the map (boot once) ----------------------------------
# Boot the server headless until the procedural map is generated and saved, then
# stop. The resulting .map/.sav cache is baked into the image; at runtime the
# server finds it (same seed/size/identity) and loads instead of regenerating.
pregenerate_map() {
    local PB=28015
    local CONSOLE="${LOG_DIR}/console/rustserver-console.log"
    mkdir -p "${LOG_DIR}/script" "${LOG_DIR}/console" "${LOG_DIR}/server"

    log "Pre-generating map: seed=${SEED} worldsize=${WORLD_SIZE} level='${SERVER_LEVEL}'. Booting server once."
    cat > "${CFG_DIR}/common.cfg" <<CFG
servername="Build Map Pregen"
rconpassword="pregen_local_only"
maxplayers="10"
worldsize="${WORLD_SIZE}"
seed="${SEED}"
serverlevel="${SERVER_LEVEL}"
tickrate="30"
ip="0.0.0.0"
displayip="127.0.0.1"
port="${PB}"
rconport="$((PB+1))"
queryport="$((PB+2))"
appport="$((PB+3))"
branch="${LGSM_BRANCH}"
betapassword=""
updateonstart="off"
startparameters="-batchmode +server.ip \${ip} +server.port \${port} +server.queryport \${queryport} +server.tickrate \${tickrate} +server.hostname \"\${servername}\" +server.identity \"\${selfname}\" +server.level \"\${serverlevel}\" +server.seed \${seed} +server.worldsize \${worldsize} +server.maxplayers \${maxplayers} +rcon.web 1 +rcon.ip \${ip} +rcon.port \${rconport} +rcon.password \"\${rconpassword}\" -logfile \${gamelog}"
CFG

    ${LGSM} start || true

    local ok=0 i
    for i in $(seq 1 240); do      # up to ~20 min
        if grep -rqaE "Server startup complete|SteamServer Initialized" "${LOG_DIR}" 2>/dev/null; then ok=1; break; fi
        sleep 5
    done
    if [ "${ok}" = "1" ]; then
        log "Map generated and server up; saving + stopping."
    else
        log "WARN: 'Server startup complete' not seen within timeout; stopping and checking for a map."
    fi
    ${LGSM} stop || true
    sleep 3

    if find "${SF}/server" -name '*.map' 2>/dev/null | grep -q .; then
        log "Baked map cache: $(find "${SF}/server" -name '*.map' | head -n1)"
    else
        echo "[build-install] FATAL: map pre-generation produced no .map cache. Console tail:"
        tail -n 60 "${LOG_DIR}/console/"*.log 2>/dev/null || true
        tail -n 60 "${LOG_DIR}/server/"*.log  2>/dev/null || true
        exit 1
    fi
}

if [ "${PREGEN_MAP,,}" = "true" ]; then
    pregenerate_map
else
    log "PREGEN_MAP=${PREGEN_MAP} -> skipping map bake (runtime will generate on first boot)."
fi

# ----- 3. Snapshot a default config/data/lang "seed" -------------------------
# Done AFTER the pregen boot so the framework's freshly generated default
# configs/data are captured into the seed the entrypoint reseeds from.
mkdir -p "${SEED_DIR}"
if [ -n "${FRAMEWORK}" ]; then
    case "${FRAMEWORK}" in
        carbon) CFG_SUB="configs" ;;
        oxide)  CFG_SUB="config"  ;;
    esac
    for sub in "${CFG_SUB}" data lang; do
        src="${SF}/${FRAMEWORK}/${sub}"
        dst="${SEED_DIR}/${FRAMEWORK}/${sub}"
        mkdir -p "${dst}"
        if [ -d "${src}" ]; then
            cp -a "${src}/." "${dst}/" 2>/dev/null || true
        fi
    done
    log "Seed snapshot written to ${SEED_DIR}/${FRAMEWORK}/ (${CFG_SUB}, data, lang)."
fi

log "Build-time bake complete."
