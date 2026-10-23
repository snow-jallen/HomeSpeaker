#!/usr/bin/env bash
# Removes HomeSpeaker's containers, systemd units and Avahi advertisement.
# Never deletes the music folder (MUSIC_DIR) or Docker itself.
#   sudo bash uninstall.sh [--dry-run] [--help]
set -euo pipefail

INSTALL_DIR="/opt/homespeaker"
AVAHI_FILE="/etc/avahi/services/homespeaker.service"
SYSTEMD_DIR="/etc/systemd/system"
DRY_RUN=0

for arg in "$@"; do
    case "$arg" in
        --dry-run) DRY_RUN=1 ;;
        --help|-h) echo "Usage: sudo bash uninstall.sh [--dry-run]"; exit 0 ;;
        *) echo "Unknown option: $arg" >&2; exit 2 ;;
    esac
done

have() { command -v "$1" >/dev/null 2>&1; }
run() {
    if [ "$DRY_RUN" = 1 ]; then echo "[dry-run] $*"; else "$@"; fi
}
# True when systemd is running (or when dry-running, so every action is shown).
useSystemd() { [ "$DRY_RUN" = 1 ] || { have systemctl && [ -d /run/systemd/system ]; }; }

if [ "$DRY_RUN" = 0 ] && [ "$(id -u)" -ne 0 ]; then
    echo "ERROR: Run this script with sudo." >&2
    exit 1
fi

if [ -f "$INSTALL_DIR/docker-compose.yml" ] && have docker; then
    echo "==> Stopping the HomeSpeaker stack"
    if [ "$DRY_RUN" = 1 ]; then
        echo "[dry-run] cd $INSTALL_DIR && docker compose down"
    else
        (cd "$INSTALL_DIR" && docker compose down) || echo "WARNING: docker compose down failed" >&2
    fi
elif [ "$DRY_RUN" = 1 ]; then
    echo "[dry-run] (docker or $INSTALL_DIR/docker-compose.yml not found here; would run: cd $INSTALL_DIR && docker compose down)"
fi

if useSystemd; then
    run systemctl disable --now homespeaker-update.timer || true
fi
run rm -f "$SYSTEMD_DIR/homespeaker-update.service" "$SYSTEMD_DIR/homespeaker-update.timer"
if useSystemd; then
    run systemctl daemon-reload
fi

run rm -f "$AVAHI_FILE"
if useSystemd; then
    run systemctl reload avahi-daemon || true
fi

echo "==> Removing $INSTALL_DIR (compose file, .env and scripts)"
run rm -rf "$INSTALL_DIR"

echo
echo "HomeSpeaker was removed. Your music folder (MUSIC_DIR) and HomeSpeaker.db were left untouched."
echo "A kiosk autostart file (~/.config/labwc/autostart), if you created one, was also left in place."
