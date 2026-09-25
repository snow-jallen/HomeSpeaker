namespace HomeSpeaker.Server2.Services;

public sealed class AutoPlayMonitorService : BackgroundService
{
    private static readonly TimeSpan pollInterval = TimeSpan.FromSeconds(15);

    /// <summary>
    /// How long after a start attempt we keep looking for actual playback before
    /// concluding the start didn't take and re-arming.
    /// </summary>
    private static readonly TimeSpan startGracePeriod = TimeSpan.FromSeconds(45);

    private readonly IServiceProvider serviceProvider;
    private readonly IMusicPlayer musicPlayer;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<AutoPlayMonitorService> logger;

    private bool wasPlaying;
    private bool autoplayIsPlaying;
    private bool armed;
    private bool wasInQuietHours;
    private DateTimeOffset? silenceStartedAt;
    private DateTimeOffset? startAttemptedAt;

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
        wasInQuietHours = QuietHours.IsQuietTime(timeProvider);
        if (!wasPlaying)
        {
            silenceStartedAt = timeProvider.GetUtcNow();
            armed = !wasInQuietHours;
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

            await Task.Delay(pollInterval, timeProvider, stoppingToken);
        }
    }

    private async Task checkAutoplayAsync(CancellationToken cancellationToken)
    {
        var isPlaying = musicPlayer.StillPlaying;
        var now = timeProvider.GetUtcNow();
        var inQuietHours = QuietHours.IsQuietTime(timeProvider);
        var quietHoursJustEnded = wasInQuietHours && !inQuietHours;
        wasInQuietHours = inQuietHours;

        if (isPlaying)
        {
            // Playback is audible, so whatever we last kicked off did take.
            startAttemptedAt = null;

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

        if (wasPlaying)
        {
            // Playback just ended; that (re)arms the silence timer. If it ended
            // during quiet hours, stay disarmed for the night - the morning
            // wake-up transition below re-arms with a fresh clock.
            //
            // Deliberately leave autoplayIsPlaying alone here: a tick can land in
            // the sub-second gap between two queued songs, and clearing the flag
            // there would lose track of autoplay-owned playback for the rest of
            // the evening (so quiet hours wouldn't stop it). The next tick either
            // sees playback again or confirms real silence below.
            wasPlaying = false;
            silenceStartedAt = now;
            armed = !inQuietHours;
            return;
        }

        // Two consecutive silent ticks: playback really has stopped, not just an
        // inter-song gap.
        autoplayIsPlaying = false;

        if (startAttemptedAt is not null && now - startAttemptedAt.Value >= startGracePeriod)
        {
            // We asked for autoplay and nothing ever started making noise (dead
            // stream, unreadable files, library not scanned yet). Re-arm with a
            // fresh silence clock so we try again next timeout instead of
            // staying latched off until someone plays something by hand.
            logger.LogWarning("Autoplay was started but nothing is playing; re-arming the silence timer.");
            startAttemptedAt = null;
            silenceStartedAt = now;
            armed = !inQuietHours;
        }

        if (inQuietHours)
        {
            // Quiet hours veto any pending trigger, including one armed earlier
            // in the evening - silence accumulated overnight must not count
            // toward the timeout.
            armed = false;
            return;
        }

        if (quietHoursJustEnded)
        {
            // The screen just woke up for the day. Start the silence clock
            // fresh so autoplay begins the configured number of minutes after
            // wake-up rather than instantly (overnight silence would have
            // long exceeded the timeout).
            armed = true;
            silenceStartedAt = now;
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
            startAttemptedAt = timeProvider.GetUtcNow();
            return;
        }

        silenceStartedAt = now;
    }
}
