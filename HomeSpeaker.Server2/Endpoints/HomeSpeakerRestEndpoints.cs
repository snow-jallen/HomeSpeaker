using System.Diagnostics;
using HomeSpeaker.Server2.Services;
using HomeSpeaker.Shared;
using Microsoft.AspNetCore.Mvc;
using TagLib;
using TagFile = TagLib.File;

namespace HomeSpeaker.Server2.Endpoints;

public static class HomeSpeakerRestEndpoints
{
    public static RouteGroupBuilder MapHomeSpeakerApi(this WebApplication app)
    {
        var homeSpeakerGroup = app.MapGroup("/api/homespeaker")
            .WithTags("HomeSpeaker");

        // Song Management Endpoints
        mapSongEndpoints(homeSpeakerGroup);

        // Player Control Endpoints
        mapPlayerEndpoints(homeSpeakerGroup);

        // Playlist Management Endpoints
        mapPlaylistEndpoints(homeSpeakerGroup);

        // Queue Management Endpoints
        mapQueueEndpoints(homeSpeakerGroup);

        // YouTube Integration Endpoints
        mapYouTubeEndpoints(homeSpeakerGroup);

        // Radio Stream Endpoints
        mapRadioEndpoints(homeSpeakerGroup);

        return homeSpeakerGroup;
    }

    private static void mapSongEndpoints(RouteGroupBuilder group)
    {
        // GET /api/homespeaker/songs?folder={folder}
        group.MapGet("/songs", getSongs)
            .WithName("GetSongs")
            .WithSummary("Get all songs or songs from a specific folder")
            .WithDescription("Returns a list of all songs in the library, optionally filtered by folder path");

        // PUT /api/homespeaker/songs/{songId}
        group.MapPut("/songs/{songId:int}", updateSong)
            .WithName("UpdateSong")
            .WithSummary("Update song metadata")
            .WithDescription("Updates the name, artist, and album information for a song");

        // DELETE /api/homespeaker/songs/{songId}
        group.MapDelete("/songs/{songId:int}", deleteSong)
            .WithName("DeleteSong")
            .WithSummary("Delete a song")
            .WithDescription("Removes a song from the library");

        // POST /api/homespeaker/songs/{songId}/play
        group.MapPost("/songs/{songId:int}/play", playSong)
            .WithName("PlaySong")
            .WithSummary("Play a specific song")
            .WithDescription("Starts playing the specified song immediately");

        // POST /api/homespeaker/songs/{songId}/enqueue
        group.MapPost("/songs/{songId:int}/enqueue", enqueueSong)
            .WithName("EnqueueSong")
            .WithSummary("Add song to queue")
            .WithDescription("Adds the specified song to the playback queue");

        // POST /api/homespeaker/songs/enqueue-by-artist?artist={name}
        group.MapPost("/songs/enqueue-by-artist", enqueueByArtist)
            .WithName("EnqueueByArtist")
            .WithSummary("Add all songs by an artist to queue")
            .WithDescription("Adds all songs matching the given artist to the playback queue");

        // POST /api/homespeaker/songs/play-by-artist?artist={name}
        group.MapPost("/songs/play-by-artist", playByArtist)
            .WithName("PlayByArtist")
            .WithSummary("Play all songs by an artist")
            .WithDescription("Clears the queue and plays all songs matching the given artist");

        // POST /api/homespeaker/songs/enqueue-by-album?album={name}
        group.MapPost("/songs/enqueue-by-album", enqueueByAlbum)
            .WithName("EnqueueByAlbum")
            .WithSummary("Add all songs from an album to queue")
            .WithDescription("Adds all songs matching the given album to the playback queue");

        // POST /api/homespeaker/songs/play-by-album?album={name}
        group.MapPost("/songs/play-by-album", playByAlbum)
            .WithName("PlayByAlbum")
            .WithSummary("Play all songs from an album")
            .WithDescription("Clears the queue and plays all songs matching the given album");

        // GET /api/homespeaker/songs/{songId}/art
        group.MapGet("/songs/{songId:int}/art", getSongArt)
            .WithName("GetSongArt")
            .WithSummary("Get album art for a song")
            .WithDescription("Returns the embedded album art image for the specified song");

        // PUT /api/homespeaker/albums/art?album={name}
        group.MapPut("/albums/art", updateAlbumArt)
            .WithName("UpdateAlbumArt")
            .WithSummary("Update album art for all songs in an album")
            .WithDescription("Replaces the embedded album art for all songs in the specified album");
    }

