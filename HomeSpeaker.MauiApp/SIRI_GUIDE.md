# Siri Integration Guide

This guide explains how to set up and use Siri voice commands with the HomeSpeaker MAUI app.

## Overview

The HomeSpeaker app integrates with Siri to allow voice-controlled playback of playlists on your HomeSpeaker devices. Once configured, you can use natural language commands to play music without opening the app.

## Setup Steps

### 1. Install and Configure the App

1. Install the HomeSpeaker app on your iPhone
2. Launch the app
3. Add at least one HomeSpeaker server:
   - Tap the "+" button
   - Enter a memorable nickname (e.g., "Living Room", "Bedroom", "Kitchen")
   - Enter the server URL (e.g., `https://192.168.1.100:5001`)
   - Optionally set as default
   - Tap "Save"

### 2. Enable Siri

Siri should automatically be enabled for the app, but you can verify:

1. Open **Settings** on your iPhone
2. Scroll down and tap **Siri & Search**
3. Find **HomeSpeaker** in the app list
4. Ensure these are enabled:
   - "Use with Ask Siri"
   - "Show in Search"
   - "Suggest Shortcuts"

### 3. Create Shortcuts (Optional)

While voice commands work directly, you can create custom shortcuts for frequently used commands:

1. Open **Shortcuts** app
2. Tap the "+" to create a new shortcut
3. Add action → Search for "HomeSpeaker"
4. Select "Play Playlist"
5. Configure:
   - Select or type playlist name
   - Select server nickname
6. Name your shortcut
7. Add to Siri with a custom phrase

## Voice Command Syntax

### Basic Command Structure

```
"Hey Siri, play the [playlist name] playlist on the [server nickname] HomeSpeaker"
```

### Example Commands

#### Simple Commands
```
"Hey Siri, play the Jazz playlist on the Living Room HomeSpeaker"
"Hey Siri, play the Rock playlist on the Bedroom HomeSpeaker"
"Hey Siri, play the Workout playlist on the Kitchen HomeSpeaker"
```

#### With Different Playlist Types
```
"Hey Siri, play the Chill Vibes playlist on the Living Room HomeSpeaker"
"Hey Siri, play the 80s Hits playlist on the Bedroom HomeSpeaker"
"Hey Siri, play the Classical playlist on the Study Room HomeSpeaker"
```

### Grammar Variations

The system understands several variations:

✅ "play the Jazz playlist on the Living Room HomeSpeaker"
✅ "play Jazz on Living Room HomeSpeaker"
✅ "play the Jazz playlist on Living Room"

⚠️ Note: Using exact names as configured provides the best results.

## How It Works

### Dynamic Options

The app provides Siri with:

1. **Available Playlists**: Dynamically fetched from your default server
2. **Available Servers**: All configured server nicknames

This allows Siri to:
- Suggest playlists as you speak
- Validate playlist and server names
- Provide autocomplete suggestions

### Intent Processing Flow

1. **You speak**: "Hey Siri, play the Jazz playlist on the Living Room HomeSpeaker"
2. **Siri parses**: 
   - Playlist name: "Jazz"
   - Server nickname: "Living Room"
3. **App receives intent**: Intent handler is invoked
4. **App executes**:
   - Looks up server by nickname
   - Connects to server via gRPC
   - Sends play playlist command
5. **Playback starts**: Music begins playing on the selected server

## Using from Apple Watch

### Requirements
- iPhone with HomeSpeaker app installed
- Apple Watch paired with iPhone
- Watch OS 7.0 or later

### How to Use

Simply raise your wrist and say:
```
"Hey Siri, play the Jazz playlist on the Living Room HomeSpeaker"
```

The watch will:
1. Process your voice command
2. Send to your iPhone
3. Execute the command
4. Provide haptic feedback on success

You don't need to open the app on your watch - Siri handles everything!

## Troubleshooting

### Siri Doesn't Recognize the Command

**Problem**: Siri says "I don't understand" or "I can't do that"

**Solutions**:
1. Ensure you're using the exact phrase: "play the [playlist] on the [server] HomeSpeaker"
2. Check that Siri is enabled for HomeSpeaker in Settings
3. Try using exact names as configured in the app
4. Make sure the app has been opened at least once

### Playlist Not Found

**Problem**: Siri says the playlist doesn't exist

**Solutions**:
1. Open the app and verify the playlist name
2. Ensure you're using the exact playlist name (case-insensitive)
3. Check that the server is online and accessible
4. Try refreshing playlists in the app

### Server Not Found

**Problem**: Siri can't find the server

**Solutions**:
1. Verify the server nickname in the app settings
2. Ensure the server is configured and saved
3. Try using the exact server nickname
4. Check that at least one server is configured

### Command Doesn't Execute

**Problem**: Siri understands but nothing happens

