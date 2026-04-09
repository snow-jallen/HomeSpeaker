# HomeSpeaker REST API Documentation

This document describes the REST API endpoints that provide an alternative to the gRPC interface for the HomeSpeaker system.

## Base URL

All endpoints are prefixed with `/api/homespeaker/`

Example: `https://localhost:7072/api/homespeaker/songs`

## Features

- **Complete gRPC Parity**: All gRPC functionality available via REST
- **OpenAPI Documentation**: Automatic Swagger documentation generation
- **Comprehensive Logging**: Structured logging with correlation IDs
- **Distributed Tracing**: Full Activity/span telemetry support
- **Error Handling**: Proper HTTP status codes and error responses
- **Type Safety**: Strong typing with request/response models

## Endpoints Overview

### Song Management
- `GET /songs` - Get all songs or filter by folder
- `PUT /songs/{id}` - Update song metadata
- `DELETE /songs/{id}` - Delete a song
- `POST /songs/{id}/play` - Play a specific song
- `POST /songs/{id}/enqueue` - Add song to queue

### Player Controls
- `GET /player/status` - Get current player status
- `POST /player/control` - Control playback (play/pause/stop/skip/volume)
- `POST /folders/{path}/play` - Play all songs in a folder
- `POST /folders/{path}/enqueue` - Add folder to queue
- `POST /stream/play` - Play an internet radio stream
- `POST /backlight/toggle` - Toggle device backlight

### Playlist Management
- `GET /playlists` - Get all playlists
- `POST /playlists/{name}/play` - Play a playlist
- `PUT /playlists/{oldName}/rename` - Rename a playlist
- `DELETE /playlists/{name}` - Delete a playlist
- `POST /playlists/{name}/songs` - Add song to playlist
- `DELETE /playlists/{name}/songs` - Remove song from playlist
- `PUT /playlists/{name}/reorder` - Reorder playlist songs

### Queue Management
- `GET /queue` - Get current play queue
- `PUT /queue` - Update entire queue
- `POST /queue/shuffle` - Shuffle the queue
- `DELETE /queue` - Clear the queue

### YouTube Integration
- `GET /youtube/search` - Search YouTube videos
- `POST /youtube/cache` - Cache a YouTube video

## Detailed Endpoint Documentation

### Song Management

#### GET /api/homespeaker/songs
Get all songs or songs from a specific folder.

**Query Parameters:**
- `folder` (optional): Filter songs by folder path

**Response:** Array of Song objects
```json
[
  {
    "songId": 1,
    "name": "Song Title",
    "path": "/music/artist/song.mp3",
    "album": "Album Name",
    "artist": "Artist Name"
  }
]
```

#### POST /api/homespeaker/songs/{songId}/play
Start playing the specified song immediately.

**Path Parameters:**
- `songId`: ID of the song to play

**Response:** 200 OK with success message

#### PUT /api/homespeaker/songs/{songId}
Update song metadata (name, artist, album).

**Path Parameters:**
- `songId`: ID of the song to update

**Request Body:**
```json
{
  "name": "New Song Title",
  "artist": "New Artist",
  "album": "New Album"
}
```

### Player Controls

#### GET /api/homespeaker/player/status
Get current player status including playback position, current song, and volume.

**Response:**
```json
{
  "elapsed": "00:02:30",
  "remaining": "00:01:45",
  "stillPlaying": true,
  "percentComplete": 0.58,
  "currentSong": {
    "songId": 1,
    "name": "Current Song",
    "path": "/music/song.mp3",
    "album": "Album",
    "artist": "Artist"
  },
  "volume": 75
}
```

#### POST /api/homespeaker/player/control
Control player operations like play, pause, stop, skip, and volume.

**Request Body:**
```json
{
  "stop": false,
  "play": true,
  "clearQueue": false,
  "skipToNext": false,
  "setVolume": true,
  "volumeLevel": 80
}
```

#### POST /api/homespeaker/folders/{*folderPath}/play
Play all songs from the specified folder.

**Path Parameters:**
- `folderPath`: Path to the folder (supports subdirectories)

### Playlist Management

#### GET /api/homespeaker/playlists
Get all playlists with their songs.

**Response:** Array of Playlist objects
```json
[
  {
    "playlistName": "My Favorites",
    "songs": [
      {
        "songId": 1,
        "name": "Song 1",
        "path": "/music/song1.mp3",
        "album": "Album 1",
        "artist": "Artist 1"
      }
    ]
  }
]
```

#### POST /api/homespeaker/playlists/{playlistName}/songs
Add a song to the specified playlist.

**Path Parameters:**
- `playlistName`: Name of the playlist

