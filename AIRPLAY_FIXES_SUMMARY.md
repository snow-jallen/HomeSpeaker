# AirPlay Troubleshooting Fixes Applied

## Issues Addressed

### 1. Device Name Issue (showing "raspberrypi" instead of "HomeSpeaker")

**Root Cause:** System shairport-sync service or existing processes conflicting with HomeSpeaker's shairport-sync instance.

**Fixes Applied:**
- Modified `AirPlayService.cs` to kill existing shairport-sync processes before starting
- Enhanced Dockerfile to disable system shairport-sync service
- Added device name conflict detection in error parsing
- Updated docker-compose.yml with explicit AirPlay environment variables

### 2. No Audio Output Issue

**Root Cause:** Missing audio backend configuration and inadequate ALSA setup.

**Fixes Applied:**
- Enhanced shairport-sync arguments with explicit ALSA backend: `--output alsa -- -d default`
- Added PulseAudio utilities to Dockerfile for better audio support
- Added privileged mode to docker-compose.yml for better audio device access
- Created audio device debugging methods to log available devices

### 3. Discovery and Network Issues

**Root Cause:** Docker networking may interfere with multicast discovery.

**Fixes Applied:**
- Created `docker-compose.airplay.yml` with host networking for better multicast support
- Added Avahi daemon installation to Dockerfile
- Enhanced port configuration with both TCP and UDP

### 4. Process Exit Issues (NEW)

**Root Cause:** Shairport-sync starting but exiting immediately due to configuration or permission issues.

**Fixes Applied:**
- Enhanced exit code logging with meaningful error messages
- Added fallback configuration attempts (ALSA backend, then auto-detect)
- Improved error output parsing for specific issue detection
- Added comprehensive audio device and permission diagnostics
- Created container diagnostic script for troubleshooting
- Enhanced Dockerfile with proper user/group setup

## Files Modified

### 1. `HomeSpeaker.Server2/Services/AirPlayService.cs`
- Added `StopExistingShairportInstances()` method
- Added `LogAudioDevices()` method for debugging
- Enhanced `StartShairportSync()` with better audio configuration
- Improved error detection for device name conflicts

### 2. `HomeSpeaker.Server2/Dockerfile`
- Added avahi-daemon for proper mDNS support
- Added command to disable system shairport-sync service
- Enhanced ALSA configuration

### 3. `docker-compose.yml`
- Added explicit AirPlay environment variables
- Added privileged mode for better audio access

### 4. `docker-compose.airplay.yml` (NEW)
- Host networking configuration for better AirPlay compatibility
- Optimized for multicast discovery

### 5. `troubleshoot-airplay.sh` (NEW)
- Comprehensive diagnostic script
- Checks audio devices, network configuration, and service conflicts

### 6. `AIRPLAY_SETUP.md`
- Enhanced troubleshooting section
- Added specific fixes for device name and audio issues
- Added diagnostic commands

## Next Steps

1. **Rebuild and restart the container:**
   ```bash
   docker-compose down
   docker-compose up -d --build
   ```

2. **Check the logs for any remaining issues:**
   ```bash
   docker logs homespeaker | grep -i airplay
   ```

3. **If device name still shows as hostname, try host networking:**
   ```bash
   docker-compose -f docker-compose.airplay.yml up -d --build
   ```

4. **Run the troubleshooting script for diagnosis:**
   ```bash
   ./troubleshoot-airplay.sh
   ```

## Expected Results

After applying these fixes:
- Device should appear as "HomeSpeaker" in AirPlay device lists
- Audio should play through the configured output device
- Connection status should be properly detected and displayed
- No conflicts with system shairport-sync services

## Additional Notes

- The enhanced logging will help identify any remaining issues
- Host networking mode (`docker-compose.airplay.yml`) is recommended for production use
- The troubleshooting script can be run anytime to diagnose configuration issues