**Solutions**:
1. Check your network connection
2. Verify the server is online and reachable
3. Open the app and try playing manually to test
4. Check server URL is correct
5. Ensure firewall isn't blocking connections

### Apple Watch Not Working

**Problem**: Commands work on iPhone but not on Watch

**Solutions**:
1. Ensure iPhone and Watch are connected
2. Check that Watch has network connectivity (through iPhone)
3. Try restarting both devices
4. Re-pair the Watch if necessary

## Advanced Usage

### Creating Custom Shortcuts

For frequently used commands, create shortcuts:

1. **Morning Routine**:
   ```
   "Good morning" → Play "Morning Jazz" on "Kitchen"
   ```

2. **Workout Time**:
   ```
   "Start workout" → Play "Workout Mix" on "Living Room"
   ```

3. **Bedtime**:
   ```
   "Good night" → Play "Sleep Sounds" on "Bedroom"
   ```

### Using with Automation

Combine with iOS Shortcuts automation:

1. **Time-based**:
   - 7:00 AM → Play morning playlist
   - 10:00 PM → Play relaxing playlist

2. **Location-based**:
   - Arrive home → Play welcome playlist
   - Leave home → Stop playback

3. **Activity-based**:
   - Start workout → Play workout playlist
   - Start focus mode → Play concentration playlist

## Privacy and Security

### What Information Siri Accesses

- Server nicknames (stored locally)
- Playlist names (fetched from servers)
- No audio content
- No listening history
- No personal data beyond configuration

### Data Storage

- All data stored locally on your device
- Server configurations in app sandbox
- No cloud sync (unless you configure it)
- Can be deleted by deleting the app

### Network Communication

- Direct connection to your HomeSpeaker servers
- No third-party servers involved
- Encrypted communication (HTTPS recommended)
- Commands processed on-device

## Best Practices

### Naming Conventions

1. **Server Nicknames**:
   - Use location-based names: "Living Room", "Bedroom"
   - Keep them short and distinct
   - Avoid special characters
   - Use easy-to-pronounce names

2. **Playlist Names**:
   - Use descriptive names: "Morning Jazz", "Workout Mix"
   - Avoid similar-sounding names
   - Keep names reasonably short
   - Use standard vocabulary

### Command Efficiency

1. **Learn Your Phrases**:
   - Practice the exact commands that work
   - Note any variations Siri recognizes
   - Create shortcuts for complex names

2. **Common Patterns**:
   - Morning: "Play Morning Playlist on Kitchen"
   - Evening: "Play Dinner Music on Living Room"
   - Workout: "Play Exercise Mix on Garage"

3. **Quick Access**:
   - Set default server for most-used location
   - Create shortcuts for daily routines
   - Use Watch for hands-free control

## Examples by Scenario

### Working from Home
```
"Hey Siri, play the Focus playlist on the Office HomeSpeaker"
"Hey Siri, play the Productivity Mix on the Office HomeSpeaker"
```

### Entertaining Guests
```
"Hey Siri, play the Dinner Party playlist on the Living Room HomeSpeaker"
"Hey Siri, play the Jazz Collection on the Living Room HomeSpeaker"
```

### Exercise
```
"Hey Siri, play the Cardio Mix on the Garage HomeSpeaker"
"Hey Siri, play the Running playlist on the Garage HomeSpeaker"
```

### Relaxation
```
"Hey Siri, play the Meditation playlist on the Bedroom HomeSpeaker"
"Hey Siri, play the Nature Sounds on the Bedroom HomeSpeaker"
```

### Cooking
```
"Hey Siri, play the Cooking Tunes on the Kitchen HomeSpeaker"
"Hey Siri, play the Upbeat Mix on the Kitchen HomeSpeaker"
```

## Getting Help

If you continue to experience issues:

1. Check the main README.md for setup instructions
2. Review IMPLEMENTATION.md for technical details
3. Open an issue on GitHub
4. Check your HomeSpeaker server logs
5. Verify network connectivity

## Tips for Best Experience

1. **Speak Clearly**: Articulate playlist and server names
2. **Use Full Phrase**: Include "playlist" and "HomeSpeaker" for clarity
3. **Check Configuration**: Regularly verify servers are accessible
4. **Update Regularly**: Keep the app updated for improvements
5. **Create Shortcuts**: For frequently used commands
6. **Test Manually**: Use the app UI to verify before using Siri
7. **Network Quality**: Ensure stable WiFi or cellular connection
8. **Background Refresh**: Enable for best performance

## Future Enhancements

Potential improvements being considered:

- [ ] More natural language patterns
- [ ] Support for "default server" in commands
- [ ] Volume control via Siri
- [ ] Queue management
- [ ] Skip/pause/resume commands
- [ ] Multi-room playback

---

Enjoy controlling your HomeSpeaker with your voice! 🎵🎙️
