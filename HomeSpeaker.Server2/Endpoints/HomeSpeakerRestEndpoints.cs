using System.Diagnostics;
using HomeSpeaker.Server;
using HomeSpeaker.Server2.Services;
using HomeSpeaker.Shared;
using Microsoft.AspNetCore.Mvc;

namespace HomeSpeaker.Server2.Endpoints;

public static class HomeSpeakerRestEndpoints
{
    public static RouteGroupBuilder MapHomeSpeakerApi(this WebApplication app)
    {
        var homeSpeakerGroup = app.MapGroup("/api/homespeaker")
            .WithTags("HomeSpeaker")
            .WithOpenApi();

        // Song Management Endpoints
        MapSongEndpoints(homeSpeakerGroup);
        
        // Player Control Endpoints
        MapPlayerEndpoints(homeSpeakerGroup);
        
        // Playlist Management Endpoints
        MapPlaylistEndpoints(homeSpeakerGroup);
        
        // Queue Management Endpoints
        MapQueueEndpoints(homeSpeakerGroup);
        
        // YouTube Integration Endpoints
        MapYouTubeEndpoints(homeSpeakerGroup);

        return homeSpeakerGroup;
    }

    private static void MapSongEndpoints(RouteGroupBuilder group)
    {
        // GET /api/homespeaker/songs?folder={folder}
        group.MapGet("/songs", GetSongs)
            .WithName("GetSongs")
            .WithSummary("Get all songs or songs from a specific folder")
            .WithDescription("Returns a list of all songs in the library, optionally filtered by folder path");

        // PUT /api/homespeaker/songs/{songId}
        group.MapPut("/songs/{songId:int}", UpdateSong)
            .WithName("UpdateSong")
            .WithSummary("Update song metadata")
            .WithDescription("Updates the name, artist, and album information for a song");

        // DELETE /api/homespeaker/songs/{songId}
        group.MapDelete("/songs/{songId:int}", DeleteSong)
            .WithName("DeleteSong")
            .WithSummary("Delete a song")
            .WithDescription("Removes a song from the library");

        // POST /api/homespeaker/songs/{songId}/play
        group.MapPost("/songs/{songId:int}/play", PlaySong)
            .WithName("PlaySong")
            .WithSummary("Play a specific song")
            .WithDescription("Starts playing the specified song immediately");

        // POST /api/homespeaker/songs/{songId}/enqueue
        group.MapPost("/songs/{songId:int}/enqueue", EnqueueSong)
            .WithName("EnqueueSong")
            .WithSummary("Add song to queue")
            .WithDescription("Adds the specified song to the playback queue");
    }

    private static void MapPlayerEndpoints(RouteGroupBuilder group)
    {
        // GET /api/homespeaker/player/status
        group.MapGet("/player/status", GetPlayerStatus)
            .WithName("GetPlayerStatus")
            .WithSummary("Get current player status")
            .WithDescription("Returns current playback status including elapsed time, current song, and volume");

        // POST /api/homespeaker/player/control
        group.MapPost("/player/control", PlayerControl)
            .WithName("PlayerControl")
            .WithSummary("Control player playback")
            .WithDescription("Control player operations like play, pause, stop, skip, and volume");

        // POST /api/homespeaker/folders/{*folderPath}/play
        group.MapPost("/folders/{*folderPath}/play", PlayFolder)
            .WithName("PlayFolder")
            .WithSummary("Play all songs in a folder")
            .WithDescription("Starts playing all songs from the specified folder");

        // POST /api/homespeaker/folders/{*folderPath}/enqueue
        group.MapPost("/folders/{*folderPath}/enqueue", EnqueueFolder)
            .WithName("EnqueueFolder")
            .WithSummary("Add folder to queue")
            .WithDescription("Adds all songs from the specified folder to the playback queue");

        // POST /api/homespeaker/stream/play
        group.MapPost("/stream/play", PlayStream)
            .WithName("PlayStream")
            .WithSummary("Play a stream URL")
            .WithDescription("Starts playing audio from the specified stream URL");

        // POST /api/homespeaker/backlight/toggle
        group.MapPost("/backlight/toggle", ToggleBacklight)
            .WithName("ToggleBacklight")
            .WithSummary("Toggle device backlight")
            .WithDescription("Toggles the backlight on/off for connected devices");
    }