**Request Body:**
```json
{
  "songPath": "/music/artist/song.mp3"
}
```

#### PUT /api/homespeaker/playlists/{playlistName}/reorder
Change the order of songs in a playlist.

**Request Body:**
```json
{
  "songPaths": [
    "/music/song2.mp3",
    "/music/song1.mp3",
    "/music/song3.mp3"
  ]
}
```

### Queue Management

#### GET /api/homespeaker/queue
Get all songs currently in the playback queue.

**Response:** Array of Song objects

#### PUT /api/homespeaker/queue
Replace the current queue with the provided list of songs.

**Request Body:**
```json
{
  "songs": [
    "/music/song1.mp3",
    "/music/song2.mp3"
  ]
}
```

### YouTube Integration

#### GET /api/homespeaker/youtube/search
Search YouTube for videos matching the search term.

**Query Parameters:**
- `q`: Search term

**Response:** Array of VideoDto objects
```json
[
  {
    "title": "Video Title",
    "id": "video_id",
    "url": "https://youtube.com/watch?v=video_id",
    "thumbnail": "https://img.youtube.com/vi/video_id/default.jpg",
    "author": "Channel Name",
    "duration": "00:03:45"
  }
]
```

#### POST /api/homespeaker/youtube/cache
Download and cache a YouTube video for offline playback.

**Request Body:**
```json
{
  "video": {
    "title": "Video Title",
    "id": "video_id",
    "url": "https://youtube.com/watch?v=video_id",
    "thumbnail": "https://img.youtube.com/vi/video_id/default.jpg",
    "author": "Channel Name",
    "duration": "00:03:45"
  }
}
```

**Response:** 202 Accepted (operation runs in background)

## Error Handling

All endpoints return appropriate HTTP status codes:

- `200 OK` - Successful operation
- `201 Created` - Resource created successfully
- `202 Accepted` - Request accepted for processing
- `400 Bad Request` - Invalid request parameters
- `404 Not Found` - Requested resource not found
- `500 Internal Server Error` - Server error with details

Error responses include a problem details object:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Invalid song ID provided"
}
```

## Logging and Telemetry

All endpoints include:
- **Structured Logging**: Request/response details with correlation IDs
- **Activity Tracing**: Distributed tracing spans for performance monitoring
- **Performance Metrics**: Execution time and success/failure rates
- **Error Tracking**: Detailed error information with stack traces

## Authentication

Currently, the API does not require authentication. In production deployments, consider adding:
- API key authentication
- JWT token validation
- Rate limiting
- CORS configuration

## OpenAPI/Swagger Documentation

When running in development mode, interactive API documentation is available at:
- `/swagger/index.html` - Swagger UI
- `/swagger/v1/swagger.json` - OpenAPI specification

## Examples

See the `Examples/HomeSpeakerRestClient.cs` file for complete C# client examples demonstrating how to use all endpoints.

### Quick Start Example

```csharp
using var httpClient = new HttpClient();
var baseUrl = "https://localhost:7072";

// Get all songs
var response = await httpClient.GetAsync($"{baseUrl}/api/homespeaker/songs");
var songs = await response.Content.ReadFromJsonAsync<Song[]>();

// Play the first song
if (songs?.Length > 0)
{
    await httpClient.PostAsync($"{baseUrl}/api/homespeaker/songs/{songs[0].SongId}/play", null);
}

// Get player status
var statusResponse = await httpClient.GetAsync($"{baseUrl}/api/homespeaker/player/status");
var status = await statusResponse.Content.ReadFromJsonAsync<PlayerStatus>();
```

## Migration from gRPC

The REST API provides complete feature parity with the gRPC interface:

| gRPC Method | REST Endpoint | Notes |
|-------------|---------------|-------|
| `GetSongs` | `GET /songs` | Supports folder filtering |
| `PlaySong` | `POST /songs/{id}/play` | Direct song playback |
| `EnqueueSong` | `POST /songs/{id}/enqueue` | Add to queue |
| `GetPlayerStatus` | `GET /player/status` | Current playback info |
| `PlayerControl` | `POST /player/control` | All control operations |
| `GetPlaylists` | `GET /playlists` | All playlist data |
| `PlayPlaylist` | `POST /playlists/{name}/play` | Start playlist |
| `SearchVideo` | `GET /youtube/search` | YouTube search |
| `CacheVideo` | `POST /youtube/cache` | Background download |

The main differences:
- **Streaming**: gRPC streaming is replaced with paginated results or background operations
- **Real-time Updates**: Consider using SignalR for real-time player events
- **Binary Data**: File streaming uses standard HTTP range requests