using HomeSpeaker.Server2.Data;

namespace HomeSpeaker.Server2.Services.PlayHistory;

/// <summary>
/// Writes an <see cref="Impression"/> for every song the speaker plays, marking the ones people
/// skip, so <see cref="SmartPlaylistRules"/> can tell what the family actually listens to. It
/// samples the player status every couple of seconds (reading it is just a field read) and
/// listens for <see cref="ChattyMusicPlayer.SkippedMessage"/>.
/// </summary>
public sealed class PlayHistoryRecorder : BackgroundService
{
    public const string PlayedBySpeaker = "Speaker";

    private static readonly TimeSpan sampleInterval = TimeSpan.FromSeconds(2);

    private readonly IMusicPlayer player;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<PlayHistoryRecorder> logger;
    private readonly PlayTracker tracker = new();
    private readonly Lock gate = new();
    private readonly System.Threading.Channels.Channel<CompletedPlay> finished =
        System.Threading.Channels.Channel.CreateUnbounded<CompletedPlay>();

    public PlayHistoryRecorder(IMusicPlayer player, IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger<PlayHistoryRecorder> logger)
    {
        this.player = player;
        this.scopeFactory = scopeFactory;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        player.PlayerEvent += onPlayerEvent;
        try
        {
            var writer = writeLoopAsync(stoppingToken);
            using var timer = new PeriodicTimer(sampleInterval, timeProvider);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                sample();
            }

            await writer;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            player.PlayerEvent -= onPlayerEvent;
        }
    }

    private void sample()
    {
        try
        {
            CompletedPlay? done;
            lock (gate)
            {
                done = tracker.Observe(player.Status, timeProvider.GetUtcNow());
            }

            enqueue(done);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Couldn't read the player status for play history");
        }
    }

    private void onPlayerEvent(object? sender, string message)
    {
        if (message != ChattyMusicPlayer.SkippedMessage)
        {
            return;
        }

        CompletedPlay? done;
        lock (gate)
        {
            done = tracker.Skipped();
        }

        enqueue(done);
    }

    private void enqueue(CompletedPlay? play)
    {
        if (play is not null)
        {
            finished.Writer.TryWrite(play);
        }
    }

    private async Task writeLoopAsync(CancellationToken stoppingToken)
    {
        await foreach (var play in finished.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<MusicContext>();
                db.Impressions.Add(new Impression
                {
                    SongPath = play.SongPath,
                    Timestamp = play.StartedAt.UtcDateTime,
                    PlayedBy = PlayedBySpeaker,
                    Skipped = play.Skipped,
                    SecondsHeard = (int)play.Heard.TotalSeconds,
                });
                await db.SaveChangesAsync(stoppingToken);
                logger.LogDebug("Recorded {Outcome} of {SongPath}", play.Skipped ? "skip" : "play", play.SongPath);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Couldn't record a play of {SongPath}", play.SongPath);
            }
        }
    }
}