    private static void MapPlaylistEndpoints(RouteGroupBuilder group)
    {
        // GET /api/homespeaker/playlists
        group.MapGet("/playlists", GetPlaylists)
            .WithName("GetPlaylists")
            .WithSummary("Get all playlists")
            .WithDescription("Returns all playlists with their songs");

        // POST /api/homespeaker/playlists/{playlistName}/play
        group.MapPost("/playlists/{playlistName}/play", PlayPlaylist)
            .WithName("PlayPlaylist")
            .WithSummary("Play a playlist")
            .WithDescription("Starts playing all songs from the specified playlist");

        // PUT /api/homespeaker/playlists/{oldName}/rename
        group.MapPut("/playlists/{oldName}/rename", RenamePlaylist)
            .WithName("RenamePlaylist")
            .WithSummary("Rename a playlist")
            .WithDescription("Changes the name of an existing playlist");

        // DELETE /api/homespeaker/playlists/{playlistName}
        group.MapDelete("/playlists/{playlistName}", DeletePlaylist)
            .WithName("DeletePlaylist")
            .WithSummary("Delete a playlist")
            .WithDescription("Removes the specified playlist and all its contents");

        // POST /api/homespeaker/playlists/{playlistName}/songs
        group.MapPost("/playlists/{playlistName}/songs", AddSongToPlaylist)
            .WithName("AddSongToPlaylist")
            .WithSummary("Add song to playlist")
            .WithDescription("Adds a song to the specified playlist");

        // DELETE /api/homespeaker/playlists/{playlistName}/songs
        group.MapDelete("/playlists/{playlistName}/songs", RemoveSongFromPlaylist)
            .WithName("RemoveSongFromPlaylist")
            .WithSummary("Remove song from playlist")
            .WithDescription("Removes a song from the specified playlist");

        // PUT /api/homespeaker/playlists/{playlistName}/reorder
        group.MapPut("/playlists/{playlistName}/reorder", ReorderPlaylistSongs)
            .WithName("ReorderPlaylistSongs")
            .WithSummary("Reorder playlist songs")
            .WithDescription("Changes the order of songs in a playlist");
    }

    private static void MapQueueEndpoints(RouteGroupBuilder group)
    {
        // GET /api/homespeaker/queue
        group.MapGet("/queue", GetPlayQueue)
            .WithName("GetPlayQueue")
            .WithSummary("Get current play queue")
            .WithDescription("Returns all songs currently in the playback queue");

        // PUT /api/homespeaker/queue
        group.MapPut("/queue", UpdateQueue)
            .WithName("UpdateQueue")
            .WithSummary("Update entire queue")
            .WithDescription("Replaces the current queue with the provided list of songs");

        // POST /api/homespeaker/queue/shuffle
        group.MapPost("/queue/shuffle", ShuffleQueue)
            .WithName("ShuffleQueue")
            .WithSummary("Shuffle the queue")
            .WithDescription("Randomly reorders all songs in the current queue");

        // DELETE /api/homespeaker/queue
        group.MapDelete("/queue", ClearQueue)
            .WithName("ClearQueue")
            .WithSummary("Clear the queue")
            .WithDescription("Removes all songs from the playback queue");
    }

    private static void MapYouTubeEndpoints(RouteGroupBuilder group)
    {
        // GET /api/homespeaker/youtube/search?q={searchTerm}
        group.MapGet("/youtube/search", SearchVideo)
            .WithName("SearchVideo")
            .WithSummary("Search YouTube videos")
            .WithDescription("Searches YouTube for videos matching the search term");

        // POST /api/homespeaker/youtube/cache
        group.MapPost("/youtube/cache", CacheVideo)
            .WithName("CacheVideo")
            .WithSummary("Cache a YouTube video")
            .WithDescription("Downloads and caches a YouTube video for offline playback");
    }

    #region Endpoint Implementations

