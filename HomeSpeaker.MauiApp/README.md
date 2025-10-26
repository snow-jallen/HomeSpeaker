# HomeSpeaker MAUI App

A cross-platform mobile app for controlling HomeSpeaker instances with Apple Watch and Siri integration.

## Features

- **Server Management**: Add and manage multiple HomeSpeaker server instances
- **Playlist Control**: Browse and play playlists from your HomeSpeaker servers
- **Siri Integration**: Control playback using Siri voice commands
- **Apple Watch Support**: Quick access to your music from your wrist

## Siri Commands

Once configured, you can use Siri to control your HomeSpeaker:

```
"Hey Siri, play the [playlist name] playlist on the [server nickname] HomeSpeaker"
```

For example:
- "Hey Siri, play the Jazz playlist on the Living Room HomeSpeaker"
- "Hey Siri, play the Workout playlist on the Bedroom HomeSpeaker"

## Setup

### iOS Requirements

- iOS 14.2 or later
- Xcode 12.0 or later for building

### Configuration

1. Launch the app
2. Tap the "+" button to add a HomeSpeaker server
3. Enter:
   - **Server Nickname**: A friendly name (e.g., "Living Room")
   - **Server URL**: The full URL to your HomeSpeaker server (e.g., `https://192.168.1.100:5001`)
   - **Set as default**: Check this if you want this to be your primary server
4. Save the configuration

### Enabling Siri

1. Go to iOS Settings > Siri & Search
2. Find "HomeSpeaker" in the app list
3. Enable "Use with Siri"
4. You can now create shortcuts or use voice commands

### Apple Watch Setup

The Apple Watch companion app will automatically be installed when you install the iOS app on a paired iPhone.

## Building from Source

### Prerequisites

- .NET 9 SDK
- macOS with Xcode (for iOS builds)
- Visual Studio 2022 or Visual Studio Code with C# extensions

### Build Commands

```bash
# Restore dependencies
dotnet restore

# Build for iOS
dotnet build -f net9.0-ios

# Build for Android
dotnet build -f net9.0-android
```

## Architecture

The app is built using:
- **.NET MAUI**: Cross-platform UI framework
- **MVVM Pattern**: Using CommunityToolkit.Mvvm
- **gRPC**: For communication with HomeSpeaker servers
- **Intents Framework**: For Siri integration

## Project Structure

```
HomeSpeaker.MauiApp/
├── Converters/              # Value converters for XAML bindings
├── Platforms/
│   ├── iOS/
│   │   └── Intents/        # Siri intent definitions and handlers
│   └── Android/
├── Resources/              # App icons, images, fonts, styles
├── Services/               # Business logic and data access
├── ViewModels/             # MVVM view models
└── Views/                  # XAML pages
```

## Security Notes

- The app allows insecure HTTPS connections for development. In production, ensure your HomeSpeaker servers use valid SSL certificates.
- Server configurations are stored locally on the device.

## Troubleshooting

### Siri Commands Not Working

1. Ensure Siri is enabled for the app in iOS Settings
2. Check that at least one server is configured
3. Verify the server URL is accessible from your device
4. Try using the exact playlist and server names as configured

### Connection Issues

1. Verify the server URL includes the protocol (https://)
2. Ensure your device can reach the server on the network
3. Check firewall settings on the server
4. For HTTPS, the server must have a valid certificate or the app needs to trust self-signed certificates

## License

This project is part of the HomeSpeaker solution. See the main repository for license information.
