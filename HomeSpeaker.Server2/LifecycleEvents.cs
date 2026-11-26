using System.Text.Json;
using HomeSpeaker.Server2.Services.PlayingPlaylist;
using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2;

public class LifecycleEvents : IHostedService
{
    public LifecycleEvents(ILogger<LifecycleEvents> logger, IMusicPlayer player, IConfiguration config, TimeProvider timeProvider, PlayingPlaylistTracker playingPlaylist)
    {
        this.logger = logger;
        this.playingPlaylist = playingPlaylist;
        this.player = player;
        this.config = config;
        this.timeProvider = timeProvider;

        if (!Path.Exists(config[ConfigKeys.MediaFolder]))
        {
            throw new MissingConfigException(ConfigKeys.MediaFolder);
        }
    }

    //write to media folder because that exists outside of the container
    public string LastStatePath => Path.Combine(config[ConfigKeys.MediaFolder]!, "lastState.json");

    private readonly ILogger<LifecycleEvents> logger;
    private readonly IMusicPlayer player;
    private readonly IConfiguration config;
    private readonly TimeProvider timeProvider;
    private readonly PlayingPlaylistTracker playingPlaylist;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Application started event raised!");
        if (File.Exists(LastStatePath))
        {
            if (QuietHours.IsQuietTime(timeProvider))
            {
                // A restart overnight (e.g. a deploy) must not start music, and the
                // saved state would be stale by morning, so drop it.
                logger.LogInformation("Quiet hours; discarding {LastStatePath} instead of resuming playback", LastStatePath);
                File.Delete(LastStatePath);
                return;
            }

            logger.LogInformation("Found {LastStatePath} file, re-setting current song and queue", LastStatePath);

            var lastState = JsonSerializer.Deserialize<LastState>(await File.ReadAllTextAsync(LastStatePath, cancellationToken));
            if (lastState?.CurrentSong != null && lastState?.Queue != null)
            {
                player.PlaySong(lastState.CurrentSong);
                foreach (var s in lastState.Queue)
                {
                    player.EnqueueSong(s);
                }

                if (lastState.PlayingPlaylistName is { } playlistName && lastState.PlayingPlaylistSongs is { } playlistSongs)
                {
                    playingPlaylist.Started(playlistName, playlistSongs);
                }

                logger.LogInformation("Restarted using {LastState}", lastState);
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Application Stopping event raised!");
        try
        {
            if (player.Status.StillPlaying)
            {
                logger.LogInformation("Still playing music...saving current song and queue");
                var playlist = playingPlaylist.Current;
                var lastState = new LastState
                {
                    CurrentSong = player.Status.CurrentSong,
                    Queue = player.SongQueue,
                    PlayingPlaylistName = playlist?.PlaylistName,
                    PlayingPlaylistSongs = playlist?.SongPaths.ToList(),
                };
                var json = JsonSerializer.Serialize(lastState);
                await File.WriteAllTextAsync(LastStatePath, json, cancellationToken);
                logger.LogInformation("Saved {LastStatePath} with {LastState}", LastStatePath, lastState);
            }
            else //if we're not playing anything right now
            {
                logger.LogInformation("Not playing anything, no state to save.");
                if (File.Exists(LastStatePath)) //don't leave behind a file as if we were.
                {
                    File.Delete(LastStatePath);
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Shutdown was cancelled before state could be saved");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving application state during shutdown");
        }
        finally
        {
            // Ensure music player is properly disposed
            if (player is IDisposable disposablePlayer)
            {
                try
                {
                    disposablePlayer.Dispose();
                    logger.LogInformation("Music player disposed successfully");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error disposing music player");
                }
            }
        }
    }

    public class LastState
    {
        public Song? CurrentSong { get; set; }
        public IEnumerable<Song>? Queue { get; set; }

        /// <summary>The playlist being played when the app stopped (see <see cref="PlayingPlaylistTracker"/>), if any.</summary>
        public string? PlayingPlaylistName { get; set; }

        public IReadOnlyList<string>? PlayingPlaylistSongs { get; set; }
    }
}
