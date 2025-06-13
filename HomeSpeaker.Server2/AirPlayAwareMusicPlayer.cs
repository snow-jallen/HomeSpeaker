using HomeSpeaker.Shared;

namespace HomeSpeaker.Server;

public class AirPlayAwareMusicPlayer : IMusicPlayer
{
    private readonly IMusicPlayer _actualPlayer;
    private readonly IAirPlayService _airPlayService;
    private readonly ILogger<AirPlayAwareMusicPlayer> _logger;

    public AirPlayAwareMusicPlayer(IMusicPlayer actualPlayer, IAirPlayService airPlayService, ILogger<AirPlayAwareMusicPlayer> logger)
    {
        _actualPlayer = actualPlayer;
        _airPlayService = airPlayService;
        _logger = logger;

        // Subscribe to AirPlay status changes
        _airPlayService.StatusChanged += OnAirPlayStatusChanged;
        
        // Forward player events
        _actualPlayer.PlayerEvent += (sender, message) => PlayerEvent?.Invoke(sender, message);
    }

    private void OnAirPlayStatusChanged(object? sender, AirPlayStatus airPlayStatus)
    {
        if (airPlayStatus.IsConnected && _actualPlayer.StillPlaying)
        {
            _logger.LogInformation("AirPlay device '{DeviceName}' connected. Stopping current music playback.", airPlayStatus.DeviceName);
            _actualPlayer.Stop();
            PlayerEvent?.Invoke(this, $"Stopped playback for AirPlay: {airPlayStatus.DeviceName}");
        }
        else if (!airPlayStatus.IsConnected)
        {
            _logger.LogInformation("AirPlay device disconnected.");
            PlayerEvent?.Invoke(this, "AirPlay device disconnected");
        }
    }

    public bool StillPlaying => _actualPlayer.StillPlaying;

    public PlayerStatus Status => _actualPlayer.Status with { AirPlayStatus = _airPlayService.CurrentStatus };

    public IEnumerable<Song> SongQueue => _actualPlayer.SongQueue;

    public event EventHandler<string>? PlayerEvent;

    public void ClearQueue() => _actualPlayer.ClearQueue();

    public void EnqueueSong(Song song) => _actualPlayer.EnqueueSong(song);

    public Task<int> GetVolume() => _actualPlayer.GetVolume();

    public void PlaySong(Song song)
    {
        // Don't play if AirPlay is connected
        if (_airPlayService.CurrentStatus.IsConnected)
        {
            _logger.LogInformation("AirPlay device is connected. Queuing song instead of playing: {SongName}", song.Name);
            EnqueueSong(song);
            PlayerEvent?.Invoke(this, $"Queued (AirPlay active): {song.Name}");
            return;
        }

        _actualPlayer.PlaySong(song);
    }

    public void PlayStream(string streamUrl)
    {
        // Don't play if AirPlay is connected
        if (_airPlayService.CurrentStatus.IsConnected)
        {
            _logger.LogInformation("AirPlay device is connected. Cannot play stream: {StreamUrl}", streamUrl);
            PlayerEvent?.Invoke(this, "Cannot play stream - AirPlay device active");
            return;
        }

        _actualPlayer.PlayStream(streamUrl);
    }

    public void ResumePlay()
    {
        // Don't resume if AirPlay is connected
        if (_airPlayService.CurrentStatus.IsConnected)
        {
            _logger.LogInformation("AirPlay device is connected. Cannot resume playback.");
            PlayerEvent?.Invoke(this, "Cannot resume - AirPlay device active");
            return;
        }

        _actualPlayer.ResumePlay();
    }

    public void SetVolume(int level0to100) => _actualPlayer.SetVolume(level0to100);

    public void ShuffleQueue() => _actualPlayer.ShuffleQueue();

    public void SkipToNext() => _actualPlayer.SkipToNext();

    public void Stop() => _actualPlayer.Stop();

    public void UpdateQueue(IEnumerable<string> songs) => _actualPlayer.UpdateQueue(songs);
}
