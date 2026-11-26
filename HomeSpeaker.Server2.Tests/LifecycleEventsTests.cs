using System.Text.Json;
using HomeSpeaker.Server2.Services.PlayingPlaylist;
using HomeSpeaker.Server2.Tests.Photos;
using HomeSpeaker.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeSpeaker.Server2.Tests;

public sealed class LifecycleEventsTests : IDisposable
{
    private static readonly TimeZoneInfo mountain = TimeZoneInfo.CreateCustomTimeZone("UTC-6", TimeSpan.FromHours(-6), "UTC-6", "UTC-6");

    private readonly TempDirectory mediaFolder = new();
    private readonly RecordingMusicPlayer player = new();
    private readonly PlayingPlaylistTracker tracker = new();

    public void Dispose() => mediaFolder.Dispose();

    [Fact]
    public async Task StartAsync_DuringQuietHours_DoesNotResumeSavedPlayback()
    {
        var lifecycle = createLifecycle(localTime: new TimeSpan(22, 45, 0));
        await writeLastStateAsync(lifecycle);

        await lifecycle.StartAsync(CancellationToken.None);

        Assert.Empty(player.Played);
        Assert.Empty(player.Enqueued);
    }

    [Fact]
    public async Task StartAsync_DuringQuietHours_DiscardsSavedStateSoItCannotResumeLater()
    {
        var lifecycle = createLifecycle(localTime: new TimeSpan(3, 0, 0));
        await writeLastStateAsync(lifecycle);

        await lifecycle.StartAsync(CancellationToken.None);

        Assert.False(File.Exists(lifecycle.LastStatePath));
    }

    [Fact]
    public async Task StartAsync_OutsideQuietHours_ResumesSavedPlayback()
    {
        var lifecycle = createLifecycle(localTime: new TimeSpan(15, 0, 0));
        await writeLastStateAsync(lifecycle);

        await lifecycle.StartAsync(CancellationToken.None);

        Assert.Equal("Current", Assert.Single(player.Played).Name);
        Assert.Equal("Next", Assert.Single(player.Enqueued).Name);
    }

    [Fact]
    public async Task Restart_RemembersWhichPlaylistWasPlaying()
    {
        var before = createLifecycle(localTime: new TimeSpan(15, 0, 0));
        player.PlaySong(new Song { SongId = 1, Name = "Current", Path = "/music/current.mp3" });
        player.EnqueueSong(new Song { SongId = 2, Name = "Next", Path = "/music/next.mp3" });
        tracker.Started("Sunday", ["/music/current.mp3", "/music/next.mp3"]);
        await before.StopAsync(CancellationToken.None);
        tracker.Forget();

        await createLifecycle(localTime: new TimeSpan(15, 0, 0)).StartAsync(CancellationToken.None);

        Assert.Equal("Sunday", tracker.PlaylistFor(new Song { Name = "Next", Path = "/music/next.mp3" }));
    }

    [Fact]
    public async Task Restart_FromAStateSavedWithoutAPlaylist_LeavesNoPlaylist()
    {
        var lifecycle = createLifecycle(localTime: new TimeSpan(15, 0, 0));
        await writeLastStateAsync(lifecycle);

        await lifecycle.StartAsync(CancellationToken.None);

        Assert.Null(tracker.PlaylistFor(new Song { Name = "Current", Path = "/music/current.mp3" }));
    }

    private LifecycleEvents createLifecycle(TimeSpan localTime)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [ConfigKeys.MediaFolder] = mediaFolder.Path })
            .Build();
        var localNow = new DateTimeOffset(new DateTime(2026, 10, 13) + localTime, mountain.BaseUtcOffset);
        return new LifecycleEvents(NullLogger<LifecycleEvents>.Instance, player, config, new FixedTimeProvider(localNow.ToUniversalTime(), mountain), tracker);
    }

    private static async Task writeLastStateAsync(LifecycleEvents lifecycle)
    {
        var lastState = new LifecycleEvents.LastState
        {
            CurrentSong = new Song { SongId = 1, Name = "Current", Path = "/music/current.mp3" },
            Queue = [new Song { SongId = 2, Name = "Next", Path = "/music/next.mp3" }]
        };
        await File.WriteAllTextAsync(lifecycle.LastStatePath, JsonSerializer.Serialize(lastState));
    }
}
