#!/usr/bin/env bash
# =============================================================================
# STATELESS runtime entrypoint -- the game + mod are already baked in the image.
#
# On every boot this script:
#   1. (as root) chowns the small bind-mount targets (config, logs) to linuxgsm,
#      then drops privileges. The baked serverfiles are already linuxgsm-owned,
#      so there is NO chown over the multi-GB install.
#   2. RESEEDS framework config/data/lang from the baked seed/ snapshot, so
#      configs + permissions are pristine on every restart (stateless).
#   3. CLEARS the logs dir so each run starts with fresh logs.
#   4. Renders the LinuxGSM config from env (ports, names, rcon).
#   5. Poll-syncs the read-only plugins mount into the live plugin dir.
#   6. Launches the server with a watchdog + graceful shutdown.
#
# Framework + branch come from the BAKED_* env baked into the image; runtime
# MOD/RUST_BRANCH env is only used to sanity-check, never to re-install.
# =============================================================================
set -euo pipefail

echo "[entrypoint] build=v4-baked  uid=$(id -u)"

LGSM_USER=linuxgsm
HOME_DIR=/home/linuxgsm
SF="${HOME_DIR}/serverfiles"
SEED="${HOME_DIR}/seed"
LOG_DIR="${HOME_DIR}/log"

# Framework is authoritative from the bake. Derive the config dir so we know
# which mount target to chown while we are still root.
BAKED_MOD="${BAKED_MOD:-carbon}"
case "${BAKED_MOD,,}" in
    carbon|rustcarbon)    _FW="carbon"; _CFGSUB="configs" ;;
    oxide|umod|rustoxide) _FW="oxide";  _CFGSUB="config"  ;;
    *)                    _FW="";       _CFGSUB="" ;;
esac
CONFIG_DIR="${SF}/${_FW}/${_CFGSUB}"

# -----------------------------------------------------------------------------
# Privilege bootstrap: root only long enough to make the bind-mount dirs
# writable by linuxgsm, then re-exec as linuxgsm.
# -----------------------------------------------------------------------------
if [ "$(id -u)" = "0" ]; then
    GS_UID="$(id -u ${LGSM_USER})"; GS_GID="$(id -g ${LGSM_USER})"
    mkdir -p "${LOG_DIR}/script" "${LOG_DIR}/console" "${LOG_DIR}/server"
    [ -n "${_FW}" ] && mkdir -p "${CONFIG_DIR}"
    # Only the small mount targets -- NOT the baked install.
    chown -R "${GS_UID}:${GS_GID}" "${LOG_DIR}" 2>/dev/null || true
    [ -n "${_FW}" ] && chown -R "${GS_UID}:${GS_GID}" "${CONFIG_DIR}" 2>/dev/null || true
    exec setpriv --reuid "${GS_UID}" --regid "${GS_GID}" --init-groups \
        env HOME="${HOME_DIR}" USER="${LGSM_USER}" "$0" "$@"
fi

export HOME="${HOME_DIR}"
cd "${HOME_DIR}"

LGSM=./rustserver
SELFNAME=rustserver

# ----- Tunables (env) --------------------------------------------------------
MOD="${BAKED_MOD}"                          # authoritative from image
RUST_BRANCH="${BAKED_BRANCH:-release}"      # authoritative from image

# If launched with env that disagrees with the bake, warn -- we run the baked one.
[ -n "${MOD_ENV:-}" ] && [ "${MOD_ENV,,}" != "${BAKED_MOD,,}" ] && \
    echo "[entrypoint] WARN: env MOD='${MOD_ENV}' != baked '${BAKED_MOD}'; running baked framework."
PORT_BASE="${PORT_BASE:-28015}"


SERVER_NAME="${SERVER_NAME:-Rust Test [${MOD}/${RUST_BRANCH}]}"
RCON_PASSWORD="${RCON_PASSWORD:-changeme_local_only}"
MAX_PLAYERS="${MAX_PLAYERS:-10}"
WORLD_SIZE="${WORLD_SIZE:-3000}"
SEED_VAL="${SEED:-1337}"
SERVER_LEVEL="${SERVER_LEVEL:-Procedural Map}"
TICKRATE="${TICKRATE:-30}"
EXTRA_START_PARAMS="${EXTRA_START_PARAMS:-}"

PLUGIN_SRC="${PLUGIN_SRC:-/plugins-src}"
PLUGIN_POLL_SECONDS="${PLUGIN_POLL_SECONDS:-2}"

GAME_PORT="${PORT_BASE}"
RCON_PORT="$((PORT_BASE + 1))"
QUERY_PORT="$((PORT_BASE + 2))"
APP_PORT="$((PORT_BASE + 3))"

case "${RUST_BRANCH,,}" in
    release|public|"") LGSM_BRANCH="" ;;
    staging)           LGSM_BRANCH="staging" ;;
    *)                 LGSM_BRANCH="${RUST_BRANCH}" ;;
esac

FRAMEWORK="${_FW}"
CFG_DIR="${HOME_DIR}/lgsm/config-lgsm/rustserver"
COMMON_CFG="${CFG_DIR}/common.cfg"
CONSOLE_LOG="${LOG_DIR}/console/${SELFNAME}-console.log"

log() { echo "[entrypoint] $*"; }

mkdir -p "${LOG_DIR}/script" "${LOG_DIR}/console" "${LOG_DIR}/server"
if [ ! -f "${SF}/RustDedicated" ]; then
    log "FATAL: baked install missing (no RustDedicated). Rebuild this image."
    exit 1
fi

