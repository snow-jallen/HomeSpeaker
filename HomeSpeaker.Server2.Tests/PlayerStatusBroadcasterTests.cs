using HomeSpeaker.Server2.Services;
using HomeSpeaker.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeSpeaker.Server2.Tests;

public sealed class PlayerStatusBroadcasterTests
{
    private static readonly Song song = new() { SongId = 1, Name = "Clair de Lune", Path = "/m/a.mp3" };

    private readonly ManualTimeProvider time = new(DateTimeOffset.Parse("2026-10-14T12:00:00Z"));
    private readonly StubStatusProvider provider = new();

    [Fact]
    public async Task PublishesOnlyMaterialChangesToSubscribers()
    {
        var broadcaster = createBroadcaster();
        using var subscription = broadcaster.Subscribe();

        provider.Status = new PlayerStatusDto { StillPlaying = true, CurrentSong = song, Volume = 40 };
        await broadcaster.RefreshAsync(CancellationToken.None);
        Assert.NotNull(await nextUpdate(subscription));

        // Elapsed ticking along updates Latest but isn't published.
        provider.Status = provider.Status with { Elapsed = TimeSpan.FromSeconds(1) };
        await broadcaster.RefreshAsync(CancellationToken.None);
        Assert.Null(await noUpdate(subscription));
        Assert.Equal(TimeSpan.FromSeconds(1), broadcaster.Latest!.Status.Elapsed);

        provider.Status = provider.Status with { Volume = 60 };
        await broadcaster.RefreshAsync(CancellationToken.None);
        var update = await nextUpdate(subscription);
        Assert.Equal(60, update!.Status.Volume);
    }

    [Fact]
    public async Task SlowSubscriberOnlyKeepsTheNewestSnapshot()
    {
        var broadcaster = createBroadcaster();
        using var subscription = broadcaster.Subscribe();

        foreach (var volume in new[] { 10, 20, 30 })
        {
            provider.Status = new PlayerStatusDto { Volume = volume };
            await broadcaster.RefreshAsync(CancellationToken.None);
        }

        var update = await nextUpdate(subscription);
        Assert.Equal(30, update!.Status.Volume);
        Assert.Null(await noUpdate(subscription));
    }

    [Fact]
    public async Task DisposingUnsubscribes()
    {
        var broadcaster = createBroadcaster();
        var subscription = broadcaster.Subscribe();
        Assert.Equal(1, broadcaster.SubscriberCount);

        subscription.Dispose();

        Assert.Equal(0, broadcaster.SubscriberCount);
        provider.Status = new PlayerStatusDto { Volume = 10 };
        await broadcaster.RefreshAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RefreshFailureKeepsTheLastGoodStatus()
    {
        var broadcaster = createBroadcaster();
        provider.Status = new PlayerStatusDto { Volume = 10 };
        await broadcaster.RefreshAsync(CancellationToken.None);

        provider.Failure = new InvalidOperationException("amixer exploded");
        await broadcaster.RefreshAsync(CancellationToken.None);

        Assert.Equal(10, broadcaster.Latest!.Status.Volume);
    }

    [Fact]
    public async Task GetFreshAsyncReadsAgainWhenLatestIsTooOld()
    {
        var broadcaster = createBroadcaster();
        provider.Status = new PlayerStatusDto { Volume = 10 };
        await broadcaster.RefreshAsync(CancellationToken.None);

        provider.Status = new PlayerStatusDto { Volume = 20 };
        Assert.Equal(10, (await broadcaster.GetFreshAsync(TimeSpan.FromSeconds(5), CancellationToken.None)).Status.Volume);

        time.Advance(TimeSpan.FromSeconds(6));
        Assert.Equal(20, (await broadcaster.GetFreshAsync(TimeSpan.FromSeconds(5), CancellationToken.None)).Status.Volume);
    }

    // The channel completes a waiting read asynchronously, so give it a (real-time) moment.
    private static Task<PlayerStatusSnapshot?> nextUpdate(PlayerStatusSubscription subscription)
        => subscription.WaitForUpdateAsync(TimeSpan.FromSeconds(5), CancellationToken.None);

    private static Task<PlayerStatusSnapshot?> noUpdate(PlayerStatusSubscription subscription)
        => subscription.WaitForUpdateAsync(TimeSpan.FromMilliseconds(100), CancellationToken.None);

    private PlayerStatusBroadcaster createBroadcaster()
        => new(provider, new RecordingMusicPlayer(), time, NullLogger<PlayerStatusBroadcaster>.Instance);

    private sealed class StubStatusProvider : IPlayerStatusProvider
    {
        public PlayerStatusDto Status { get; set; } = new();

        public Exception? Failure { get; set; }

        public Task<PlayerStatusDto> GetAsync(CancellationToken cancellationToken)
            => Failure is null ? Task.FromResult(Status) : Task.FromException<PlayerStatusDto>(Failure);
    }
}
