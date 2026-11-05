namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>
/// When this device's anchors live on another HomeSpeaker, checks it every 30 seconds while a page is open, so a tick made
/// on another speaker or a phone shows up here without a reload.
/// </summary>
public sealed class RemoteAnchorPoller : BackgroundService
{
    private static readonly TimeSpan interval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory scopeFactory;
    private readonly IConfiguration configuration;
    private readonly AnchorChangeNotifier notifier;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<RemoteAnchorPoller> logger;
    private readonly AnchorBoardWatcher watcher = new();

    public RemoteAnchorPoller(IServiceScopeFactory scopeFactory, IConfiguration configuration, AnchorChangeNotifier notifier, TimeProvider timeProvider, ILogger<RemoteAnchorPoller> logger)
    {
        this.scopeFactory = scopeFactory;
        this.configuration = configuration;
        this.notifier = notifier;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(interval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (!notifier.HasListeners || !AnchorSourceSettings.IsRemote(configuration))
            {
                continue;
            }

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var board = await scope.ServiceProvider.GetRequiredService<RemoteAnchorStore>().GetBoardAsync(cancellationToken: stoppingToken);
                if (watcher.Changed(board))
                {
                    notifier.NotifyChanged();
                }
            }
            catch (AnchorSourceUnavailableException ex)
            {
                logger.LogDebug(ex, "Anchor home unreachable while polling");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A background loop that throws stops the whole app; a bad poll just waits for the next one.
                logger.LogWarning(ex, "Polling the anchor home failed");
            }
        }
    }
}
