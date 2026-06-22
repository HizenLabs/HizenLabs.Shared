#!/usr/bin/env bash
# =============================================================================
# Spin up an EXTRA Rust instance beyond the 4 in docker-compose.yml -- e.g. a
# second carbon-release for server-to-server / dual-server plugin testing.
#
# Reuses the BAKED per-combo image (rust-test-env:<framework>-<branch>); builds
# it first if it does not exist yet. Stateless like the rest: no volume, its own
# per-server mounts under ./servers/rust-<name>/, 127.0.0.1-only ports.
#
# Usage:  ./run-extra.sh <name> <carbon|oxide> <release|staging> <port_base>
# Example (second carbon on release, ports 28240-28243):
#   ./run-extra.sh carbon-b carbon release 28240
#
# Manage:  docker logs -f rust-carbon-b
#          docker rm -f rust-carbon-b
#          ./reset.ps1 carbon-b -Hard      (or just rm -f + re-run this)
# =============================================================================
set -euo pipefail

NAME="${1:?name required}"
FRAMEWORK="${2:?framework required: carbon|oxide}"
BRANCH="${3:?branch required: release|staging}"
PORT_BASE="${4:?port_base required, e.g. 28240}"

HERE="$(cd "$(dirname "$0")" && pwd)"
[ -f "${HERE}/.env" ] && set -a && . "${HERE}/.env" && set +a

case "${FRAMEWORK}" in
    carbon) CFG_TARGET="/home/linuxgsm/serverfiles/carbon/configs" ;;
    oxide)  CFG_TARGET="/home/linuxgsm/serverfiles/oxide/config" ;;
    *) echo "framework must be carbon or oxide"; exit 1 ;;
esac
case "${BRANCH}" in
    release|staging) : ;;
    *) echo "branch must be release or staging"; exit 1 ;;
esac

IMAGE="rust-test-env:${FRAMEWORK}-${BRANCH}"
CONTAINER="rust-${NAME}"
SRV="${HERE}/servers/${CONTAINER}"

# Build the baked image if it is not present yet.
if ! docker image inspect "${IMAGE}" >/dev/null 2>&1; then
    echo "Image ${IMAGE} not found -- building it (one-time, large)."
    docker build \
        --build-arg UID="${UID:-1000}" --build-arg GID="${GID:-1000}" \
        --build-arg MOD="${FRAMEWORK}" --build-arg RUST_BRANCH="${BRANCH}" \
        -t "${IMAGE}" "${HERE}"
fi

mkdir -p "${SRV}/plugins" "${SRV}/config" "${SRV}/logs"

docker rm -f "${CONTAINER}" 2>/dev/null || true
docker run -d --name "${CONTAINER}" --restart unless-stopped --stop-timeout 90 \
  -e PORT_BASE="${PORT_BASE}" \
  -e SERVER_NAME="${FRAMEWORK^} - ${BRANCH} - ${NAME} - :${PORT_BASE}" \
  -e RCON_PASSWORD="${RCON_PASSWORD:-changeme_local_only}" \
  -e MAX_PLAYERS="${MAX_PLAYERS:-10}" \
  -e WORLD_SIZE="${WORLD_SIZE:-3000}" \
  -e SEED="${SEED:-1337}" \
  -e SERVER_LEVEL="${SERVER_LEVEL:-Procedural Map}" \
  -p "127.0.0.1:${PORT_BASE}:${PORT_BASE}/udp" \
  -p "127.0.0.1:$((PORT_BASE+1)):$((PORT_BASE+1))/tcp" \
  -p "127.0.0.1:$((PORT_BASE+2)):$((PORT_BASE+2))/udp" \
  -p "127.0.0.1:$((PORT_BASE+3)):$((PORT_BASE+3))/tcp" \
  -v "${SRV}/plugins:/plugins-src:ro" \
  -v "${SRV}/config:${CFG_TARGET}" \
  -v "${SRV}/logs:/home/linuxgsm/log" \
  "${IMAGE}"

echo "Started ${CONTAINER}: ${FRAMEWORK}/${BRANCH}"
echo "  game ${PORT_BASE}/udp  rcon $((PORT_BASE+1))/tcp  query $((PORT_BASE+2))/udp  app $((PORT_BASE+3))/tcp  (127.0.0.1)"
echo "  mounts: ${SRV}/{plugins,config,logs}"
echo "  logs: docker logs -f ${CONTAINER}"
