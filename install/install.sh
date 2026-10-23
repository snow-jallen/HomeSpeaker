#!/usr/bin/env bash
# HomeSpeaker installer for Raspberry Pi OS (Bookworm) and other Debian/Ubuntu systems with systemd.
#
#   curl -fsSL https://raw.githubusercontent.com/snow-jallen/homespeaker/master/install/install.sh | sudo bash
#   curl -fsSL .../install/install.sh | sudo HOMESPEAKER_REF=my-branch HTTP_PORT=8080 bash -s -- --yes --kiosk
#   (flags go after "bash -s --"; variables go between sudo and bash)
#
# Options: --yes  --kiosk  --no-start  --dry-run  --help
# Environment: HOMESPEAKER_REPO (default snow-jallen/homespeaker), HOMESPEAKER_REF (default master),
#              HTTP_PORT (default 80)
set -euo pipefail

REPO="${HOMESPEAKER_REPO:-snow-jallen/homespeaker}"
REF="${HOMESPEAKER_REF:-master}"
RAW_BASE="https://raw.githubusercontent.com/${REPO}/${REF}"
INSTALL_DIR="/opt/homespeaker"
AVAHI_FILE="/etc/avahi/services/homespeaker.service"
SYSTEMD_DIR="/etc/systemd/system"

YES=0
KIOSK=0
NO_START=0
DRY_RUN=0

usage() {
    cat <<'USAGE'
Usage: sudo bash install.sh [options]

  --yes       Accept all defaults (AirPlay receiver on, kiosk off unless --kiosk)
  --kiosk     Set up a Chromium kiosk (labwc autostart) pointing at HomeSpeaker
  --no-start  Do not run "docker compose up" or wait for the health check
  --dry-run   Print every action and file instead of doing it
  --help      Show this help

Environment: HOMESPEAKER_REPO, HOMESPEAKER_REF, HTTP_PORT
USAGE
}

for arg in "$@"; do
    case "$arg" in
        --yes|-y) YES=1 ;;
        --kiosk) KIOSK=1 ;;
        --no-start) NO_START=1 ;;
        --dry-run) DRY_RUN=1 ;;
        --help|-h) usage; exit 0 ;;
        *) echo "Unknown option: $arg" >&2; usage >&2; exit 2 ;;
    esac
done

log() { echo "==> $*"; }
warn() { echo "WARNING: $*" >&2; }
die() { echo "ERROR: $*" >&2; exit 1; }
have() { command -v "$1" >/dev/null 2>&1; }
# True when systemd is running (or when dry-running, so every action is shown).
useSystemd() { [ "$DRY_RUN" = 1 ] || { have systemctl && [ -d /run/systemd/system ]; }; }

# Run a command, or just print it in dry-run mode.
run() {
    if [ "$DRY_RUN" = 1 ]; then
        echo "[dry-run] $*"
    else
        "$@"
    fi
}

# Ask a yes/no question. stdin may be the script itself (curl | bash), so read from the terminal.
# Returns success for yes. With --yes or no terminal the default is used.
ask() {
    local prompt="$1" default="$2" reply="" hint="y/N"
    [ "$default" = y ] && hint="Y/n"
    if [ "$YES" = 1 ] || [ ! -r /dev/tty ]; then
        [ "$default" = y ]
        return
    fi
    read -r -p "$prompt [$hint] " reply </dev/tty || true
    reply="${reply:-$default}"
    case "$reply" in [yY]*) return 0 ;; *) return 1 ;; esac
}

# --- Preconditions ----------------------------------------------------------
if [ "$DRY_RUN" = 1 ]; then
    log "Dry run: nothing will be changed"
elif [ "$(id -u)" -ne 0 ]; then
    die "Run this script with sudo (for example: curl -fsSL ... | sudo bash)."
fi

TARGET_USER="${SUDO_USER:-}"
if [ -z "$TARGET_USER" ] && [ "$DRY_RUN" = 1 ]; then
    TARGET_USER="$(id -un)"
    warn "SUDO_USER is not set; dry-run is using '$TARGET_USER'. A real run requires sudo from a normal user."
elif [ -z "$TARGET_USER" ] || [ "$TARGET_USER" = root ]; then
    die "SUDO_USER is not set. Log in as a normal user and run the script with sudo; do not run it from a root login."