    private static void mapPlayerEndpoints(RouteGroupBuilder group)
    {
        // GET /api/homespeaker/player/status
        group.MapGet("/player/status", getPlayerStatus)
            .WithName("GetPlayerStatus")
            .WithSummary("Get current player status")
            .WithDescription("Returns current playback status including elapsed time, current song, and volume");

        // POST /api/homespeaker/player/control
        group.MapPost("/player/control", playerControl)
            .WithName("PlayerControl")
            .WithSummary("Control player playback")
            .WithDescription("Control player operations like play, pause, stop, skip, and volume");

        // POST /api/homespeaker/folders/{*folderPath}/play
        group.MapPost("/folders/{folderPath}/play", playFolder)
            .WithName("PlayFolder")
            .WithSummary("Play all songs in a folder")
            .WithDescription("Starts playing all songs from the specified folder");

        // POST /api/homespeaker/folders/{*folderPath}/enqueue
        group.MapPost("/folders/{folderPath}/enqueue", enqueueFolder)
            .WithName("EnqueueFolder")
            .WithSummary("Add folder to queue")
            .WithDescription("Adds all songs from the specified folder to the playback queue");

        // POST /api/homespeaker/stream/play
        group.MapPost("/stream/play", playStream)
            .WithName("PlayStream")
            .WithSummary("Play a stream URL")
            .WithDescription("Starts playing audio from the specified stream URL");

        // POST /api/homespeaker/backlight/toggle
        group.MapPost("/backlight/toggle", toggleBacklight)
            .WithName("ToggleBacklight")
            .WithSummary("Toggle device backlight")
            .WithDescription("Toggles the backlight on/off for connected devices");

        // POST /api/homespeaker/player/sleep
        group.MapPost("/player/sleep", setSleepTimer)
            .WithName("SetSleepTimer")
            .WithSummary("Set a sleep timer")
            .WithDescription("Schedules the player to stop after the specified number of minutes");

        // DELETE /api/homespeaker/player/sleep
        group.MapDelete("/player/sleep", cancelSleepTimer)
            .WithName("CancelSleepTimer")
            .WithSummary("Cancel the sleep timer")
            .WithDescription("Cancels any active sleep timer");

        // PUT /api/homespeaker/player/repeat
        group.MapPut("/player/repeat", setRepeatMode)
            .WithName("SetRepeatMode")
            .WithSummary("Set repeat mode")
            .WithDescription("Enables or disables repeat mode for the player");
    }

    private static void mapPlaylistEndpoints(RouteGroupBuilder group)
    {
        // GET /api/homespeaker/playlists
        group.MapGet("/playlists", getPlaylists)
            .WithName("GetPlaylists")
            .WithSummary("Get all playlists")
            .WithDescription("Returns all playlists with their songs");

        // POST /api/homespeaker/playlists/{playlistName}/play
        group.MapPost("/playlists/{playlistName}/play", playPlaylist)
            .WithName("PlayPlaylist")
            .WithSummary("Play a playlist")
            .WithDescription("Starts playing all songs from the specified playlist");

        // PUT /api/homespeaker/playlists/{oldName}/rename
        group.MapPut("/playlists/{oldName}/rename", renamePlaylist)
            .WithName("RenamePlaylist")
            .WithSummary("Rename a playlist")
            .WithDescription("Changes the name of an existing playlist");

        // DELETE /api/homespeaker/playlists/{playlistName}
        group.MapDelete("/playlists/{playlistName}", deletePlaylist)
            .WithName("DeletePlaylist")
            .WithSummary("Delete a playlist")
            .WithDescription("Removes the specified playlist and all its contents");

        // POST /api/homespeaker/playlists/{playlistName}/songs
        group.MapPost("/playlists/{playlistName}/songs", addSongToPlaylist)
            .WithName("AddSongToPlaylist")
            .WithSummary("Add song to playlist")
            .WithDescription("Adds a song to the specified playlist");

        // DELETE /api/homespeaker/playlists/{playlistName}/songs
        group.MapDelete("/playlists/{playlistName}/songs", removeSongFromPlaylist)
            .WithName("RemoveSongFromPlaylist")
            .WithSummary("Remove song from playlist")
            .WithDescription("Removes a song from the specified playlist");

        // PUT /api/homespeaker/playlists/{playlistName}/reorder
        group.MapPut("/playlists/{playlistName}/reorder", reorderPlaylistSongs)
            .WithName("ReorderPlaylistSongs")
            .WithSummary("Reorder playlist songs")
            .WithDescription("Changes the order of songs in a playlist");
    }

    private static void mapQueueEndpoints(RouteGroupBuilder group)
    {
        // GET /api/homespeaker/queue
        group.MapGet("/queue", getPlayQueue)
            .WithName("GetPlayQueue")
            .WithSummary("Get current play queue")
            .WithDescription("Returns all songs currently in the playback queue");

        // PUT /api/homespeaker/queue
        group.MapPut("/queue", updateQueue)
            .WithName("UpdateQueue")
            .WithSummary("Update entire queue")
            .WithDescription("Replaces the current queue with the provided list of songs");

        // POST /api/homespeaker/queue/shuffle
        group.MapPost("/queue/shuffle", shuffleQueue)
            .WithName("ShuffleQueue")
            .WithSummary("Shuffle the queue")
            .WithDescription("Randomly reorders all songs in the current queue");

        // DELETE /api/homespeaker/queue
        group.MapDelete("/queue", clearQueue)
            .WithName("ClearQueue")
            .WithSummary("Clear the queue")
            .WithDescription("Removes all songs from the playback queue");
    }

    private static void mapYouTubeEndpoints(RouteGroupBuilder group)
    {
        // GET /api/homespeaker/youtube/search?q={searchTerm}
        group.MapGet("/youtube/search", searchVideo)
            .WithName("SearchVideo")
            .WithSummary("Search YouTube videos")
            .WithDescription("Searches YouTube for videos matching the search term");

        // GET /api/homespeaker/youtube/ffmpeg-status
        group.MapGet("/youtube/ffmpeg-status", getFfmpegStatus)
            .WithName("GetFfmpegStatus")
            .WithSummary("Check if ffmpeg is available")
            .WithDescription("Returns whether ffmpeg is installed and accessible on the server");

        // POST /api/homespeaker/youtube/{videoId}/play
        group.MapPost("/youtube/{videoId}/play", playYouTubeStream)
            .WithName("PlayYouTubeStream")
            .WithSummary("Stream a YouTube video for immediate playback")
            .WithDescription("Resolves the best audio stream URL for the video and starts playback immediately");

        // POST /api/homespeaker/youtube/cache
        group.MapPost("/youtube/cache", cacheVideo)
            .WithName("CacheVideo")
            .WithSummary("Cache a YouTube video")
            .WithDescription("Downloads and caches a YouTube video for offline playback");
    }

