using System.Collections.Concurrent;

namespace HomeSpeaker.Server2.Services;

/// <summary>
/// Keeps the latest player status and tells subscribers (the SSE stream, Live Activity pushes)
/// when it changes materially. While anyone is subscribed it re-reads the status every second,
/// and shortly after every <see cref="IMusicPlayer.PlayerEvent"/>; with no subscribers it idles.
/// </summary>
public sealed class PlayerStatusBroadcaster : BackgroundService
{
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);

    // Player actions often raise several events back to back (stop, then play, then queue);
    // waiting a moment lets one refresh capture the settled state.
    private static readonly TimeSpan eventDebounce = TimeSpan.FromMilliseconds(250);

    private readonly IPlayerStatusProvider statusProvider;
    private readonly IMusicPlayer musicPlayer;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<PlayerStatusBroadcaster> logger;
    private readonly ConcurrentDictionary<PlayerStatusSubscription, byte> subscribers = new();
    private TaskCompletionSource wakeSignal = newWakeSignal();
    private PlayerStatusSnapshot? latest;
    private int consecutiveFailures;

    public PlayerStatusBroadcaster(
        IPlayerStatusProvider statusProvider,
        IMusicPlayer musicPlayer,
        TimeProvider timeProvider,
        ILogger<PlayerStatusBroadcaster> logger)
    {
        this.statusProvider = statusProvider;
        this.musicPlayer = musicPlayer;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    /// <summary>The most recently read status. Only kept current while someone is subscribed.</summary>
    public PlayerStatusSnapshot? Latest => Volatile.Read(ref latest);

    public int SubscriberCount => subscribers.Count;

    /// <summary>
    /// Starts receiving material changes. Subscribing also wakes the refresh loop, so
    /// <see cref="Latest"/> becomes current within a moment.
    /// </summary>
    public PlayerStatusSubscription Subscribe()
    {
        var subscription = new PlayerStatusSubscription(s => subscribers.TryRemove(s, out _), timeProvider);
        subscribers.TryAdd(subscription, 0);
        wake();
        return subscription;
    }

    /// <summary>
    /// Returns <see cref="Latest"/> when it was read within <paramref name="maxAge"/>, otherwise
    /// reads the status afresh.
    /// </summary>
    public async Task<PlayerStatusSnapshot> GetFreshAsync(TimeSpan maxAge, CancellationToken cancellationToken)
    {
        var current = Latest;
        if (current is not null && timeProvider.GetUtcNow() - current.CapturedAt <= maxAge)
        {
            return current;
        }

        var status = await statusProvider.GetAsync(cancellationToken);
        return new PlayerStatusSnapshot(status, timeProvider.GetUtcNow());
    }

    /// <summary>Reads the status once and notifies subscribers if it changed materially.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            var status = await statusProvider.GetAsync(cancellationToken);
            var snapshot = new PlayerStatusSnapshot(status, timeProvider.GetUtcNow());
            var previous = Interlocked.Exchange(ref latest, snapshot);
            consecutiveFailures = 0;

            if (!PlayerStatusChangeDetector.Compare(previous, snapshot).IsMaterial)
            {
                return;
            }

            foreach (var subscriber in subscribers.Keys)
            {
                subscriber.Publish(snapshot);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // This runs every second, so only the first failure of a streak is worth a warning.
            if (++consecutiveFailures == 1)
            {
                logger.LogWarning(ex, "Failed to refresh player status for subscribers");
            }
            else
            {
                logger.LogDebug(ex, "Player status refresh still failing ({Failures} in a row)", consecutiveFailures);
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        musicPlayer.PlayerEvent += onPlayerEvent;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (!subscribers.IsEmpty)
                {
                    await RefreshAsync(stoppingToken);
                }

                var signal = Volatile.Read(ref wakeSignal).Task;
                var completed = await Task.WhenAny(signal, Task.Delay(RefreshInterval, timeProvider, stoppingToken));
                if (completed == signal)
                {
                    Interlocked.Exchange(ref wakeSignal, newWakeSignal());
                    await Task.Delay(eventDebounce, timeProvider, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
        finally
        {
            musicPlayer.PlayerEvent -= onPlayerEvent;
        }
    }

    private void onPlayerEvent(object? sender, string message) => wake();

    private void wake() => Volatile.Read(ref wakeSignal).TrySetResult();

    private static TaskCompletionSource newWakeSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