fi
if ! id "$TARGET_USER" >/dev/null 2>&1; then
    die "User '$TARGET_USER' does not exist."
fi

TARGET_UID="$(id -u "$TARGET_USER")"
TARGET_GROUP="$(id -gn "$TARGET_USER")"
TARGET_HOME="$(getent passwd "$TARGET_USER" | cut -d: -f6 || true)"
TARGET_HOME="${TARGET_HOME:-/home/$TARGET_USER}"
HOST_NAME="$(hostname -s 2>/dev/null || hostname)"
HTTP_PORT="${HTTP_PORT:-80}"

validatePort() {
    case "$HTTP_PORT" in
        ''|*[!0-9]*) die "HTTP_PORT must be a number from 1 to 65535 (got '$HTTP_PORT')." ;;
    esac
    if [ "${#HTTP_PORT}" -gt 5 ] || [ "$HTTP_PORT" -lt 1 ] || [ "$HTTP_PORT" -gt 65535 ]; then
        die "HTTP_PORT must be a number from 1 to 65535 (got '$HTTP_PORT')."
    fi
}
validatePort

# Where this script lives (empty when piped from curl); used to prefer local copies of the assets.
SCRIPT_DIR=""
if [ -n "${BASH_SOURCE[0]:-}" ] && [ -f "${BASH_SOURCE[0]}" ]; then
    SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
fi

TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT

