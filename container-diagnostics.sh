#!/bin/bash
# Container AirPlay Diagnostic Script

echo "=== Container AirPlay Diagnostics ==="
echo "Date: $(date)"
echo "User: $(whoami)"
echo "UID: $(id -u)"
echo "GID: $(id -g)"
echo "Groups: $(groups)"
echo ""

echo "=== Audio Device Check ==="
echo "Audio devices:"
ls -la /dev/snd/ 2>/dev/null || echo "No /dev/snd devices found"
echo ""

echo "Audio device permissions:"
ls -la /dev/snd/ | head -5
echo ""

echo "=== ALSA Configuration ==="
echo "ALSA devices:"
aplay -l 2>/dev/null || echo "aplay failed"
echo ""

echo "ALSA configuration:"
cat /etc/asound.conf 2>/dev/null || echo "No /etc/asound.conf found"
echo ""

echo "=== Environment Variables ==="
echo "ALSA_CARD: $ALSA_CARD"
echo "HOME: $HOME"
echo ""

echo "=== Shairport-sync Test ==="
echo "Shairport-sync version:"
shairport-sync --version 2>/dev/null || echo "shairport-sync not found or failed"
echo ""

echo "Testing basic shairport-sync (will timeout after 3 seconds):"
timeout 3s shairport-sync --name "DiagnosticTest" --port 5026 --verbose 2>&1 | head -10
echo ""

echo "=== Process Check ==="
echo "Running audio-related processes:"
ps aux | grep -E "(pulse|alsa|audio|shairport)" | grep -v grep || echo "No audio processes found"
echo ""

echo "=== Network Check ==="
echo "Listening on port 5025:"
netstat -ln | grep 5025 || echo "Port 5025 not in use"
echo ""

echo "=== Recommendations ==="
if [ ! -d "/dev/snd" ]; then
    echo "⚠ /dev/snd not found - audio devices not mounted"
fi

if ! groups | grep -q audio; then
    echo "⚠ User not in audio group"
fi

if [ -z "$ALSA_CARD" ]; then
    echo "⚠ ALSA_CARD environment variable not set"
fi

echo "=== End Diagnostics ==="