# ----- 1. Reseed framework config/data/lang (pristine every boot) ------------
reseed_dir() {
    # $1 = subdir name under the framework (configs|config|data|lang)
    local sub="$1"
    local src="${SEED}/${FRAMEWORK}/${sub}"
    local dst="${SF}/${FRAMEWORK}/${sub}"
    [ -d "${src}" ] || return 0
    mkdir -p "${dst}"
    rsync -a --delete "${src}/" "${dst}/" 2>/dev/null || true
}
if [ -n "${FRAMEWORK}" ] && [ -d "${SEED}/${FRAMEWORK}" ]; then
    log "Reseeding ${FRAMEWORK} config/data/lang from baked snapshot (stateless)."
    reseed_dir "${_CFGSUB}"
    reseed_dir data
    reseed_dir lang
fi

# ----- 2. Clear logs (fresh each run) ----------------------------------------
log "Clearing logs for a fresh run."
rm -rf "${LOG_DIR}/console/"* "${LOG_DIR}/server/"* "${LOG_DIR}/script/"* 2>/dev/null || true
mkdir -p "${LOG_DIR}/script" "${LOG_DIR}/console" "${LOG_DIR}/server"

# ----- 3. Render LinuxGSM config ---------------------------------------------
mkdir -p "${CFG_DIR}"
cat > "${COMMON_CFG}" <<CFG
# Auto-generated by entrypoint.sh each boot -- edit env vars, not this file.
servername="${SERVER_NAME}"
rconpassword="${RCON_PASSWORD}"
maxplayers="${MAX_PLAYERS}"
worldsize="${WORLD_SIZE}"
seed="${SEED_VAL}"
serverlevel="${SERVER_LEVEL}"
tickrate="${TICKRATE}"
ip="0.0.0.0"
displayip="127.0.0.1"
port="${GAME_PORT}"
rconport="${RCON_PORT}"
queryport="${QUERY_PORT}"
appport="${APP_PORT}"
branch="${LGSM_BRANCH}"
betapassword=""
updateonstart="off"
startparameters="-batchmode +app.listenip \${ip} +app.port \${appport} +server.ip \${ip} +server.port \${port} +server.queryport \${queryport} +server.tickrate \${tickrate} +server.hostname \"\${servername}\" +server.identity \"\${selfname}\" +server.gamemode \${gamemode} +server.level \"\${serverlevel}\" +server.seed \${seed} +server.salt \${salt} +server.maxplayers \${maxplayers} +server.worldsize \${worldsize} +server.saveinterval \${saveinterval} +rcon.web \${rconweb} +rcon.ip \${ip} +rcon.port \${rconport} +rcon.password \"\${rconpassword}\" +server.tags \${tags} ${EXTRA_START_PARAMS} -logfile \${gamelog}"
CFG

log "Instance: MOD=${MOD} BRANCH=${RUST_BRANCH} | game=${GAME_PORT}/udp rcon=${RCON_PORT}/tcp query=${QUERY_PORT}/udp app=${APP_PORT}/tcp"

# ----- 4. Plugin poll-sync (read-only Windows mount -> live plugin dir) -------
PLUGIN_DST=""
SYNC_PID=""
sync_plugins_once() {
    [ -n "${PLUGIN_DST}" ] || return 0
    mkdir -p "${PLUGIN_DST}"
    rsync -a --delete --exclude='.gitkeep' "${PLUGIN_SRC}/" "${PLUGIN_DST}/" 2>/dev/null || true
}
if [ -n "${FRAMEWORK}" ] && [ -d "${PLUGIN_SRC}" ]; then
    PLUGIN_DST="${SF}/${FRAMEWORK}/plugins"
    log "Syncing plugins ${PLUGIN_SRC} -> ${PLUGIN_DST} (poll ${PLUGIN_POLL_SECONDS}s)."
    sync_plugins_once
    ( while true; do sync_plugins_once; sleep "${PLUGIN_POLL_SECONDS}"; done ) &
    SYNC_PID=$!
elif [ -n "${FRAMEWORK}" ]; then
    log "No ${PLUGIN_SRC} mount found -- skipping plugin sync."
fi

# ----- 4b. Export managed assemblies to the refs mount (plugin build refs) ---
# Copied every boot so the host-side refs always match the running build.
REFS_OUT="/refs-out"
if [ -d "${REFS_OUT}" ]; then
    log "Exporting managed assemblies to ${REFS_OUT}."
    rsync -a --delete "${SF}/RustDedicated_Data/Managed/" "${REFS_OUT}/" 2>/dev/null || true
    if [ "${FRAMEWORK}" = "carbon" ] && [ -d "${SF}/carbon/managed" ]; then
        rsync -a "${SF}/carbon/managed/" "${REFS_OUT}/" 2>/dev/null || true
    fi
fi

# ----- 5. Launch with watchdog + graceful shutdown ---------------------------
mkdir -p "$(dirname "${CONSOLE_LOG}")"
touch "${CONSOLE_LOG}"

MON_PID=""
shutdown() {
    log "Stop signal -- saving and shutting down."
    [ -n "${SYNC_PID}" ] && kill "${SYNC_PID}" 2>/dev/null || true
    [ -n "${MON_PID}" ] && kill "${MON_PID}" 2>/dev/null || true
    ${LGSM} stop || true
    exit 0
}
trap shutdown SIGTERM SIGINT

log "Starting ${SELFNAME} (${SERVER_NAME})."
${LGSM} start || log "WARN: 'start' returned non-zero -- see console output below."

# Watchdog: restart the server if it actually crashes, but give first-boot map
# generation a grace period before second-guessing.
(
    sleep 180
    while true; do ${LGSM} monitor >/dev/null 2>&1 || true; sleep 60; done
) &
MON_PID=$!

tail -n +1 -F "${CONSOLE_LOG}" &
TAIL_PID=$!
wait "${TAIL_PID}"