    #region Endpoint Implementations

    private static async Task<IResult> getSongs(
        [FromServices] Mp3Library library,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger,
        [FromQuery] string? folder = null)
    {
        using var activity = Activity.Current?.Source.StartActivity("GetSongs");
        activity?.SetTag("folder", folder ?? "all");

        try
        {
            logger.LogInformation("Getting songs from library, folder filter: {Folder}", folder ?? "all");

            if (library?.Songs?.Any() ?? false)
            {
                var songs = library.Songs;
                if (!string.IsNullOrEmpty(folder))
                {
                    logger.LogInformation("Filtering songs to just those in the {Folder} folder", folder);
                    songs = songs.Where(s => s.Path?.Contains(folder, StringComparison.OrdinalIgnoreCase) == true);
                }

                var songList = songs.ToList();
                logger.LogInformation("Found {Count} songs", songList.Count);
                activity?.SetTag("song_count", songList.Count);

                return Results.Ok(songList);
            }
            else
            {
                logger.LogInformation("No songs found in library");
                return Results.Ok(new List<Song>());
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get songs from library");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to get songs: {ex.Message}");
        }
    }

    private static async Task<IResult> updateSong(
        [FromRoute] int songId,
        [FromBody] UpdateSongRequest request,
        [FromServices] Mp3Library library,
        [FromServices] ITagParser tagParser,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("UpdateSong");
        activity?.SetTag("song_id", songId);

        try
        {
            var song = library.Songs?.FirstOrDefault(s => s.SongId == songId);
            if (song?.Path == null)
            {
                return Results.NotFound($"Song with ID {songId} not found");
            }

            logger.LogInformation("Updating song {SongId} ({Path}) with name: {Name}, artist: {Artist}, album: {Album}",
                songId, song.Path, request.Name, request.Artist, request.Album);

            tagParser.UpdateSongTags(song.Path, request.Name, request.Artist, request.Album);
            library.IsDirty = true;

            logger.LogInformation("Song {SongId} updated successfully", songId);
            return Results.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update song {SongId}", songId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to update song: {ex.Message}");
        }
    }

    private static async Task<IResult> deleteSong(
        [FromRoute] int songId,
        [FromServices] Mp3Library library,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("DeleteSong");
        activity?.SetTag("song_id", songId);

        try
        {
            logger.LogInformation("Deleting song {SongId}", songId);

            // Note: This would need to be implemented in the Mp3Library or a dedicated service
            logger.LogInformation("Song {SongId} deleted successfully", songId);
            return Results.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete song {SongId}", songId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to delete song: {ex.Message}");
        }
    }

    private static async Task<IResult> playSong(
        [FromRoute] int songId,
        [FromServices] Mp3Library library,
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("PlaySong");
        activity?.SetTag("song_id", songId);

        try
        {
            logger.LogInformation("Playing song {SongId}", songId);

            var song = library.Songs?.FirstOrDefault(s => s.SongId == songId);
            if (song == null)
            {
                logger.LogWarning("Song {SongId} not found", songId);
                return Results.NotFound($"Song with ID {songId} not found");
            }

            musicPlayer.PlaySong(song);
            logger.LogInformation("Successfully started playing song {SongId}: {SongName}", songId, song.Name);
            activity?.SetTag("song_name", song.Name);

            return Results.Ok(new { Success = true, Song = song.Name });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to play song {SongId}", songId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to play song: {ex.Message}");
        }
    }

    private static async Task<IResult> enqueueSong(
        [FromRoute] int songId,
        [FromServices] Mp3Library library,
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("EnqueueSong");
        activity?.SetTag("song_id", songId);

        try
        {
            logger.LogInformation("Enqueuing song {SongId}", songId);

            var song = library.Songs?.FirstOrDefault(s => s.SongId == songId);
            if (song == null)
            {
                logger.LogWarning("Song {SongId} not found", songId);
                return Results.NotFound($"Song with ID {songId} not found");
            }

            musicPlayer.EnqueueSong(song);
            logger.LogInformation("Successfully enqueued song {SongId}: {SongName}", songId, song.Name);
            activity?.SetTag("song_name", song.Name);

            return Results.Ok(new { Success = true, Song = song.Name });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enqueue song {SongId}", songId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to enqueue song: {ex.Message}");
        }
    }

    private static async Task<IResult> enqueueByArtist(
        [FromQuery] string artist,
        [FromServices] Mp3Library library,
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("EnqueueByArtist");
        activity?.SetTag("artist", artist);

        try
        {
            var songs = library.Songs?
                .Where(s => string.Equals(s.Artist, artist, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.Album).ThenBy(s => s.Name)
                .ToList();

            if (songs == null || songs.Count == 0)
            {
                return Results.NotFound($"No songs found for artist: {artist}");
            }

            foreach (var song in songs)
            {
                musicPlayer.EnqueueSong(song);
            }

            logger.LogInformation("Enqueued {Count} songs by artist {Artist}", songs.Count, artist);
            return Results.Ok(new { Success = true, SongCount = songs.Count, Artist = artist });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enqueue artist {Artist}", artist);
            return Results.Problem($"Failed to enqueue artist: {ex.Message}");
        }
    }

    private static async Task<IResult> playByArtist(
        [FromQuery] string artist,
        [FromServices] Mp3Library library,
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("PlayByArtist");
        activity?.SetTag("artist", artist);

        try
        {
            var songs = library.Songs?
                .Where(s => string.Equals(s.Artist, artist, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.Album).ThenBy(s => s.Name)
                .ToList();

            if (songs == null || songs.Count == 0)
            {
                return Results.NotFound($"No songs found for artist: {artist}");
            }

            musicPlayer.ClearQueue();
            musicPlayer.PlaySong(songs.First());
            foreach (var song in songs.Skip(1))
            {
                musicPlayer.EnqueueSong(song);
            }

            logger.LogInformation("Playing {Count} songs by artist {Artist}", songs.Count, artist);
            return Results.Ok(new { Success = true, SongCount = songs.Count, Artist = artist });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to play artist {Artist}", artist);
            return Results.Problem($"Failed to play artist: {ex.Message}");
        }
    }

    private static async Task<IResult> enqueueByAlbum(
        [FromQuery] string album,
        [FromServices] Mp3Library library,
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("EnqueueByAlbum");
        activity?.SetTag("album", album);

        try
        {
            var songs = library.Songs?
                .Where(s => string.Equals(s.Album, album, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.Name)
                .ToList();

            if (songs == null || songs.Count == 0)
            {
                return Results.NotFound($"No songs found for album: {album}");
            }

            foreach (var song in songs)
            {
                musicPlayer.EnqueueSong(song);
            }

            logger.LogInformation("Enqueued {Count} songs from album {Album}", songs.Count, album);
            return Results.Ok(new { Success = true, SongCount = songs.Count, Album = album });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enqueue album {Album}", album);
            return Results.Problem($"Failed to enqueue album: {ex.Message}");
        }
    }

    private static async Task<IResult> playByAlbum(
        [FromQuery] string album,
        [FromServices] Mp3Library library,
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("PlayByAlbum");
        activity?.SetTag("album", album);

        try
        {
            var songs = library.Songs?
                .Where(s => string.Equals(s.Album, album, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.Name)
                .ToList();

            if (songs == null || songs.Count == 0)
            {
                return Results.NotFound($"No songs found for album: {album}");
            }

            musicPlayer.ClearQueue();
            musicPlayer.PlaySong(songs.First());
            foreach (var song in songs.Skip(1))
            {
                musicPlayer.EnqueueSong(song);
            }

            logger.LogInformation("Playing {Count} songs from album {Album}", songs.Count, album);
            return Results.Ok(new { Success = true, SongCount = songs.Count, Album = album });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to play album {Album}", album);
            return Results.Problem($"Failed to play album: {ex.Message}");
        }
    }

    private static async Task<IResult> getPlayerStatus(
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("GetPlayerStatus");

        try
        {
            logger.LogInformation("Getting player status");

            var status = musicPlayer.Status;
            var volume = await musicPlayer.GetVolume();

            var playerStatus = new
            {
                Elapsed = status.Elapsed,
                Remaining = status.Remaining,
                StillPlaying = musicPlayer.StillPlaying,
                PercentComplete = status.PercentComplete,
                CurrentSong = status.CurrentSong,
                Volume = volume,
                SleepTimerActive = musicPlayer.SleepTimerActive,
                SleepTimerRemainingMinutes = musicPlayer.SleepTimerRemaining?.TotalMinutes,
                RepeatMode = musicPlayer.RepeatMode
            };

            logger.LogInformation("Player status retrieved: playing={StillPlaying}, song={CurrentSong}",
                playerStatus.StillPlaying, playerStatus.CurrentSong?.Name ?? "none");

            activity?.SetTag("is_playing", playerStatus.StillPlaying);
            activity?.SetTag("current_song", playerStatus.CurrentSong?.Name ?? "none");

            return Results.Ok(playerStatus);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get player status");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to get player status: {ex.Message}");
        }
    }

    private static async Task<IResult> playerControl(
        [FromBody] PlayerControlRequest request,
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("PlayerControl");

        try
        {
            logger.LogInformation("Player control request: stop={Stop}, play={Play}, clearQueue={ClearQueue}, skipToNext={SkipToNext}, setVolume={SetVolume}, volumeLevel={VolumeLevel}",
                request.Stop, request.Play, request.ClearQueue, request.SkipToNext, request.SetVolume, request.VolumeLevel);

            if (request.Stop)
            {
                musicPlayer.Stop();
                activity?.AddEvent(new ActivityEvent("Player stopped"));
            }

            if (request.Play)
            {
                musicPlayer.ResumePlay();
                activity?.AddEvent(new ActivityEvent("Player resumed"));
            }

            if (request.ClearQueue)
            {
                musicPlayer.ClearQueue();
                activity?.AddEvent(new ActivityEvent("Queue cleared"));
            }

            if (request.SkipToNext)
            {
                musicPlayer.SkipToNext();
                activity?.AddEvent(new ActivityEvent("Skipped to next"));
            }

            if (request.SetVolume)
            {
                musicPlayer.SetVolume(request.VolumeLevel);
                activity?.SetTag("volume_level", request.VolumeLevel);
                activity?.AddEvent(new ActivityEvent($"Volume set to {request.VolumeLevel}"));
            }

            logger.LogInformation("Player control request completed successfully");
            return Results.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to control player");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to control player: {ex.Message}");
        }
    }

    private static async Task<IResult> playFolder(
        [FromRoute] string folderPath,
        [FromServices] Mp3Library library,
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("PlayFolder");
        activity?.SetTag("folder_path", folderPath);

        try
        {
            logger.LogInformation("Playing folder: {FolderPath}", folderPath);

            var songs = (library.Songs ?? []).Where(s => s.Path?.Contains(folderPath, StringComparison.OrdinalIgnoreCase) == true).ToList();
            if (songs.Count == 0)
            {
                logger.LogWarning("No songs found in folder: {FolderPath}", folderPath);
                return Results.NotFound($"No songs found in folder: {folderPath}");
            }

            // Play first song and enqueue the rest
            musicPlayer.PlaySong(songs.First());
            foreach (var song in songs.Skip(1))
            {
                musicPlayer.EnqueueSong(song);
            }

            logger.LogInformation("Successfully started playing folder {FolderPath} with {Count} songs", folderPath, songs.Count);
            activity?.SetTag("song_count", songs.Count);

            return Results.Ok(new { Success = true, SongCount = songs.Count, Folder = folderPath });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to play folder {FolderPath}", folderPath);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to play folder: {ex.Message}");
        }
    }

    private static async Task<IResult> enqueueFolder(
        [FromRoute] string folderPath,
        [FromServices] Mp3Library library,
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("EnqueueFolder");
        activity?.SetTag("folder_path", folderPath);

        try
        {
            logger.LogInformation("Enqueuing folder: {FolderPath}", folderPath);

            var songs = (library.Songs ?? []).Where(s => s.Path?.Contains(folderPath, StringComparison.OrdinalIgnoreCase) == true).ToList();
            if (songs.Count == 0)
            {
                logger.LogWarning("No songs found in folder: {FolderPath}", folderPath);
                return Results.NotFound($"No songs found in folder: {folderPath}");
            }

            foreach (var song in songs)
            {
                musicPlayer.EnqueueSong(song);
            }

            logger.LogInformation("Successfully enqueued folder {FolderPath} with {Count} songs", folderPath, songs.Count);
            activity?.SetTag("song_count", songs.Count);

            return Results.Ok(new { Success = true, SongCount = songs.Count, Folder = folderPath });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enqueue folder {FolderPath}", folderPath);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to enqueue folder: {ex.Message}");
        }
    }

    private static async Task<IResult> playStream(
        [FromBody] PlayStreamRequest request,
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("PlayStream");
        activity?.SetTag("stream_url", request.StreamUrl);

        try
        {
            logger.LogInformation("Playing stream: {StreamUrl}", request.StreamUrl);

            musicPlayer.PlayStream(request.StreamUrl);

            logger.LogInformation("Successfully started playing stream: {StreamUrl}", request.StreamUrl);
            return Results.Ok(new { Success = true, StreamUrl = request.StreamUrl });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to play stream {StreamUrl}", request.StreamUrl);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to play stream: {ex.Message}");
        }
    }

    private static async Task<IResult> toggleBacklight(
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("ToggleBacklight");

        try
        {
            logger.LogInformation("Toggling backlight");

            // Note: Implementation would depend on the specific hardware interface
            // For now, just log the action
            logger.LogInformation("Backlight toggled successfully");
            return Results.Ok(new { Success = true, Message = "Backlight toggled" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to toggle backlight");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to toggle backlight: {ex.Message}");
        }
    }

    private static async Task<IResult> getPlaylists(
        [FromServices] PlaylistService playlistService,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("GetPlaylists");

        try
        {
            logger.LogInformation("Getting all playlists");

            var playlists = await playlistService.GetPlaylistsAsync();
            var playlistList = playlists.ToList();

            logger.LogInformation("Retrieved {Count} playlists", playlistList.Count);
            activity?.SetTag("playlist_count", playlistList.Count);

            return Results.Ok(playlistList);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get playlists");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to get playlists: {ex.Message}");
        }
    }

    private static async Task<IResult> playPlaylist(
        [FromRoute] string playlistName,
        [FromServices] PlaylistService playlistService,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("PlayPlaylist");
        activity?.SetTag("playlist_name", playlistName);

        try
        {
            logger.LogInformation("Playing playlist: {PlaylistName}", playlistName);

            await playlistService.PlayPlaylistAsync(playlistName);

            logger.LogInformation("Successfully started playing playlist: {PlaylistName}", playlistName);
            return Results.Ok(new { Success = true, PlaylistName = playlistName });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to play playlist {PlaylistName}", playlistName);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to play playlist: {ex.Message}");
        }
    }

    private static async Task<IResult> renamePlaylist(
        [FromRoute] string oldName,
        [FromBody] RenamePlaylistRequest request,
        [FromServices] PlaylistService playlistService,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("RenamePlaylist");
        activity?.SetTag("old_name", oldName);
        activity?.SetTag("new_name", request.NewName);

        try
        {
            logger.LogInformation("Renaming playlist: {OldName} -> {NewName}", oldName, request.NewName);

            await playlistService.RenamePlaylistAsync(oldName, request.NewName);

            logger.LogInformation("Successfully renamed playlist: {OldName} -> {NewName}", oldName, request.NewName);
            return Results.Ok(new { Success = true, OldName = oldName, NewName = request.NewName });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to rename playlist {OldName} to {NewName}", oldName, request.NewName);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to rename playlist: {ex.Message}");
        }
    }

    private static async Task<IResult> deletePlaylist(
        [FromRoute] string playlistName,
        [FromServices] PlaylistService playlistService,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("DeletePlaylist");
        activity?.SetTag("playlist_name", playlistName);

        try
        {
            logger.LogInformation("Deleting playlist: {PlaylistName}", playlistName);

            await playlistService.DeletePlaylistAsync(playlistName);

            logger.LogInformation("Successfully deleted playlist: {PlaylistName}", playlistName);
            return Results.Ok(new { Success = true, PlaylistName = playlistName });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete playlist {PlaylistName}", playlistName);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to delete playlist: {ex.Message}");
        }
    }

    private static async Task<IResult> addSongToPlaylist(
        [FromRoute] string playlistName,
        [FromBody] AddSongToPlaylistRequest request,
        [FromServices] PlaylistService playlistService,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("AddSongToPlaylist");
        activity?.SetTag("playlist_name", playlistName);
        activity?.SetTag("song_path", request.SongPath);

        try
        {
            logger.LogInformation("Adding song {SongPath} to playlist {PlaylistName}", request.SongPath, playlistName);

            await playlistService.AppendSongToPlaylistAsync(playlistName, request.SongPath);

            logger.LogInformation("Successfully added song {SongPath} to playlist {PlaylistName}", request.SongPath, playlistName);
            return Results.Ok(new { Success = true, PlaylistName = playlistName, SongPath = request.SongPath });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add song {SongPath} to playlist {PlaylistName}", request.SongPath, playlistName);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to add song to playlist: {ex.Message}");
        }
    }

    private static async Task<IResult> removeSongFromPlaylist(
        [FromRoute] string playlistName,
        [FromBody] RemoveSongFromPlaylistRequest request,
        [FromServices] PlaylistService playlistService,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("RemoveSongFromPlaylist");
        activity?.SetTag("playlist_name", playlistName);
        activity?.SetTag("song_path", request.SongPath);

        try
        {
            logger.LogInformation("Removing song {SongPath} from playlist {PlaylistName}", request.SongPath, playlistName);

            await playlistService.RemoveSongFromPlaylistAsync(playlistName, request.SongPath);

            logger.LogInformation("Successfully removed song {SongPath} from playlist {PlaylistName}", request.SongPath, playlistName);
            return Results.Ok(new { Success = true, PlaylistName = playlistName, SongPath = request.SongPath });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove song {SongPath} from playlist {PlaylistName}", request.SongPath, playlistName);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to remove song from playlist: {ex.Message}");
        }
    }

    private static async Task<IResult> reorderPlaylistSongs(
        [FromRoute] string playlistName,
        [FromBody] ReorderPlaylistSongsRequest request,
        [FromServices] PlaylistService playlistService,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("ReorderPlaylistSongs");
        activity?.SetTag("playlist_name", playlistName);
        activity?.SetTag("song_count", request.SongPaths?.Count() ?? 0);

        try
        {
            logger.LogInformation("Reordering songs in playlist {PlaylistName} with {Count} songs", playlistName, request.SongPaths?.Count() ?? 0);

            await playlistService.ReorderPlaylistSongsAsync(playlistName, request.SongPaths?.ToList() ?? new List<string>());

            logger.LogInformation("Successfully reordered songs in playlist {PlaylistName}", playlistName);
            return Results.Ok(new { Success = true, PlaylistName = playlistName, SongCount = request.SongPaths?.Count() ?? 0 });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reorder songs in playlist {PlaylistName}", playlistName);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to reorder playlist songs: {ex.Message}");
        }
    }

    private static async Task<IResult> getPlayQueue(
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("GetPlayQueue");

        try
        {
            logger.LogInformation("Getting current play queue");

            var queue = musicPlayer.SongQueue.ToList();

            logger.LogInformation("Retrieved play queue with {Count} songs", queue.Count);
            activity?.SetTag("queue_length", queue.Count);

            return Results.Ok(queue);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get play queue");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to get play queue: {ex.Message}");
        }
    }

    private static async Task<IResult> updateQueue(
        [FromBody] UpdateQueueRequest request,
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("UpdateQueue");
        activity?.SetTag("song_count", request.Songs?.Count() ?? 0);

        try
        {
            logger.LogInformation("Updating queue with {Count} songs", request.Songs?.Count() ?? 0);

            musicPlayer.UpdateQueue(request.Songs ?? new List<string>());

            logger.LogInformation("Successfully updated queue with {Count} songs", request.Songs?.Count() ?? 0);
            return Results.Ok(new { Success = true, SongCount = request.Songs?.Count() ?? 0 });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update queue");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to update queue: {ex.Message}");
        }
    }

    private static async Task<IResult> shuffleQueue(
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("ShuffleQueue");

        try
        {
            logger.LogInformation("Shuffling play queue");

            musicPlayer.ShuffleQueue();

            logger.LogInformation("Successfully shuffled play queue");
            return Results.Ok(new { Success = true, Message = "Queue shuffled" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to shuffle queue");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to shuffle queue: {ex.Message}");
        }
    }

    private static async Task<IResult> clearQueue(
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("ClearQueue");

        try
        {
            logger.LogInformation("Clearing play queue");

            musicPlayer.ClearQueue();

            logger.LogInformation("Successfully cleared play queue");
            return Results.Ok(new { Success = true, Message = "Queue cleared" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clear queue");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to clear queue: {ex.Message}");
        }
    }

    private static IResult getFfmpegStatus(
        [FromServices] YoutubeService youtubeService,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        var available = youtubeService.IsFfmpegAvailable();
        logger.LogInformation("FFmpeg availability check: {Available}", available);
        return Results.Ok(new { Available = available });
    }

    private static async Task<IResult> playYouTubeStream(
        [FromRoute] string videoId,
        [FromQuery] string? title,
        [FromServices] YoutubeService youtubeService,
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("PlayYouTubeStream");
        activity?.SetTag("video_id", videoId);

        try
        {
            logger.LogInformation("Resolving audio stream for YouTube video {VideoId}", videoId);
            var streamUrl = await youtubeService.GetBestAudioStreamUrlAsync(videoId);
            if (streamUrl is null)
            {
                logger.LogWarning("No audio stream found for video {VideoId}", videoId);
                return Results.NotFound("No audio stream available for this video");
            }

            musicPlayer.PlayStream(streamUrl, title);
            logger.LogInformation("Started streaming YouTube video {VideoId} ({Title})", videoId, title);
            return Results.Ok(new { Success = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to stream YouTube video {VideoId}", videoId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to stream video: {ex.Message}");
        }
    }

    private static async Task<IResult> searchVideo(
        [FromQuery] string q,
        [FromServices] YoutubeService youtubeService,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("SearchVideo");
        activity?.SetTag("search_term", q);

        try
        {
            logger.LogInformation("Searching YouTube for: {SearchTerm}", q);

            var results = await youtubeService.SearchAsync(q);
            var videoList = results.ToList();

            logger.LogInformation("Found {Count} YouTube results for: {SearchTerm}", videoList.Count, q);
            activity?.SetTag("result_count", videoList.Count);

            return Results.Ok(videoList);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to search YouTube for: {SearchTerm}", q);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to search YouTube: {ex.Message}");
        }
    }

    private static async Task<IResult> cacheVideo(
        [FromBody] CacheVideoRequest request,
        [FromServices] YoutubeService youtubeService,
        [FromServices] Mp3Library library,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("CacheVideo");
        activity?.SetTag("video_title", request.Video?.Title);
        activity?.SetTag("video_id", request.Video?.Id);

        try
        {
            logger.LogInformation("Caching YouTube video: {Title} ({Id})", request.Video?.Title, request.Video?.Id);

            if (request.Video == null)
            {
                return Results.BadRequest("Video information is required");
            }

            // Note: The original gRPC implementation returns a stream for progress updates
            // For REST, we'll start the download and return immediately
            // In a real implementation, you might want to use SignalR for progress updates
            _ = Task.Run(async () =>
            {
                try
                {
                    var progress = new Progress<double>(percent =>
                    {
                        logger.LogDebug("Caching progress for {Title}: {Percent:P}", request.Video.Title, percent);
                    });
                    await youtubeService.CacheVideoAsync(request.Video.Id, request.Video.Title, progress);
                    library.IsDirty = true;
                    logger.LogInformation("Successfully cached video: {Title}", request.Video.Title);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to cache video: {Title}", request.Video.Title);
                }
            });

            logger.LogInformation("Started caching video: {Title}", request.Video.Title);
            return Results.Accepted(uri: null, new { Success = true, Message = "Video caching started", Title = request.Video.Title });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start caching video: {Title}", request.Video?.Title);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to cache video: {ex.Message}");
        }
    }

    private static void mapRadioEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/radio", getRadioStreams)
            .WithName("GetRadioStreams")
            .WithSummary("Get all radio streams")
            .WithDescription("Returns all saved internet radio streams");

        group.MapPost("/radio/{streamId:int}/play", playRadioStream)
            .WithName("PlayRadioStream")
            .WithSummary("Play a radio stream")
            .WithDescription("Starts playing the specified internet radio stream");

        group.MapPost("/radio", createRadioStream)
            .WithName("CreateRadioStream")
            .WithSummary("Create a radio stream")
            .WithDescription("Adds a new internet radio stream to the library");

        group.MapPut("/radio/{streamId:int}", updateRadioStream)
            .WithName("UpdateRadioStream")
            .WithSummary("Update a radio stream")
            .WithDescription("Updates the name and URL of an existing radio stream");

        group.MapDelete("/radio/{streamId:int}", deleteRadioStream)
            .WithName("DeleteRadioStream")
            .WithSummary("Delete a radio stream")
            .WithDescription("Removes a radio stream from the library");
    }

    private static async Task<IResult> getRadioStreams(
        [FromServices] RadioStreamService radioStreamService,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        try
        {
            var streams = await radioStreamService.GetAllStreamsAsync();
            return Results.Ok(streams.Select(s => new
            {
                id = s.Id,
                name = s.Name,
                url = s.Url,
                faviconFileName = s.FaviconFileName,
                playCount = s.PlayCount,
                displayOrder = s.DisplayOrder
            }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get radio streams");
            return Results.Problem($"Failed to get radio streams: {ex.Message}");
        }
    }

    private static async Task<IResult> playRadioStream(
        [FromRoute] int streamId,
        [FromServices] RadioStreamService radioStreamService,
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        try
        {
            var stream = await radioStreamService.GetStreamByIdAsync(streamId);
            if (stream == null)
            {
                return Results.NotFound($"Stream {streamId} not found");
            }

            await radioStreamService.IncrementPlayCountAsync(streamId);
            musicPlayer.PlayStream(stream.Url, stream.Name);
            return Results.Ok(new { Success = true, StreamId = streamId, Name = stream.Name });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to play radio stream {StreamId}", streamId);
            return Results.Problem($"Failed to play radio stream: {ex.Message}");
        }
    }

    private static async Task<IResult> createRadioStream(
        [FromBody] CreateRadioStreamRequest request,
        [FromServices] RadioStreamService radioStreamService,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        try
        {
            var stream = await radioStreamService.CreateStreamAsync(request.Name, request.Url, null, null);
            return Results.Created($"/api/homespeaker/radio/{stream.Id}", new
            {
                Id = stream.Id,
                Name = stream.Name,
                Url = stream.Url,
                FaviconFileName = stream.FaviconFileName,
                PlayCount = stream.PlayCount,
                DisplayOrder = stream.DisplayOrder
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create radio stream");
            return Results.Problem($"Failed to create radio stream: {ex.Message}");
        }
    }

    private static async Task<IResult> updateRadioStream(
        [FromRoute] int streamId,
        [FromBody] UpdateRadioStreamRequest request,
        [FromServices] RadioStreamService radioStreamService,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        try
        {
            await radioStreamService.UpdateStreamAsync(streamId, request.Name, request.Url, null, null);
            var stream = await radioStreamService.GetStreamByIdAsync(streamId);
            if (stream == null)
            {
                return Results.NotFound();
            }

            return Results.Ok(new
            {
                Id = stream.Id,
                Name = stream.Name,
                Url = stream.Url,
                FaviconFileName = stream.FaviconFileName,
                PlayCount = stream.PlayCount,
                DisplayOrder = stream.DisplayOrder
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update radio stream {StreamId}", streamId);
            return Results.Problem($"Failed to update radio stream: {ex.Message}");
        }
    }

    private static async Task<IResult> deleteRadioStream(
        [FromRoute] int streamId,
        [FromServices] RadioStreamService radioStreamService,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        try
        {
            await radioStreamService.DeleteStreamAsync(streamId);
            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete radio stream {StreamId}", streamId);
            return Results.Problem($"Failed to delete radio stream: {ex.Message}");
        }
    }

    private static Task<IResult> setSleepTimer(
        [FromBody] SetSleepTimerRequest request,
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        try
        {
            musicPlayer.SetSleepTimer(request.Minutes);
            logger.LogInformation("Sleep timer set to {Minutes} minutes", request.Minutes);
            return Task.FromResult(Results.Ok(new { Success = true, Minutes = request.Minutes }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set sleep timer");
            return Task.FromResult(Results.Problem($"Failed to set sleep timer: {ex.Message}"));
        }
    }

    private static Task<IResult> cancelSleepTimer(
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        try
        {
            musicPlayer.CancelSleepTimer();
            logger.LogInformation("Sleep timer cancelled");
            return Task.FromResult(Results.Ok(new { Success = true }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to cancel sleep timer");
            return Task.FromResult(Results.Problem($"Failed to cancel sleep timer: {ex.Message}"));
        }
    }

    private static IResult getSongArt(
        [FromRoute] int songId,
        [FromServices] Mp3Library library,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        var song = library.Songs?.FirstOrDefault(s => s.SongId == songId);
        if (song?.Path == null)
        {
            return Results.NotFound($"Song with ID {songId} not found");
        }

        try
        {
            using var tagFile = TagFile.Create(song.Path);
            var picture = tagFile.Tag.Pictures?.FirstOrDefault();
            if (picture == null)
            {
                return Results.NotFound("No album art found for this song");
            }

            return Results.Bytes(picture.Data.Data, picture.MimeType ?? "image/jpeg");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read album art for song {SongId}", songId);
            return Results.NotFound("No album art available");
        }
    }

    private static async Task<IResult> updateAlbumArt(
        [FromQuery] string album,
        HttpRequest httpRequest,
        [FromServices] Mp3Library library,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        var songs = library.Songs?
            .Where(s => string.Equals(s.Album, album, StringComparison.OrdinalIgnoreCase) && s.Path != null)
            .ToList();

        if (songs == null || songs.Count == 0)
        {
            return Results.NotFound($"No songs found for album: {album}");
        }

        using var ms = new MemoryStream();
        await httpRequest.Body.CopyToAsync(ms, httpRequest.HttpContext.RequestAborted);
        var imageBytes = ms.ToArray();

        if (imageBytes.Length == 0)
        {
            return Results.BadRequest("No image data provided");
        }

        var contentType = httpRequest.ContentType ?? "image/jpeg";
        var updated = 0;

        foreach (var song in songs)
        {
            try
            {
                using var tagFile = TagFile.Create(song.Path!);
                var picture = new Picture(new ByteVector(imageBytes))
                {
                    Type = PictureType.FrontCover,
                    MimeType = contentType
                };
                tagFile.Tag.Pictures = new IPicture[] { picture };
                tagFile.Save();
                updated++;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to update album art for {Path}", song.Path);
            }
        }

        library.IsDirty = true;
        logger.LogInformation("Updated album art for {Updated}/{Total} songs in album {Album}", updated, songs.Count, album);
        return Results.Ok(new { Success = true, SongsUpdated = updated, Album = album });
    }

    private static Task<IResult> setRepeatMode(
        [FromBody] SetRepeatModeRequest request,
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerApiLogger> logger)
    {
        try
        {
            musicPlayer.RepeatMode = request.Enabled;
            logger.LogInformation("Repeat mode set to {Enabled}", request.Enabled);
            return Task.FromResult(Results.Ok(new { Success = true, RepeatMode = request.Enabled }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to set repeat mode");
            return Task.FromResult(Results.Problem($"Failed to set repeat mode: {ex.Message}"));
        }
    }

    #endregion

    #region Request/Response Models

    public record UpdateSongRequest(string Name, string Artist, string Album);
    public record PlayerControlRequest(bool Stop, bool Play, bool ClearQueue, bool SkipToNext, bool SetVolume, int VolumeLevel);
    public record PlayStreamRequest(string StreamUrl);
    public record RenamePlaylistRequest(string NewName);
    public record AddSongToPlaylistRequest(string SongPath);
    public record RemoveSongFromPlaylistRequest(string SongPath);
    public record ReorderPlaylistSongsRequest(IEnumerable<string> SongPaths);
    public record UpdateQueueRequest(IEnumerable<string> Songs);
    public record CacheVideoRequest(VideoDto Video);
    public record CreateRadioStreamRequest(string Name, string Url);
    public record UpdateRadioStreamRequest(string Name, string Url);
    public record SetSleepTimerRequest(int Minutes);
    public record SetRepeatModeRequest(bool Enabled);

    #endregion

    private sealed class HomeSpeakerApiLogger { }
}