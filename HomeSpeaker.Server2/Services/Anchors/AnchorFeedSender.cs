using HomeSpeaker.Server2.Data;

namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>Sends queued ticks to the external feed: right away when one is queued, and every 10 seconds while any wait.</summary>
public sealed class AnchorFeedSender : BackgroundService
{
    private static readonly TimeSpan interval = TimeSpan.FromSeconds(10);

    private readonly IServiceScopeFactory scopeFactory;
    private readonly HttpAnchorFeed feed;
    private readonly AnchorFeedSignal signal;
    private readonly AnchorChangeNotifier notifier;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<AnchorFeedSender> logger;
    private readonly SemaphoreSlim wake = new(0);

    public AnchorFeedSender(IServiceScopeFactory scopeFactory, HttpAnchorFeed feed, AnchorFeedSignal signal, AnchorChangeNotifier notifier, TimeProvider timeProvider, ILogger<AnchorFeedSender> logger)
    {
        this.scopeFactory = scopeFactory;
        this.feed = feed;
        this.signal = signal;
        this.notifier = notifier;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    public override void Dispose()
    {
        wake.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        signal.Woken += onWoken;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (feed.IsConfigured)
                {
                    await sendAsync(stoppingToken);
                }

                await wake.WaitAsync(interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
        finally
        {
            signal.Woken -= onWoken;
        }
    }

    private void onWoken() => wake.Release();

    private async Task sendAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<MusicContext>();
            if (await AnchorFeedOutbox.SendDueAsync(db, feed, timeProvider, stoppingToken, logger) > 0)
            {
                notifier.NotifyChanged();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Sending queued anchor ticks failed");
        }
    }
}
