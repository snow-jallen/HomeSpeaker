using System.Text.Json;
using HomeSpeaker.Server2.Services;
using HomeSpeaker.Server2.Services.LiveActivities;
using HomeSpeaker.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HomeSpeaker.Server2.Tests;

public sealed class LiveActivityPushDispatcherTests
{
    private const string Token = "0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly Song song = new() { SongId = 1, Name = "Clair de Lune", Artist = "Debussy", Path = "/m/a.mp3" };

    private readonly ManualTimeProvider time = new(DateTimeOffset.Parse("2026-10-14T12:00:00Z"));
    private readonly RecordingLiveActivitySender sender = new();
    private readonly LiveActivityRegistry registry;
    private readonly LiveActivityPushDispatcher dispatcher;

    public LiveActivityPushDispatcherTests()
    {
        registry = new LiveActivityRegistry(time);
        dispatcher = createDispatcher(configured: true);
    }

    [Fact]
    public async Task FirstStatusIsPushedImmediatelyAtHighPriority()
    {
        registry.Register("activity-1", Token);

        await dispatcher.ProcessAsync(snapshot(playing()), CancellationToken.None);

        var push = Assert.Single(sender.Pushes);
        Assert.Equal(Token, push.Token);
        Assert.Equal(10, push.Priority);
        Assert.Equal("update", push.Event);
    }

    [Fact]
    public async Task UnchangedStatusIsNotPushedAgain()
    {
        registry.Register("activity-1", Token);
        await dispatcher.ProcessAsync(snapshot(playing()), CancellationToken.None);

        time.Advance(TimeSpan.FromSeconds(1));
        await dispatcher.ProcessAsync(snapshot(playing() with { Elapsed = TimeSpan.FromSeconds(11) }), CancellationToken.None);

        Assert.Single(sender.Pushes);
    }

    [Fact]
    public async Task LowPriorityChangesAreThrottledAndCoalescedToTheLatest()
    {
        registry.Register("activity-1", Token);
        await dispatcher.ProcessAsync(snapshot(playing()), CancellationToken.None);

        // First low-priority change goes straight out.
        time.Advance(TimeSpan.FromSeconds(1));
        await dispatcher.ProcessAsync(snapshot(playing() with { Volume = 45 }), CancellationToken.None);
        Assert.Equal([10, 5], sender.Pushes.Select(p => p.Priority));

        // Further ones within 10 s are held back...
        time.Advance(TimeSpan.FromSeconds(2));
        await dispatcher.ProcessAsync(snapshot(playing() with { Volume = 50 }), CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(2));
        await dispatcher.ProcessAsync(snapshot(playing() with { Volume = 55 }), CancellationToken.None);
        Assert.Equal(2, sender.Pushes.Count);

        // ...and only the newest is sent once the window reopens.
        time.Advance(TimeSpan.FromSeconds(6));
        await dispatcher.ProcessAsync(snapshot(playing() with { Volume = 55 }), CancellationToken.None);
        Assert.Equal(3, sender.Pushes.Count);
        Assert.Equal(5, sender.Pushes[2].Priority);
        Assert.Equal(55, sender.Pushes[2].ContentState.GetProperty("volume").GetInt32());
    }

    [Fact]
    public async Task HighPriorityChangesBypassTheThrottle()
    {
        registry.Register("activity-1", Token);
        await dispatcher.ProcessAsync(snapshot(playing()), CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(1));
        await dispatcher.ProcessAsync(snapshot(playing() with { Volume = 45 }), CancellationToken.None);

        time.Advance(TimeSpan.FromSeconds(1));
        await dispatcher.ProcessAsync(snapshot(playing() with { Volume = 45, StillPlaying = false }), CancellationToken.None);

        Assert.Equal([10, 5, 10], sender.Pushes.Select(p => p.Priority));
        Assert.False(sender.Pushes[2].ContentState.GetProperty("isPlaying").GetBoolean());
    }

    [Fact]
    public async Task EndsAndDropsTheActivityAfterThirtyMinutesStopped()
    {
        registry.Register("activity-1", Token);
        var stopped = playing() with { StillPlaying = false };
        await dispatcher.ProcessAsync(snapshot(stopped), CancellationToken.None);

        time.Advance(TimeSpan.FromMinutes(29));
        await dispatcher.ProcessAsync(snapshot(stopped), CancellationToken.None);
        Assert.Single(sender.Pushes);
        Assert.Single(registry.GetActive());

        time.Advance(TimeSpan.FromMinutes(1));
        await dispatcher.ProcessAsync(snapshot(stopped), CancellationToken.None);

        Assert.Equal(2, sender.Pushes.Count);
        var end = sender.Pushes[1];
        Assert.Equal("end", end.Event);
        Assert.Equal(time.GetUtcNow().ToUnixTimeSeconds(), end.Aps.GetProperty("dismissal-date").GetInt64());
        Assert.Empty(registry.GetActive());
    }

