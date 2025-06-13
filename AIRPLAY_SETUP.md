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

## How it Works

1. When the HomeSpeaker server starts, it launches `shairport-sync` as a background process
2. Your iOS device will see "HomeSpeaker" (or your configured device name) in the AirPlay list
3. When you connect and start streaming, the HomeSpeaker automatically stops any currently playing music
4. The web interface shows which device is connected and when
5. When you disconnect from AirPlay, you can resume normal HomeSpeaker operation

## Troubleshooting

- Make sure `shairport-sync` is installed and in your PATH
- Check that port 5000 (or your configured port) is not being used by another service
- Ensure your iOS device and HomeSpeaker server are on the same network
- Check the server logs for any AirPlay-related errors
