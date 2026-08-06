#!/bin/bash
# Launch HomeSpeaker.Server2 locally, smoke-test the REST API, optionally
# screenshot the Blazor UI, then shut down. Run from the repo root:
#   bash .claude/skills/run-homespeaker-server/smoke.sh
# Exit code 0 = all checks passed.
#
# Flags:
#   --keep        leave the server running after the checks (prints the PID)
#   --screenshot  also capture the web UI with headless Edge/Chrome
set -u

PORT=5280
BASE="http://localhost:$PORT"
REPO_ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
SCRATCH="${TMPDIR:-/tmp}/homespeaker-smoke"
mkdir -p "$SCRATCH"
LOG="$SCRATCH/server.log"
KEEP=0
SHOT=0
for arg in "$@"; do
  case "$arg" in
    --keep) KEEP=1 ;;
    --screenshot) SHOT=1 ;;
  esac
done

PASS=0; FAIL=0
check() { # check <name> <ok:0|1>
  if [ "$2" = 0 ]; then PASS=$((PASS+1)); echo "  ok: $1"; else FAIL=$((FAIL+1)); echo "  FAIL: $1"; fi
}

cd "$REPO_ROOT" || exit 1

if lsof -iTCP:$PORT -sTCP:LISTEN >/dev/null 2>&1; then
  echo "Port $PORT is already in use - is a server already running? Aborting." >&2
  exit 1
fi

# Ensure at least one song exists so library endpoints return data.
# (The library only scans *.mp3. No ffmpeg needed - this synthesizes
# ~3s of silent MPEG-1 Layer III frames plus an ID3v1 tag.)
MP3="HomeSpeaker.Server2/HomeSpeakerMedia/smoke-test.mp3"
if [ ! -f "$MP3" ]; then
  python3 - "$MP3" <<'EOF'
import sys
frame = bytes([0xFF, 0xFB, 0x90, 0x00]) + bytes(413)
def field(s, n):
    b = s.encode()[:n]
    return b + bytes(n - len(b))
id3v1 = (b"TAG" + field("Smoke Test Song", 30) + field("Smoke Test Artist", 30)
         + field("Smoke Test Album", 30) + field("2026", 4) + field("", 28)
         + bytes([0, 1, 255]))
with open(sys.argv[1], "wb") as f:
    f.write(frame * 120 + id3v1)
EOF
  echo "generated $MP3"
fi

echo "starting server (log: $LOG) ..."
dotnet run --project HomeSpeaker.Server2 --launch-profile http > "$LOG" 2>&1 &
RUN_PID=$!

stop_server() {
  kill "$RUN_PID" 2>/dev/null
  # dotnet run launches an apphost binary named HomeSpeaker.Server2 (no .dll
  # suffix in the command line) - match that, not "dotnet ... .dll".
  pkill -f "bin/Debug/net.*HomeSpeaker.Server2" 2>/dev/null
}
[ "$KEEP" = 1 ] || trap stop_server EXIT

for _ in $(seq 1 60); do
  curl -sf -m 2 "$BASE/health" >/dev/null 2>&1 && break
  sleep 1
done

echo "running checks against $BASE ..."

curl -sf -m 5 "$BASE/health" | jq -e '.status == "Healthy"' >/dev/null
check "health endpoint reports Healthy" $?

curl -sf -m 10 "$BASE/api/homespeaker/songs" | jq -e 'map(select(.name == "Smoke Test Song")) | length >= 1' >/dev/null
check "library contains the smoke-test song" $?

curl -sf -m 10 "$BASE/api/homespeaker/player/status" | jq -e 'has("volume") and has("stillPlaying")' >/dev/null
check "player status returns volume + playing state" $?

curl -sf -m 5 "$BASE/api/homespeaker/queue" | jq -e 'type == "array"' >/dev/null
check "queue endpoint returns an array" $?

curl -sf -m 5 -X POST "$BASE/api/homespeaker/playlists/Smoke%20Playlist/songs" \
  -H "Content-Type: application/json" \
  -d '{"songPath":"HomeSpeakerMedia/smoke-test.mp3"}' >/dev/null
check "add song to playlist" $?

curl -sf -m 5 "$BASE/api/homespeaker/playlists" | jq -e 'map(select(.name == "Smoke Playlist")) | length == 1' >/dev/null
check "playlist visible with the song in it" $?

curl -sf -m 5 -X DELETE "$BASE/api/homespeaker/playlists/Smoke%20Playlist" >/dev/null
check "delete playlist" $?

STREAM_ID=$(curl -sf -m 5 -X POST "$BASE/api/homespeaker/radio" \
  -H "Content-Type: application/json" \
  -d '{"name":"Smoke Stream","url":"http://example.com/stream"}' | jq -r '.id')
[ -n "$STREAM_ID" ] && [ "$STREAM_ID" != "null" ]
check "create radio stream (id=$STREAM_ID)" $?

curl -sf -m 5 -X DELETE "$BASE/api/homespeaker/radio/$STREAM_ID" >/dev/null
check "delete radio stream" $?

if [ "$SHOT" = 1 ]; then
  BROWSER=""
  for c in "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge" \
           "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"; do
    [ -x "$c" ] && BROWSER="$c" && break
  done
  if [ -n "$BROWSER" ]; then
    # --virtual-time-budget captures the server-side-rendered page (nav +
    # Now Playing card). A wall-clock --timeout instead catches the blank
    # frame while the Blazor circuit re-renders - don't use it.
    "$BROWSER" --headless --disable-gpu --window-size=1280,900 \
      --virtual-time-budget=8000 --screenshot="$SCRATCH/ui.png" "$BASE/" >/dev/null 2>&1
    [ -s "$SCRATCH/ui.png" ]
    check "UI screenshot written to $SCRATCH/ui.png" $?
  else
    echo "  skip: no Chromium-based browser found for screenshot"
  fi
fi

echo
echo "passed: $PASS  failed: $FAIL"
if [ "$KEEP" = 1 ]; then
  echo "server left running: PID $RUN_PID, $BASE (kill with: pkill -f 'bin/Debug/net.*HomeSpeaker.Server2')"
fi
[ "$FAIL" = 0 ]
