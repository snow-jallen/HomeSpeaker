using HomeSpeaker.Shared;
using System.Text.Json;

namespace HomeSpeaker.Server2;

public class LifecycleEvents : IHostedService
{
    public LifecycleEvents(ILogger<LifecycleEvents> logger, IMusicPlayer player, IConfiguration config)
    {
        _logger = logger;
        _player = player;
        _config = config;
    }

    //write to media folder because that exists outside of the container
    public string LastStatePath => Path.Combine(_config[ConfigKeys.MediaFolder] ?? throw new MissingConfigException(ConfigKeys.MediaFolder), "lastState.json");

    private readonly ILogger<LifecycleEvents> _logger;
    private readonly IMusicPlayer _player;
    private readonly IConfiguration _config;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Application started event raised!");
        if (File.Exists(LastStatePath))
        {
            _logger.LogInformation("Found {LastStatePath} file, re-setting current song and queue", LastStatePath);

            var lastState = JsonSerializer.Deserialize<LastState>(await File.ReadAllTextAsync(LastStatePath));
            if (lastState?.CurrentSong != null && lastState?.Queue != null)
            {
                _player.PlaySong(lastState.CurrentSong);
                foreach (var s in lastState.Queue)
                {
                    _player.EnqueueSong(s);
                }

                _logger.LogInformation("Restarted using {lastState}", lastState);
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Application Stopping event raised!");
        try
        {
            if (_player.Status.StillPlaying)
            {
                _logger.LogInformation("Still playing music...saving current song and queue");
                var lastState = new LastState
                {
                    CurrentSong = _player.Status.CurrentSong,
                    Queue = _player.SongQueue
                };
                var json = JsonSerializer.Serialize(lastState);
                await File.WriteAllTextAsync(LastStatePath, json, cancellationToken);
                _logger.LogInformation("Saved {LastStatePath} with {LastState}", LastStatePath, lastState);
            }
            else //if we're not playing anything right now
            {
                _logger.LogInformation("Not playing anything, no state to save.");
                if (File.Exists(LastStatePath)) //don't leave behind a file as if we were.
                    File.Delete(LastStatePath);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Shutdown was cancelled before state could be saved");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving application state during shutdown");
        }
        finally
        {
            // Ensure music player is properly disposed
            if (_player is IDisposable disposablePlayer)
            {
                try
                {
                    disposablePlayer.Dispose();
                    _logger.LogInformation("Music player disposed successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing music player");
                }
            }
        }
    }

    public class LastState
    {
        public Song? CurrentSong { get; set; }
        public IEnumerable<Song>? Queue { get; set; }
    }
}