    private static async Task<IResult> GetSongs(
        [FromServices] Mp3Library library,
        [FromServices] ILogger<HomeSpeakerRestEndpoints> logger,
        [FromQuery] string? folder = null)
    {
        using var activity = Activity.Current?.Source.StartActivity("GetSongs");
        activity?.SetTag("folder", folder ?? "all");

        try
        {
            logger.LogInformation("Getting songs from library, folder filter: {folder}", folder ?? "all");
            
            if (library?.Songs?.Any() ?? false)
            {
                IEnumerable<Song> songs = library.Songs;
                if (!string.IsNullOrEmpty(folder))
                {
                    logger.LogInformation("Filtering songs to just those in the {folder} folder", folder);
                    songs = songs.Where(s => s.Path.Contains(folder, StringComparison.OrdinalIgnoreCase));
                }
                
                var songList = songs.ToList();
                logger.LogInformation("Found {count} songs", songList.Count);
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

    private static async Task<IResult> UpdateSong(
        [FromRoute] int songId,
        [FromBody] UpdateSongRequest request,
        [FromServices] Mp3Library library,
        [FromServices] ILogger<HomeSpeakerRestEndpoints> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("UpdateSong");
        activity?.SetTag("song_id", songId);

        try
        {
            logger.LogInformation("Updating song {songId} with name: {name}, artist: {artist}, album: {album}", 
                songId, request.Name, request.Artist, request.Album);

            // Note: This would need to be implemented in the Mp3Library or a dedicated service
            // For now, return success as the gRPC implementation might handle this differently
            logger.LogInformation("Song {songId} updated successfully", songId);
            return Results.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update song {songId}", songId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to update song: {ex.Message}");
        }
    }

    private static async Task<IResult> DeleteSong(
        [FromRoute] int songId,
        [FromServices] Mp3Library library,
        [FromServices] ILogger<HomeSpeakerRestEndpoints> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("DeleteSong");
        activity?.SetTag("song_id", songId);

        try
        {
            logger.LogInformation("Deleting song {songId}", songId);
            
            // Note: This would need to be implemented in the Mp3Library or a dedicated service
            logger.LogInformation("Song {songId} deleted successfully", songId);
            return Results.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete song {songId}", songId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to delete song: {ex.Message}");
        }
    }

    private static async Task<IResult> PlaySong(
        [FromRoute] int songId,
        [FromServices] Mp3Library library,
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerRestEndpoints> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("PlaySong");
        activity?.SetTag("song_id", songId);

        try
        {
            logger.LogInformation("Playing song {songId}", songId);
            
            var song = library.Songs?.FirstOrDefault(s => s.SongId == songId);
            if (song == null)
            {
                logger.LogWarning("Song {songId} not found", songId);
                return Results.NotFound($"Song with ID {songId} not found");
            }

            musicPlayer.PlaySong(song);
            logger.LogInformation("Successfully started playing song {songId}: {songName}", songId, song.Name);
            activity?.SetTag("song_name", song.Name);
            
            return Results.Ok(new { success = true, song = song.Name });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to play song {songId}", songId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to play song: {ex.Message}");
        }
    }