    [Fact]
    public async Task ResumingPlaybackResetsTheStoppedClock()
    {
        registry.Register("activity-1", Token);
        var stopped = playing() with { StillPlaying = false };
        await dispatcher.ProcessAsync(snapshot(stopped), CancellationToken.None);

        time.Advance(TimeSpan.FromMinutes(20));
        await dispatcher.ProcessAsync(snapshot(playing()), CancellationToken.None);
        time.Advance(TimeSpan.FromMinutes(1));
        await dispatcher.ProcessAsync(snapshot(stopped), CancellationToken.None);

        time.Advance(TimeSpan.FromMinutes(20));
        await dispatcher.ProcessAsync(snapshot(stopped), CancellationToken.None);

        Assert.DoesNotContain(sender.Pushes, p => p.Event == "end");
        Assert.Single(registry.GetActive());
    }

    [Fact]
    public async Task InvalidTokenDropsTheRegistration()
    {
        registry.Register("activity-1", Token);
        sender.Result = new PushNotificationSendResult(false, ShouldDeactivateDevice: true, ErrorCode: "BadDeviceToken");

        await dispatcher.ProcessAsync(snapshot(playing()), CancellationToken.None);

        Assert.Empty(registry.GetActive());
    }

    [Fact]
    public async Task TransientFailureRetriesAfterADelay()
    {
        registry.Register("activity-1", Token);
        sender.Result = new PushNotificationSendResult(false, ErrorCode: "ServiceUnavailable");
        await dispatcher.ProcessAsync(snapshot(playing()), CancellationToken.None);

        sender.Result = new PushNotificationSendResult(true);
        time.Advance(TimeSpan.FromSeconds(5));
        await dispatcher.ProcessAsync(snapshot(playing()), CancellationToken.None);
        Assert.Single(sender.Pushes);

        time.Advance(LiveActivityPushDispatcher.TransientRetryDelay);
        await dispatcher.ProcessAsync(snapshot(playing()), CancellationToken.None);
        Assert.Equal(2, sender.Pushes.Count);
        Assert.Single(registry.GetActive());
    }

    [Fact]
    public async Task DoesNothingWhenApnsIsNotConfigured()
    {
        var unconfigured = createDispatcher(configured: false);
        registry.Register("activity-1", Token);

        Assert.False(unconfigured.HasWork());
        await unconfigured.ProcessAsync(snapshot(playing()), CancellationToken.None);

        Assert.Empty(sender.Pushes);
        Assert.Single(registry.GetActive());
    }

    [Fact]
    public async Task EachActivityIsThrottledIndependently()
    {
        registry.Register("activity-1", Token);
        await dispatcher.ProcessAsync(snapshot(playing()), CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(1));
        await dispatcher.ProcessAsync(snapshot(playing() with { Volume = 45 }), CancellationToken.None);

        registry.Register("activity-2", "fedcba9876543210fedcba9876543210");
        time.Advance(TimeSpan.FromSeconds(1));
        await dispatcher.ProcessAsync(snapshot(playing() with { Volume = 50 }), CancellationToken.None);

        // activity-1 is inside its throttle window; activity-2 gets its first (high priority) push.
        var last = Assert.Single(sender.Pushes.Skip(2));
        Assert.Equal("fedcba9876543210fedcba9876543210", last.Token);
        Assert.Equal(10, last.Priority);
    }

    private LiveActivityPushDispatcher createDispatcher(bool configured)
    {
        var options = new PushNotificationOptions { Enabled = configured };
        if (configured)
        {
            options.Apns = new PushNotificationOptions.ApnsOptions
            {
                TeamId = "ABCDE12345",
                KeyId = "KEY1234567",
                Topic = "com.homespeaker.mobile",
                PrivateKeyBase64 = "unused"
            };
        }

        return new LiveActivityPushDispatcher(registry, sender, Options.Create(options), time, NullLogger<LiveActivityPushDispatcher>.Instance);
    }

    private static PlayerStatusDto playing() => new()
    {
        StillPlaying = true,
        CurrentSong = song,
        Volume = 40
    };

    private PlayerStatusSnapshot snapshot(PlayerStatusDto status) => new(status, time.GetUtcNow());

    private sealed class RecordingLiveActivitySender : ILiveActivityPushSender
    {
        public List<RecordedPush> Pushes { get; } = [];

        public PushNotificationSendResult Result { get; set; } = new(true);

        public Task<PushNotificationSendResult> SendLiveActivityAsync(string pushToken, string payloadJson, int priority, CancellationToken cancellationToken = default)
        {
            Pushes.Add(new RecordedPush(pushToken, priority, JsonDocument.Parse(payloadJson).RootElement.GetProperty("aps").Clone()));
            return Task.FromResult(Result);
        }
    }

    private sealed record RecordedPush(string Token, int Priority, JsonElement Aps)
    {
        public string? Event => Aps.GetProperty("event").GetString();

        public JsonElement ContentState => Aps.GetProperty("content-state");
    }
}
