# =============================================================================
# Rust (Facepunch) dedicated test server -- ONE framework x branch baked in.
#
# The game install + mod framework + (for staging) the staging overlay + a
# PRE-GENERATED MAP are all baked at BUILD time. The running container is
# stateless: no SteamCMD download on boot, no named volume, no ownership dance.
# Reset = recreate the container -> guaranteed fresh.
#
# One image per (framework x branch); docker-compose.yml passes MOD/RUST_BRANCH
# as build args and tags each image rust-test-env:<mod>-<branch>.
#
# Layers before the first `ARG MOD` are identical across all four builds, so the
# heavy base (apt deps + LinuxGSM preload) is cached/shared. The bake diverges
# per (mod x branch) below.
# =============================================================================
FROM ubuntu:24.04

# Build-time UID/GID for the in-container linuxgsm user (same for all four).
ARG UID=1000
ARG GID=1000

ENV DEBIAN_FRONTEND=noninteractive \
    LANG=en_US.UTF-8 \
    LGSM_GAMESERVER=rustserver

# LinuxGSM + Rust runtime deps. Rust/SteamCMD are 32-bit-linked (i386 libs).
RUN dpkg --add-architecture i386 \
    && apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates curl wget file tar bzip2 gzip unzip xz-utils cpio pigz \
        bsdmainutils python3 util-linux binutils bc jq tmux distro-info \
        netcat-openbsd iproute2 uuid-runtime procps ncurses-bin locales rsync \
        lib32gcc-s1 lib32stdc++6 lib32z1 libsdl2-2.0-0:i386 \
    && locale-gen en_US.UTF-8 \
    && rm -rf /var/lib/apt/lists/*

# LinuxGSM refuses to run as root. Free UID/GID 1000 (Ubuntu 24.04 ships an
# 'ubuntu' user there) then create our linuxgsm user to claim it.
RUN set -eux; \
    if getent passwd ${UID} >/dev/null; then userdel -rf "$(getent passwd ${UID} | cut -d: -f1)" || true; fi; \
    if getent group  ${GID} >/dev/null; then groupdel    "$(getent group  ${GID} | cut -d: -f1)" || true; fi; \
    groupadd -g ${GID} linuxgsm; \
    useradd -u ${UID} -g ${GID} -m -d /home/linuxgsm -s /bin/bash linuxgsm

USER linuxgsm
WORKDIR /home/linuxgsm
RUN mkdir -p /home/linuxgsm/serverfiles /home/linuxgsm/seed

# Preload LinuxGSM + the rustserver instance scripts (no game download yet).
RUN wget -O linuxgsm.sh https://linuxgsm.sh \
    && chmod +x linuxgsm.sh \
    && ./linuxgsm.sh rustserver

# ----------------------------------------------------------------------------
# Everything ABOVE is identical across the four images (shared cache). Below,
# the per-(mod x branch) bake begins. Declaring these ARGs here (not earlier)
# keeps the base layers shareable.
# ----------------------------------------------------------------------------
ARG MOD=carbon
ARG RUST_BRANCH=release
ARG OXIDE_STAGING_URL=https://downloads.oxidemod.com/artifacts/Oxide.Rust/staging/Oxide.Rust-linux.zip

# Map params. The map is PRE-GENERATED at build time, so these MUST match the
# runtime values (docker-compose passes the same .env values as build args).
# Change seed/size/level -> rebuild to re-bake the map. SERVER_LEVEL default is
# empty here; build-install.sh applies "Procedural Map" when empty.
ARG SEED=1337
ARG WORLD_SIZE=3000
ARG SERVER_LEVEL=
ARG PREGEN_MAP=true
# Bump to force the bake to re-run (game+mod+staging+map) while keeping the
# OS/LinuxGSM base layers cached. rebuild.ps1 sets this to a timestamp.
ARG CACHE_BUST=0

# Bake the framework/branch identity into the image so the entrypoint trusts it
# regardless of what env a container is launched with.
ENV BAKED_MOD=${MOD} \
    BAKED_BRANCH=${RUST_BRANCH}

# Install game + mod + (staging overlay) + pre-generated map + seed snapshot.
COPY --chown=linuxgsm:linuxgsm build-install.sh /home/linuxgsm/build-install.sh
RUN chmod +x /home/linuxgsm/build-install.sh \
    && MOD="${MOD}" RUST_BRANCH="${RUST_BRANCH}" \
       OXIDE_STAGING_URL="${OXIDE_STAGING_URL}" \
       SEED="${SEED}" WORLD_SIZE="${WORLD_SIZE}" SERVER_LEVEL="${SERVER_LEVEL}" \
       PREGEN_MAP="${PREGEN_MAP}" CACHE_BUST="${CACHE_BUST}" \
       /home/linuxgsm/build-install.sh

COPY --chown=linuxgsm:linuxgsm entrypoint.sh /home/linuxgsm/entrypoint.sh
RUN chmod +x /home/linuxgsm/entrypoint.sh

# game(udp) / query(udp) / rcon(tcp) / Rust+ app(tcp). Published with per-
# instance offsets in docker-compose.yml.
EXPOSE 28015/udp 28017/udp 28016/tcp 28082/tcp

# Start as ROOT so the entrypoint can chown the (small) bind-mount targets
# (config + logs), then drop to linuxgsm via setpriv. The baked install is
# already linuxgsm-owned, so there is NO chown over the multi-GB serverfiles.
USER root
ENTRYPOINT ["/home/linuxgsm/entrypoint.sh"]