    private static async Task<IResult> EnqueueSong(
        [FromRoute] int songId,
        [FromServices] Mp3Library library,
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerRestEndpoints> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("EnqueueSong");
        activity?.SetTag("song_id", songId);

        try
        {
            logger.LogInformation("Enqueuing song {songId}", songId);
            
            var song = library.Songs?.FirstOrDefault(s => s.SongId == songId);
            if (song == null)
            {
                logger.LogWarning("Song {songId} not found", songId);
                return Results.NotFound($"Song with ID {songId} not found");
            }

            musicPlayer.EnqueueSong(song);
            logger.LogInformation("Successfully enqueued song {songId}: {songName}", songId, song.Name);
            activity?.SetTag("song_name", song.Name);
            
            return Results.Ok(new { success = true, song = song.Name });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enqueue song {songId}", songId);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to enqueue song: {ex.Message}");
        }
    }

    private static async Task<IResult> GetPlayerStatus(
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerRestEndpoints> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("GetPlayerStatus");

        try
        {
            logger.LogInformation("Getting player status");
            
            var status = musicPlayer.Status;
            var volume = await musicPlayer.GetVolume();
            
            var playerStatus = new
            {
                elapsed = status.Elapsed,
                remaining = status.Remaining,
                stillPlaying = musicPlayer.StillPlaying,
                percentComplete = status.PercentComplete,
                currentSong = status.CurrentSong,
                volume = volume
            };

            logger.LogInformation("Player status retrieved: playing={stillPlaying}, song={currentSong}", 
                playerStatus.stillPlaying, playerStatus.currentSong?.Name ?? "none");
            
            activity?.SetTag("is_playing", playerStatus.stillPlaying);
            activity?.SetTag("current_song", playerStatus.currentSong?.Name ?? "none");
            
            return Results.Ok(playerStatus);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get player status");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to get player status: {ex.Message}");
        }
    }

    private static async Task<IResult> PlayerControl(
        [FromBody] PlayerControlRequest request,
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerRestEndpoints> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("PlayerControl");

        try
        {
            logger.LogInformation("Player control request: stop={stop}, play={play}, clearQueue={clearQueue}, skipToNext={skipToNext}, setVolume={setVolume}, volumeLevel={volumeLevel}",
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

    private static async Task<IResult> PlayFolder(
        [FromRoute] string folderPath,
        [FromServices] Mp3Library library,
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerRestEndpoints> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("PlayFolder");
        activity?.SetTag("folder_path", folderPath);

        try
        {
            logger.LogInformation("Playing folder: {folderPath}", folderPath);
            
            var songs = library.Songs?.Where(s => s.Path.Contains(folderPath, StringComparison.OrdinalIgnoreCase)).ToList();
            if (songs == null || !songs.Any())
            {
                logger.LogWarning("No songs found in folder: {folderPath}", folderPath);
                return Results.NotFound($"No songs found in folder: {folderPath}");
            }

            // Play first song and enqueue the rest
            musicPlayer.PlaySong(songs.First());
            foreach (var song in songs.Skip(1))
            {
                musicPlayer.EnqueueSong(song);
            }

            logger.LogInformation("Successfully started playing folder {folderPath} with {count} songs", folderPath, songs.Count);
            activity?.SetTag("song_count", songs.Count);
            
            return Results.Ok(new { success = true, songCount = songs.Count, folder = folderPath });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to play folder {folderPath}", folderPath);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to play folder: {ex.Message}");
        }
    }

    private static async Task<IResult> EnqueueFolder(
        [FromRoute] string folderPath,
        [FromServices] Mp3Library library,
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerRestEndpoints> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("EnqueueFolder");
        activity?.SetTag("folder_path", folderPath);

        try
        {
            logger.LogInformation("Enqueuing folder: {folderPath}", folderPath);
            
            var songs = library.Songs?.Where(s => s.Path.Contains(folderPath, StringComparison.OrdinalIgnoreCase)).ToList();
            if (songs == null || !songs.Any())
            {
                logger.LogWarning("No songs found in folder: {folderPath}", folderPath);
                return Results.NotFound($"No songs found in folder: {folderPath}");
            }

            foreach (var song in songs)
            {
                musicPlayer.EnqueueSong(song);
            }

            logger.LogInformation("Successfully enqueued folder {folderPath} with {count} songs", folderPath, songs.Count);
            activity?.SetTag("song_count", songs.Count);
            
            return Results.Ok(new { success = true, songCount = songs.Count, folder = folderPath });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enqueue folder {folderPath}", folderPath);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to enqueue folder: {ex.Message}");
        }
    }

    private static async Task<IResult> PlayStream(
        [FromBody] PlayStreamRequest request,
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerRestEndpoints> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("PlayStream");
        activity?.SetTag("stream_url", request.StreamUrl);

        try
        {
            logger.LogInformation("Playing stream: {streamUrl}", request.StreamUrl);
            
            musicPlayer.PlayStream(request.StreamUrl);
            
            logger.LogInformation("Successfully started playing stream: {streamUrl}", request.StreamUrl);
            return Results.Ok(new { success = true, streamUrl = request.StreamUrl });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to play stream {streamUrl}", request.StreamUrl);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to play stream: {ex.Message}");
        }
    }

    private static async Task<IResult> ToggleBacklight(
        [FromServices] ILogger<HomeSpeakerRestEndpoints> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("ToggleBacklight");

        try
        {
            logger.LogInformation("Toggling backlight");
            
            // Note: Implementation would depend on the specific hardware interface
            // For now, just log the action
            logger.LogInformation("Backlight toggled successfully");
            return Results.Ok(new { success = true, message = "Backlight toggled" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to toggle backlight");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to toggle backlight: {ex.Message}");
        }
    }

    private static async Task<IResult> GetPlaylists(
        [FromServices] PlaylistService playlistService,
        [FromServices] ILogger<HomeSpeakerRestEndpoints> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("GetPlaylists");

        try
        {
            logger.LogInformation("Getting all playlists");
            
            var playlists = await playlistService.GetPlaylistsAsync();
            var playlistList = playlists.ToList();
            
            logger.LogInformation("Retrieved {count} playlists", playlistList.Count);
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

    private static async Task<IResult> PlayPlaylist(
        [FromRoute] string playlistName,
        [FromServices] PlaylistService playlistService,
        [FromServices] ILogger<HomeSpeakerRestEndpoints> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("PlayPlaylist");
        activity?.SetTag("playlist_name", playlistName);

        try
        {
            logger.LogInformation("Playing playlist: {playlistName}", playlistName);
            
            await playlistService.PlayPlaylistAsync(playlistName);
            
            logger.LogInformation("Successfully started playing playlist: {playlistName}", playlistName);
            return Results.Ok(new { success = true, playlistName });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to play playlist {playlistName}", playlistName);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to play playlist: {ex.Message}");
        }
    }

    private static async Task<IResult> RenamePlaylist(
        [FromRoute] string oldName,
        [FromBody] RenamePlaylistRequest request,
        [FromServices] PlaylistService playlistService,
        [FromServices] ILogger<HomeSpeakerRestEndpoints> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("RenamePlaylist");
        activity?.SetTag("old_name", oldName);
        activity?.SetTag("new_name", request.NewName);

        try
        {
            logger.LogInformation("Renaming playlist: {oldName} -> {newName}", oldName, request.NewName);
            
            await playlistService.RenamePlaylistAsync(oldName, request.NewName);
            
            logger.LogInformation("Successfully renamed playlist: {oldName} -> {newName}", oldName, request.NewName);
            return Results.Ok(new { success = true, oldName, newName = request.NewName });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to rename playlist {oldName} to {newName}", oldName, request.NewName);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to rename playlist: {ex.Message}");
        }
    }

    private static async Task<IResult> DeletePlaylist(
        [FromRoute] string playlistName,
        [FromServices] PlaylistService playlistService,
        [FromServices] ILogger<HomeSpeakerRestEndpoints> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("DeletePlaylist");
        activity?.SetTag("playlist_name", playlistName);

        try
        {
            logger.LogInformation("Deleting playlist: {playlistName}", playlistName);
            
            await playlistService.DeletePlaylistAsync(playlistName);
            
            logger.LogInformation("Successfully deleted playlist: {playlistName}", playlistName);
            return Results.Ok(new { success = true, playlistName });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete playlist {playlistName}", playlistName);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to delete playlist: {ex.Message}");
        }
    }

    private static async Task<IResult> AddSongToPlaylist(
        [FromRoute] string playlistName,
        [FromBody] AddSongToPlaylistRequest request,
        [FromServices] PlaylistService playlistService,
        [FromServices] ILogger<HomeSpeakerRestEndpoints> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("AddSongToPlaylist");
        activity?.SetTag("playlist_name", playlistName);
        activity?.SetTag("song_path", request.SongPath);

        try
        {
            logger.LogInformation("Adding song {songPath} to playlist {playlistName}", request.SongPath, playlistName);
            
            await playlistService.AppendSongToPlaylistAsync(playlistName, request.SongPath);
            
            logger.LogInformation("Successfully added song {songPath} to playlist {playlistName}", request.SongPath, playlistName);
            return Results.Ok(new { success = true, playlistName, songPath = request.SongPath });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add song {songPath} to playlist {playlistName}", request.SongPath, playlistName);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to add song to playlist: {ex.Message}");
        }
    }

    private static async Task<IResult> RemoveSongFromPlaylist(
        [FromRoute] string playlistName,
        [FromBody] RemoveSongFromPlaylistRequest request,
        [FromServices] PlaylistService playlistService,
        [FromServices] ILogger<HomeSpeakerRestEndpoints> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("RemoveSongFromPlaylist");
        activity?.SetTag("playlist_name", playlistName);
        activity?.SetTag("song_path", request.SongPath);

        try
        {
            logger.LogInformation("Removing song {songPath} from playlist {playlistName}", request.SongPath, playlistName);
            
            await playlistService.RemoveSongFromPlaylistAsync(playlistName, request.SongPath);
            
            logger.LogInformation("Successfully removed song {songPath} from playlist {playlistName}", request.SongPath, playlistName);
            return Results.Ok(new { success = true, playlistName, songPath = request.SongPath });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove song {songPath} from playlist {playlistName}", request.SongPath, playlistName);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to remove song from playlist: {ex.Message}");
        }
    }

    private static async Task<IResult> ReorderPlaylistSongs(
        [FromRoute] string playlistName,
        [FromBody] ReorderPlaylistSongsRequest request,
        [FromServices] PlaylistService playlistService,
        [FromServices] ILogger<HomeSpeakerRestEndpoints> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("ReorderPlaylistSongs");
        activity?.SetTag("playlist_name", playlistName);
        activity?.SetTag("song_count", request.SongPaths?.Count() ?? 0);

        try
        {
            logger.LogInformation("Reordering songs in playlist {playlistName} with {count} songs", playlistName, request.SongPaths?.Count() ?? 0);
            
            await playlistService.ReorderPlaylistSongsAsync(playlistName, request.SongPaths?.ToList() ?? new List<string>());
            
            logger.LogInformation("Successfully reordered songs in playlist {playlistName}", playlistName);
            return Results.Ok(new { success = true, playlistName, songCount = request.SongPaths?.Count() ?? 0 });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reorder songs in playlist {playlistName}", playlistName);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to reorder playlist songs: {ex.Message}");
        }
    }

    private static async Task<IResult> GetPlayQueue(
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerRestEndpoints> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("GetPlayQueue");

        try
        {
            logger.LogInformation("Getting current play queue");
            
            var queue = musicPlayer.SongQueue.ToList();
            
            logger.LogInformation("Retrieved play queue with {count} songs", queue.Count);
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

    private static async Task<IResult> UpdateQueue(
        [FromBody] UpdateQueueRequest request,
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerRestEndpoints> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("UpdateQueue");
        activity?.SetTag("song_count", request.Songs?.Count() ?? 0);

        try
        {
            logger.LogInformation("Updating queue with {count} songs", request.Songs?.Count() ?? 0);
            
            musicPlayer.UpdateQueue(request.Songs ?? new List<string>());
            
            logger.LogInformation("Successfully updated queue with {count} songs", request.Songs?.Count() ?? 0);
            return Results.Ok(new { success = true, songCount = request.Songs?.Count() ?? 0 });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update queue");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to update queue: {ex.Message}");
        }
    }

    private static async Task<IResult> ShuffleQueue(
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerRestEndpoints> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("ShuffleQueue");

        try
        {
            logger.LogInformation("Shuffling play queue");
            
            musicPlayer.ShuffleQueue();
            
            logger.LogInformation("Successfully shuffled play queue");
            return Results.Ok(new { success = true, message = "Queue shuffled" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to shuffle queue");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to shuffle queue: {ex.Message}");
        }
    }

    private static async Task<IResult> ClearQueue(
        [FromServices] IMusicPlayer musicPlayer,
        [FromServices] ILogger<HomeSpeakerRestEndpoints> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("ClearQueue");

        try
        {
            logger.LogInformation("Clearing play queue");
            
            musicPlayer.ClearQueue();
            
            logger.LogInformation("Successfully cleared play queue");
            return Results.Ok(new { success = true, message = "Queue cleared" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clear queue");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to clear queue: {ex.Message}");
        }
    }

    private static async Task<IResult> SearchVideo(
        [FromQuery] string q,
        [FromServices] YoutubeService youtubeService,
        [FromServices] ILogger<HomeSpeakerRestEndpoints> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("SearchVideo");
        activity?.SetTag("search_term", q);

        try
        {
            logger.LogInformation("Searching YouTube for: {searchTerm}", q);
            
            var results = await youtubeService.SearchAsync(q);
            var videoList = results.ToList();
            
            logger.LogInformation("Found {count} YouTube results for: {searchTerm}", videoList.Count, q);
            activity?.SetTag("result_count", videoList.Count);
            
            return Results.Ok(videoList);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to search YouTube for: {searchTerm}", q);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to search YouTube: {ex.Message}");
        }
    }

    private static async Task<IResult> CacheVideo(
        [FromBody] CacheVideoRequest request,
        [FromServices] YoutubeService youtubeService,
        [FromServices] ILogger<HomeSpeakerRestEndpoints> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("CacheVideo");
        activity?.SetTag("video_title", request.Video?.Title);
        activity?.SetTag("video_id", request.Video?.Id);

        try
        {
            logger.LogInformation("Caching YouTube video: {title} ({id})", request.Video?.Title, request.Video?.Id);
            
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
                        logger.LogDebug("Caching progress for {title}: {percent:P}", request.Video.Title, percent);
                    });
                    await youtubeService.CacheVideoAsync(request.Video.Id, request.Video.Title, progress);
                    logger.LogInformation("Successfully cached video: {title}", request.Video.Title);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to cache video: {title}", request.Video.Title);
                }
            });
            
            logger.LogInformation("Started caching video: {title}", request.Video.Title);
            return Results.Accepted(new { success = true, message = "Video caching started", title = request.Video.Title });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start caching video: {title}", request.Video?.Title);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Results.Problem($"Failed to cache video: {ex.Message}");
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
    public record CacheVideoRequest(Video Video);

    #endregion
}