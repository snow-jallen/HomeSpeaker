namespace HomeSpeaker.Server2.Services.LiveActivities;

/// <summary>
/// Feeds player status changes to <see cref="LiveActivityPushDispatcher"/>. It runs its own loop
/// so slow APNs requests never hold up the broadcaster, and it only subscribes to the broadcaster
/// while there are Live Activities to update.
/// </summary>
public sealed class LiveActivityPushWorker : BackgroundService
{
    // Re-evaluated at least this often even without a change, so throttled updates and the
    // stopped-for-30-minutes end go out on time.
    private static readonly TimeSpan evaluationInterval = TimeSpan.FromSeconds(1);

    // A snapshot older than this is not trusted to describe the player.
    private static readonly TimeSpan maxSnapshotAge = TimeSpan.FromSeconds(5);

    private readonly PlayerStatusBroadcaster broadcaster;
    private readonly LiveActivityPushDispatcher dispatcher;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<LiveActivityPushWorker> logger;

    public LiveActivityPushWorker(
        PlayerStatusBroadcaster broadcaster,
        LiveActivityPushDispatcher dispatcher,
        TimeProvider timeProvider,
        ILogger<LiveActivityPushWorker> logger)
    {
        this.broadcaster = broadcaster;
        this.dispatcher = dispatcher;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        PlayerStatusSubscription? subscription = null;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (!dispatcher.HasWork())
                {
                    subscription?.Dispose();
                    subscription = null;
                    await Task.Delay(evaluationInterval, timeProvider, stoppingToken);
                    continue;
                }

                subscription ??= broadcaster.Subscribe();
                var update = await subscription.WaitForUpdateAsync(evaluationInterval, stoppingToken);
                var current = update ?? broadcaster.Latest;
                if (current is null || timeProvider.GetUtcNow() - current.CapturedAt > maxSnapshotAge)
                {
                    continue;
                }

                try
                {
                    await dispatcher.ProcessAsync(current, stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogWarning(ex, "Failed to push Live Activity updates");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
        finally
        {
            subscription?.Dispose();
        }
    }
}
