#!/bin/bash
# HomeSpeaker AirPlay Troubleshooting Script

echo "=== HomeSpeaker AirPlay Troubleshooting ==="
echo "Date: $(date)"
echo ""

echo "1. Checking shairport-sync installation..."
if command -v shairport-sync >/dev/null 2>&1; then
    echo "✓ shairport-sync is installed"
    shairport-sync --version
else
    echo "✗ shairport-sync is not installed or not in PATH"
fi
echo ""

echo "2. Checking for existing shairport-sync processes..."
existing_processes=$(pgrep -f shairport-sync)
if [ -n "$existing_processes" ]; then
    echo "⚠ Found existing shairport-sync processes:"
    ps aux | grep shairport-sync | grep -v grep
    echo "These may conflict with HomeSpeaker. Consider stopping them with:"
    echo "sudo pkill -f shairport-sync"
else
    echo "✓ No existing shairport-sync processes found"
fi
echo ""

echo "3. Checking audio devices..."
echo "ALSA playback devices:"
aplay -l 2>/dev/null || echo "aplay command not found"
echo ""
echo "ALSA mixer controls:"
amixer scontrols 2>/dev/null || echo "amixer command not found"
echo ""

echo "4. Checking network configuration..."
echo "Current hostname: $(hostname)"
echo "IP addresses:"
ip addr show | grep "inet " | grep -v 127.0.0.1
echo ""

echo "5. Checking port availability..."
port_check=$(netstat -ln | grep :5025 || echo "Port 5025 is available")
echo "Port 5025 status: $port_check"
echo ""

echo "6. Checking for system shairport-sync service..."
if systemctl is-active --quiet shairport-sync 2>/dev/null; then
    echo "⚠ System shairport-sync service is running. This may conflict with HomeSpeaker."
    echo "Consider disabling it with: sudo systemctl disable --now shairport-sync"
elif systemctl is-enabled --quiet shairport-sync 2>/dev/null; then
    echo "⚠ System shairport-sync service is enabled but not running"
else
    echo "✓ No system shairport-sync service found"
fi
echo ""

echo "7. Checking Avahi/mDNS..."
if systemctl is-active --quiet avahi-daemon 2>/dev/null; then
    echo "✓ Avahi daemon is running (required for AirPlay discovery)"
else
    echo "⚠ Avahi daemon is not running. Install and start it for proper AirPlay discovery:"
    echo "sudo apt install avahi-daemon"
    echo "sudo systemctl enable --now avahi-daemon"
fi
echo ""

echo "8. Testing basic shairport-sync functionality..."
echo "Attempting to start shairport-sync with test configuration..."
timeout 5s shairport-sync --name "TestDevice" --port 5026 --verbose 2>&1 | head -10
echo ""

echo "=== Recommendations ==="
echo "1. If device name shows as hostname instead of 'HomeSpeaker':"
echo "   - Stop any system shairport-sync services"
echo "   - Ensure no other AirPlay receivers are running"
echo ""
echo "2. If no audio output:"
echo "   - Check that audio devices are accessible in container"
echo "   - Verify ALSA_CARD environment variable is set correctly"
echo "   - Ensure container has audio device permissions"
echo ""
echo "3. If device not discovered:"
echo "   - Ensure Avahi/mDNS is running"
echo "   - Check firewall settings for multicast traffic"
echo "   - Try host networking mode for Docker"
echo ""
