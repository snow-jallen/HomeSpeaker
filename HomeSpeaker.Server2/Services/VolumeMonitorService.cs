namespace HomeSpeaker.Server2.Services;

public class VolumeMonitorService : BackgroundService
{
    private readonly IMusicPlayer musicPlayer;
    private readonly PlayerStateService playerStateService;
    private readonly ILogger<VolumeMonitorService> logger;
    private int lastKnownVolume = -1;

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
                var currentVolume = await musicPlayer.GetVolume();
                if (currentVolume != lastKnownVolume)
                {
                    logger.LogInformation("Volume changed: {OldVolume} -> {NewVolume}", lastKnownVolume, currentVolume);
                    lastKnownVolume = currentVolume;
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
