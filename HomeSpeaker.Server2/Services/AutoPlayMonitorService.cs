namespace HomeSpeaker.Server2.Services;

public sealed class AutoPlayMonitorService : BackgroundService
{
    private readonly IServiceProvider serviceProvider;
    private readonly IMusicPlayer musicPlayer;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<AutoPlayMonitorService> logger;

    private bool wasPlaying;
    private bool autoplayStartedForCurrentSilence;
    private DateTimeOffset? silenceStartedAt;

    public AutoPlayMonitorService(
        IServiceProvider serviceProvider,
        IMusicPlayer musicPlayer,
        TimeProvider timeProvider,
        ILogger<AutoPlayMonitorService> logger)
    {
        this.serviceProvider = serviceProvider;
        this.musicPlayer = musicPlayer;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        wasPlaying = musicPlayer.StillPlaying;
        if (!wasPlaying)
        {
            silenceStartedAt = timeProvider.GetUtcNow();
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await checkAutoplayAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Autoplay monitor loop failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(15), timeProvider, stoppingToken);
        }
    }

    private async Task checkAutoplayAsync(CancellationToken cancellationToken)
    {
        var isPlaying = musicPlayer.StillPlaying;
        var now = timeProvider.GetUtcNow();

        if (isPlaying)
        {
            wasPlaying = true;
            autoplayStartedForCurrentSilence = false;
            silenceStartedAt = null;
            return;
        }

        if (wasPlaying || silenceStartedAt is null)
        {
            wasPlaying = false;
            autoplayStartedForCurrentSilence = false;
            silenceStartedAt = now;
            return;
        }

        if (autoplayStartedForCurrentSilence)
        {
            return;
        }

        await using var scope = serviceProvider.CreateAsyncScope();
        var autoPlayService = scope.ServiceProvider.GetRequiredService<AutoPlayService>();
        var settings = await autoPlayService.GetSettingsAsync(cancellationToken);
        var timeout = TimeSpan.FromMinutes(Math.Clamp(settings.SilenceTimeoutMinutes, 1, 24 * 60));
        if (settings.Sources.Count == 0 || now - silenceStartedAt.Value < timeout)
        {
            return;
        }

        var started = await autoPlayService.TryStartConfiguredAutoplayAsync(cancellationToken);
        if (started)
        {
            logger.LogInformation("Autoplay started after {TimeoutMinutes} minutes of silence.", settings.SilenceTimeoutMinutes);
            autoplayStartedForCurrentSilence = true;
            return;
        }

        silenceStartedAt = now;
    }
}
