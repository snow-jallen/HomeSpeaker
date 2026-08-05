namespace HomeSpeaker.Server2.Services;

public sealed class AutoPlayMonitorService : BackgroundService
{
    private readonly IServiceProvider serviceProvider;
    private readonly IMusicPlayer musicPlayer;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<AutoPlayMonitorService> logger;

    private bool wasPlaying;
    private bool autoplayIsPlaying;
    private bool armed;
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
            armed = !QuietHours.IsQuietTime(timeProvider);
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
        var inQuietHours = QuietHours.IsQuietTime(timeProvider);

        if (isPlaying)
        {
            if (inQuietHours && autoplayIsPlaying)
            {
                logger.LogInformation("Quiet hours began; stopping autoplay playback for the night.");
                musicPlayer.Stop();
                musicPlayer.ClearQueue();
                autoplayIsPlaying = false;
                wasPlaying = false;
                armed = false;
                silenceStartedAt = now;
                return;
            }

            wasPlaying = true;
            return;
        }

        autoplayIsPlaying = false;

        if (wasPlaying)
        {
            // Playback just ended; that (re)arms the silence timer. Silence that
            // begins during quiet hours must not trigger autoplay - not now, and
            // not when the quiet window ends in the morning.
            wasPlaying = false;
            silenceStartedAt = now;
            armed = !inQuietHours;
            return;
        }

        if (inQuietHours)
        {
            // Quiet hours veto any pending trigger, including one armed earlier
            // in the evening - otherwise autoplay would fire the moment the
            // window ends, without anyone having played anything.
            armed = false;
            return;
        }

        if (!armed || silenceStartedAt is null)
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
            autoplayIsPlaying = true;
            armed = false;
            return;
        }

        silenceStartedAt = now;
    }
}
