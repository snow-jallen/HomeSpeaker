#!/bin/bash

# HomeSpeaker container startup script
set -e

echo "Starting HomeSpeaker container..."

# Start Avahi daemon in the background (required for AirPlay mDNS)
echo "Starting Avahi daemon..."
if command -v avahi-daemon >/dev/null 2>&1; then
    # Create avahi run directory if it doesn't exist
    sudo mkdir -p /var/run/avahi-daemon 2>/dev/null || true
    sudo chown avahi:avahi /var/run/avahi-daemon 2>/dev/null || true
    
    # Start avahi daemon
    sudo avahi-daemon --daemonize --no-drop-root 2>/dev/null || {
        echo "Warning: Failed to start avahi-daemon as system service, trying user mode..."
        # If system start fails, try to start without root privileges
        avahi-daemon --no-drop-root --daemonize 2>/dev/null || {
            echo "Warning: Could not start Avahi daemon. AirPlay mDNS may not work properly."
        }
    }
    
    # Wait for daemon to initialize
    sleep 2
    
    # Check if avahi daemon is running
    if pgrep avahi-daemon >/dev/null; then
        echo "Avahi daemon started successfully"
    else
        echo "Warning: Avahi daemon may not be running properly"
    fi
else
    echo "Warning: avahi-daemon not found. AirPlay discovery may not work."
fi

# Log audio setup for debugging
echo "Audio setup information:"
echo "Current user: $(whoami)"
echo "User groups: $(groups)"
echo "Audio devices:"
ls -la /dev/snd/ 2>/dev/null || echo "No /dev/snd devices found"

# Check ALSA configuration
echo "ALSA configuration:"
aplay -l 2>/dev/null || echo "No ALSA playback devices found"

# Start the main application
echo "Starting HomeSpeaker application..."
exec dotnet HomeSpeaker.Server2.dll
