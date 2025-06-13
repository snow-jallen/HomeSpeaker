# AirPlay Setup Instructions

## Prerequisites

For AirPlay functionality to work, you need to install `shairport-sync` on your Linux server.

### Ubuntu/Debian:
```bash
sudo apt update
sudo apt install shairport-sync
```

### CentOS/RHEL/Fedora:
```bash
sudo dnf install shairport-sync
# or for older versions:
sudo yum install shairport-sync
```

## Docker Deployment

The AirPlay functionality is automatically included in the Docker deployment. The Dockerfile installs `shairport-sync` and the docker-compose.yml exposes the necessary ports.

### Network Configuration for Docker

AirPlay discovery requires multicast networking. You have two options:

#### Option 1: Bridge Network (Default)
Uses the standard docker-compose.yml with port mapping:
```bash
docker-compose up -d --build
```

#### Option 2: Host Network (Recommended for AirPlay)
Uses docker-compose.airplay.yml with host networking for better AirPlay compatibility:
```bash
docker-compose -f docker-compose.airplay.yml up -d --build
```

## Configuration

Add the following configuration to your `appsettings.json`:

```json
{
  "AirPlay": {
    "DeviceName": "HomeSpeaker",
    "Port": 5000
  }
}
```

## GitHub Actions Deployment

The GitHub Actions workflow has been updated to support AirPlay:

1. **Automatic Installation**: The Dockerfile now installs shairport-sync
2. **Port Exposure**: docker-compose.yml exposes AirPlay ports (5000 TCP/UDP)
3. **Environment Variables**: AirPlay configuration is set via environment variables
4. **Tag-based Deployment**: Deployment is triggered by creating and pushing tags

To deploy a new version with AirPlay support:

```bash
git tag -a 2025.6.13 -m "Added AirPlay support"
git push --tags
```

## How it Works

1. When the HomeSpeaker server starts, it launches `shairport-sync` as a background process
2. Your iOS device will see "HomeSpeaker" (or your configured device name) in the AirPlay list
3. When you connect and start streaming, the HomeSpeaker automatically stops any currently playing music
4. The web interface shows which device is connected and when
5. When you disconnect from AirPlay, you can resume normal HomeSpeaker operation

## Troubleshooting

### General Issues
- Make sure `shairport-sync` is installed and in your PATH
- Check that port 5000 (or your configured port) is not being used by another service
- Ensure your iOS device and HomeSpeaker server are on the same network
- Check the server logs for any AirPlay-related errors

### Docker-Specific Issues
- If AirPlay devices aren't discovered, try using the host network configuration
- Ensure the container has access to audio devices (`/dev/snd`)
- Check that the AirPlay ports are properly exposed and not blocked by firewall

### Network Discovery Issues
- AirPlay uses Bonjour/mDNS for discovery - ensure multicast is enabled on your network
- Some corporate/complex networks may block multicast traffic
- Try connecting both devices to the same WiFi network segment

### Container Logs
Check the container logs for AirPlay-related messages:
```bash
docker logs homespeaker | grep -i airplay
docker logs homespeaker | grep -i shairport
```
