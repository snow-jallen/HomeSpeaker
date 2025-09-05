using HomeSpeaker.WebAssembly.Models;

namespace HomeSpeaker.WebAssembly.Services;

public interface IPlaybackModeService
{
    PlaybackMode CurrentMode { get; set; }
    Task PlaySongAsync(SongViewModel song);
    Task PauseAsync();
    Task ResumeAsync();
    Task StopAsync();
    Task SetVolumeAsync(int volume);
    Task<int> GetVolumeAsync();
    event EventHandler<object>? ModeChanged;
    event EventHandler<string>? StatusMessage;
}

public class PlaybackModeService : IPlaybackModeService
{
    private readonly HomeSpeakerService _homeSpeakerService;
    private readonly IBrowserAudioService _browserAudioService;
    private readonly ILogger<PlaybackModeService> _logger;
    private PlaybackMode _currentMode = PlaybackMode.Server;    public event EventHandler<object>? ModeChanged;
    public event EventHandler<string>? StatusMessage;

    public PlaybackMode CurrentMode
    {
    get => _currentMode;
        set
    {            if (_currentMode != value)
            {
        _currentMode = value;
                ModeChanged?.Invoke(this, (object)value);
        _logger.LogInformation("Playback mode changed to {Mode}", value);
                StatusMessage?.Invoke(this, $"Playback mode: {value}");
            }
        }
    }

    public PlaybackModeService(
        HomeSpeakerService homeSpeakerService,
        IBrowserAudioService browserAudioService,
        ILogger<PlaybackModeService> logger)
    {
    _homeSpeakerService = homeSpeakerService;
    _browserAudioService = browserAudioService;
    _logger = logger;

        // Subscribe to browser audio events
    _browserAudioService.StatusChanged += OnBrowserStatusChanged;
    _browserAudioService.ErrorOccurred += OnBrowserError;
    }

    public async Task PlaySongAsync(SongViewModel song)
    {
        try
        {
            Console.WriteLine($"PlaySongAsync called with song: {song.Name}, CurrentMode: {CurrentMode}");
            
            switch (CurrentMode)
            {
                case PlaybackMode.Server:
                    Console.WriteLine("Using server playback");
                    await _homeSpeakerService.PlaySongAsync(song.SongId);
                    StatusMessage?.Invoke(this, $"Playing on server: {song.Name}");
                    break;

                case PlaybackMode.Local:
                    Console.WriteLine("Using local playback");
                    await _browserAudioService.PlaySongAsync(song);
                    StatusMessage?.Invoke(this, $"Playing locally: {song.Name}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in PlaySongAsync: {ex}");
            _logger.LogError(ex, "Error playing song {SongName} in {Mode} mode", song.Name, CurrentMode);
            StatusMessage?.Invoke(this, $"Error playing {song.Name}: {ex.Message}");
        }
    }

    public async Task PauseAsync()
    {
        try
        {
            switch (CurrentMode)
            {
                case PlaybackMode.Server:
                    await _homeSpeakerService.HomeSpeakerClient.PlayerControlAsync(
                        new HomeSpeaker.Shared.PlayerControlRequest { Stop = true });
                    StatusMessage?.Invoke(this, "Paused on server");
                    break;

                case PlaybackMode.Local:
                    await _browserAudioService.PauseAsync();
                    StatusMessage?.Invoke(this, "Paused locally");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing in {Mode} mode", CurrentMode);
            StatusMessage?.Invoke(this, $"Error pausing: {ex.Message}");
        }
    }

    public async Task ResumeAsync()
    {
        try
        {
            switch (CurrentMode)
            {
                case PlaybackMode.Server:
                    await _homeSpeakerService.HomeSpeakerClient.PlayerControlAsync(
                        new HomeSpeaker.Shared.PlayerControlRequest { Play = true });
                    StatusMessage?.Invoke(this, "Resumed on server");
                    break;

                case PlaybackMode.Local:
                    await _browserAudioService.ResumeAsync();
                    StatusMessage?.Invoke(this, "Resumed locally");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming in {Mode} mode", CurrentMode);
            StatusMessage?.Invoke(this, $"Error resuming: {ex.Message}");
        }
    }

    public async Task StopAsync()
    {
        try
        {
            switch (CurrentMode)
            {
                case PlaybackMode.Server:
                    await _homeSpeakerService.HomeSpeakerClient.PlayerControlAsync(
                        new HomeSpeaker.Shared.PlayerControlRequest { Stop = true });
                    StatusMessage?.Invoke(this, "Stopped on server");
                    break;

                case PlaybackMode.Local:
                    await _browserAudioService.StopAsync();
                    StatusMessage?.Invoke(this, "Stopped locally");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping in {Mode} mode", CurrentMode);
            StatusMessage?.Invoke(this, $"Error stopping: {ex.Message}");
        }
    }

    public async Task SetVolumeAsync(int volume)
    {
        try
        {
            switch (CurrentMode)
            {
                case PlaybackMode.Server:
                    await _homeSpeakerService.HomeSpeakerClient.PlayerControlAsync(
                        new HomeSpeaker.Shared.PlayerControlRequest { SetVolume = true, VolumeLevel = volume });
                    StatusMessage?.Invoke(this, $"Server volume: {volume}%");
                    break;

                case PlaybackMode.Local:
                    await _browserAudioService.SetVolumeAsync(volume / 100.0f);
                    StatusMessage?.Invoke(this, $"Local volume: {volume}%");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting volume in {Mode} mode", CurrentMode);
            StatusMessage?.Invoke(this, $"Error setting volume: {ex.Message}");
        }
    }

    public async Task<int> GetVolumeAsync()
    {
        try
        {
            switch (CurrentMode)
            {
                case PlaybackMode.Server:
                    var status = await _homeSpeakerService.HomeSpeakerClient.GetPlayerStatusAsync(
                        new HomeSpeaker.Shared.GetStatusRequest());
                    return status.Volume;

                case PlaybackMode.Local:
                    var volume = await _browserAudioService.GetVolumeAsync();
                    return (int)(volume * 100);

                default:
                    return 50;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting volume in {Mode} mode", CurrentMode);
            return 50;
        }
    }

    private void OnBrowserStatusChanged(object? sender, BrowserPlayerStatus status)
    {
    if (CurrentMode == PlaybackMode.Local)
        {
            var statusText = status.IsPlaying ? "Playing" : status.IsPaused ? "Paused" : "Stopped";
            if (!string.IsNullOrEmpty(status.CurrentSong))
            {
                statusText += $": {status.CurrentSong}";
            }
            StatusMessage?.Invoke(this, statusText);
        }
    }

    private void OnBrowserError(object? sender, string error)
    {
    if (CurrentMode == PlaybackMode.Local)
        {
            StatusMessage?.Invoke(this, $"Local playback error: {error}");
        }
    }
}
