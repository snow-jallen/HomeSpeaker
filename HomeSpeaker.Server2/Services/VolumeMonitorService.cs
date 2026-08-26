namespace HomeSpeaker.Server2.Services;

public class VolumeMonitorService : BackgroundService
{
    private readonly IMusicPlayer musicPlayer;
    private readonly PlayerStateService playerStateService;
    private readonly ILogger<VolumeMonitorService> logger;

    public VolumeMonitorService(IMusicPlayer musicPlayer, PlayerStateService playerStateService, ILogger<VolumeMonitorService> logger)
    {
        this.musicPlayer = musicPlayer;
        this.playerStateService = playerStateService;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Compare against the shared state rather than a private baseline so a
                // volume set through REST/the facade (which update PlayerStateService
                // directly) doesn't leave this monitor with a stale notion of "current"
                // that suppresses or duplicates the next broadcast.
                var currentVolume = await musicPlayer.GetVolume();
                if (currentVolume != playerStateService.Volume)
                {
                    logger.LogInformation("Volume changed: {OldVolume} -> {NewVolume}", playerStateService.Volume, currentVolume);
                    playerStateService.UpdateVolume(currentVolume);
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Error polling volume");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