# --- Helpers for assets and files ------------------------------------------
# fetch <repo-path> <dest-file>: copy from a local checkout if present, otherwise download.
fetch() {
    local path="$1" dest="$2" localCopy=""
    if [ -n "$SCRIPT_DIR" ]; then
        case "$path" in
            install/*) localCopy="$SCRIPT_DIR/${path#install/}" ;;
            *) localCopy="$SCRIPT_DIR/../$path" ;;
        esac
    fi
    if [ -n "$localCopy" ] && [ -f "$localCopy" ]; then
        cp "$localCopy" "$dest"
    elif have curl; then
        curl -fsSL "$RAW_BASE/$path" -o "$dest"
    else
        return 1
    fi
}

# put <src-file> <dest> <mode> [owner:group]: install a file (print it in dry-run mode).
put() {
    local src="$1" dest="$2" mode="$3" owner="${4:-root:root}"
    if [ "$DRY_RUN" = 1 ]; then
        echo "[dry-run] write $dest (mode $mode, owner $owner)"
        sed 's/^/    | /' "$src"
    else
        install -D -m "$mode" -o "${owner%%:*}" -g "${owner##*:}" "$src" "$dest"
    fi
}

# download <repo-path> <dest> <mode> [owner:group]: fetch an asset and install it.
download() {
    local path="$1" dest="$2" mode="$3" owner="${4:-root:root}" staged
    staged="$TMP_DIR/${path//\//_}"
    if ! fetch "$path" "$staged"; then
        if [ "$DRY_RUN" = 1 ]; then
            echo "[dry-run] would download $RAW_BASE/$path to $dest (curl not available here)"
            return 0
        fi
        die "Could not obtain $path"
    fi
    if [ "$DRY_RUN" = 1 ] && [ "${path%.sh}" != "$path" ]; then
        echo "[dry-run] download $RAW_BASE/$path to $dest (mode $mode, owner $owner)"
        return 0
    fi
    put "$staged" "$dest" "$mode" "$owner"
}

# --- 1. Docker ---------------------------------------------------------------
if ! have docker; then
    log "Installing Docker (get.docker.com)"
    if [ "$DRY_RUN" = 1 ]; then
        echo "[dry-run] curl -fsSL https://get.docker.com | sh"
    else
        have curl || die "curl is required (sudo apt-get install -y curl)."
        curl -fsSL https://get.docker.com | sh
    fi
fi

if have docker; then
    composeVersion="$(docker compose version --short 2>/dev/null || true)"
    composeVersion="${composeVersion#v}"
    if [ -z "$composeVersion" ]; then
        [ "$DRY_RUN" = 1 ] || die "The Docker Compose plugin is missing. Install docker-compose-plugin (2.24 or newer)."
    else
        lowest="$(printf '%s\n2.24.0\n' "$composeVersion" | sort -V | head -1)"
        if [ "$lowest" != "2.24.0" ]; then
            die "Docker Compose $composeVersion is too old; version 2.24 or newer is required. Update the docker-compose-plugin package."
        fi
    fi
fi

log "Adding $TARGET_USER to the docker and audio groups"
for grp in docker audio; do
    if getent group "$grp" >/dev/null 2>&1 || [ "$DRY_RUN" = 1 ]; then
        run usermod -aG "$grp" "$TARGET_USER"
    else
        warn "Group '$grp' does not exist; skipping"
    fi
done

# --- 2. Compose file ---------------------------------------------------------
log "Setting up $INSTALL_DIR"
run install -d -m 755 "$INSTALL_DIR"
download docker-compose.yml "$INSTALL_DIR/docker-compose.yml" 644

# --- 3. .env -----------------------------------------------------------------
ENV_FILE="$INSTALL_DIR/.env"
AIRPLAY=0
if [ -f "$ENV_FILE" ]; then
    log "$ENV_FILE exists; leaving it unchanged"
    if grep -Eq '^COMPOSE_PROFILES=.*airplay' "$ENV_FILE"; then AIRPLAY=1; fi
    existingPort="$(sed -n 's/^HTTP_PORT=//p' "$ENV_FILE" | tail -1)"
    existingPort="$(printf '%s' "$existingPort" | tr -d "'\"")"
    HTTP_PORT="${existingPort:-$HTTP_PORT}"
    validatePort
else
    if ask "Enable the AirPlay receiver (shows this speaker in AirPlay menus)?" y; then AIRPLAY=1; fi
    MUSIC_DIR="$TARGET_HOME/music"
    TZ_VALUE="$(timedatectl show -p Timezone --value 2>/dev/null || true)"
    TZ_VALUE="${TZ_VALUE:-Etc/UTC}"
    {
        echo "MUSIC_DIR=$MUSIC_DIR"
        echo "TZ=$TZ_VALUE"
        echo "PUID=$TARGET_UID"
        echo "AIRPLAY_NAME=\"$HOST_NAME HomeSpeaker\""
        if [ "$HTTP_PORT" != 80 ]; then echo "HTTP_PORT=$HTTP_PORT"; fi
        if [ "$AIRPLAY" = 1 ]; then echo "COMPOSE_PROFILES=airplay"; fi
    } >"$TMP_DIR/env"
    log "Creating music folder $MUSIC_DIR"
    run install -d -m 755 -o "$TARGET_USER" -g "$TARGET_GROUP" "$MUSIC_DIR"
    put "$TMP_DIR/env" "$ENV_FILE" 644
fi

if [ "$AIRPLAY" = 1 ]; then
    download scripts/start-shairport.sh "$INSTALL_DIR/scripts/start-shairport.sh" 755
fi

if [ ! -S "/run/user/$TARGET_UID/pulse/native" ]; then
    warn "No PulseAudio socket at /run/user/$TARGET_UID/pulse/native. Audio needs a running user audio session (log in on the desktop, or run: sudo loginctl enable-linger $TARGET_USER)."
fi

# --- 4. Avahi ----------------------------------------------------------------
if ! have avahi-daemon; then
    log "Installing avahi-daemon"
    have apt-get || [ "$DRY_RUN" = 1 ] || die "avahi-daemon is missing and apt-get is not available; install avahi-daemon manually and re-run."
    run apt-get update
    if [ "$DRY_RUN" = 1 ]; then
        echo "[dry-run] DEBIAN_FRONTEND=noninteractive apt-get install -y avahi-daemon"
    else
        DEBIAN_FRONTEND=noninteractive apt-get install -y avahi-daemon </dev/null
    fi
fi
log "Writing the Avahi service advertisement"
if fetch install/homespeaker.service.xml "$TMP_DIR/avahi.tpl"; then
    sed -e "s|@HTTP_PORT@|$HTTP_PORT|g" -e "s|@HOST@|$HOST_NAME|g" "$TMP_DIR/avahi.tpl" >"$TMP_DIR/avahi.xml"
    put "$TMP_DIR/avahi.xml" "$AVAHI_FILE" 644
elif [ "$DRY_RUN" = 1 ]; then
    echo "[dry-run] would write $AVAHI_FILE from $RAW_BASE/install/homespeaker.service.xml (curl not available here)"
else
    die "Could not obtain install/homespeaker.service.xml"
fi
if useSystemd; then
    run systemctl enable --now avahi-daemon
    run systemctl reload avahi-daemon
fi

# --- 5. Kiosk ----------------------------------------------------------------
hasLabwc=0
if [ -x /usr/bin/labwc ] || [ -d "$TARGET_HOME/.config/labwc" ]; then hasLabwc=1; fi
if [ "$KIOSK" = 0 ] && [ "$hasLabwc" = 1 ]; then
    if ask "Start a full-screen Chromium kiosk showing HomeSpeaker at login?" n; then KIOSK=1; fi
fi
if [ "$KIOSK" = 1 ]; then
    if [ "$hasLabwc" = 0 ]; then
        warn "labwc was not detected. Only labwc autostart (Raspberry Pi OS Bookworm Wayland) is supported; writing it anyway."
    fi
    AUTOSTART="$TARGET_HOME/.config/labwc/autostart"
    kioskBrowser="chromium"
    if ! have chromium && have chromium-browser; then kioskBrowser="chromium-browser"; fi
    kioskUrl="http://localhost/?kiosk=1"
    if [ "$HTTP_PORT" != 80 ]; then kioskUrl="http://localhost:$HTTP_PORT/?kiosk=1"; fi
    log "Writing kiosk autostart $AUTOSTART"
    echo "$kioskBrowser --kiosk --noerrdialogs --disable-infobars $kioskUrl &" >"$TMP_DIR/autostart"
    # Keep the original file: back up only once, and never back up our own output.
    if [ -f "$AUTOSTART" ] && [ ! -e "$AUTOSTART.bak" ] && ! cmp -s "$AUTOSTART" "$TMP_DIR/autostart"; then
        run cp -p "$AUTOSTART" "$AUTOSTART.bak"
    fi
    # Create each missing directory level owned by the user (not root).
    for dir in "$TARGET_HOME/.config" "$TARGET_HOME/.config/labwc"; do
        if [ ! -d "$dir" ] || [ "$DRY_RUN" = 1 ]; then run install -d -m 755 -o "$TARGET_USER" -g "$TARGET_GROUP" "$dir"; fi
    done
    put "$TMP_DIR/autostart" "$AUTOSTART" 755 "$TARGET_USER:$TARGET_GROUP"
fi

# --- 6. Nightly updates ------------------------------------------------------
log "Installing the nightly update timer"
download install/homespeaker-update.service "$SYSTEMD_DIR/homespeaker-update.service" 644
download install/homespeaker-update.timer "$SYSTEMD_DIR/homespeaker-update.timer" 644
if useSystemd; then
    run systemctl daemon-reload
    run systemctl enable --now homespeaker-update.timer
fi

# --- 7. Start ----------------------------------------------------------------
if [ "$NO_START" = 1 ]; then
    log "Skipping start (--no-start). Start later with: cd $INSTALL_DIR && sudo docker compose up -d"
elif [ "$DRY_RUN" = 1 ]; then
    echo "[dry-run] cd $INSTALL_DIR && docker compose up -d"
    echo "[dry-run] wait up to 120 s for curl -fsS http://localhost:$HTTP_PORT/alive"
else
    log "Starting HomeSpeaker"
    (cd "$INSTALL_DIR" && docker compose up -d </dev/null)
    log "Waiting for HomeSpeaker to become healthy (up to 120 s)"
    healthy=0
    for _ in $(seq 1 60); do
        if curl -fsS "http://localhost:$HTTP_PORT/alive" >/dev/null 2>&1; then healthy=1; break; fi
        sleep 2
    done
    [ "$healthy" = 1 ] || warn "HomeSpeaker did not answer on /alive within 120 s. Check: cd $INSTALL_DIR && sudo docker compose logs"
fi

echo
if [ "$HTTP_PORT" = 80 ]; then
    echo "Open http://$HOST_NAME.local to finish setup."
else
    echo "Open http://$HOST_NAME.local:$HTTP_PORT to finish setup."
fi
echo "Note: log out and back in for the docker/audio group changes to apply to $TARGET_USER."
