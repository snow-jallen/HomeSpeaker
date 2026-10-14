using System.Text.Json;
using HomeSpeaker.Server2.Services;
using HomeSpeaker.Server2.Services.LiveActivities;
using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Tests;

public sealed class LiveActivityPayloadTests
{
    private static readonly DateTimeOffset capturedAt = DateTimeOffset.Parse("2026-10-14T12:00:00.250Z");
    private static readonly DateTimeOffset now = DateTimeOffset.Parse("2026-10-14T12:00:01Z");

    private static readonly string[] contentStateKeys =
    [
        "title", "artist", "isPlaying", "isStream", "volume", "repeatMode", "sleepTimerEndsAtUnix", "updatedAtUnix"
    ];

    [Fact]
    public void UpdatePayloadHasExactApsShape()
    {
        var status = new PlayerStatusDto
        {
            StillPlaying = true,
            CurrentSong = new Song { SongId = 1, Name = "Clair de Lune", Artist = "Debussy", Path = "/m/a.mp3" },
            Volume = 42,
            RepeatMode = true,
            SleepTimerActive = true,
            SleepTimerRemainingMinutes = 10
        };

        var json = LiveActivityPayload.BuildUpdate(LiveActivityPayload.CreateContentState(new PlayerStatusSnapshot(status, capturedAt)), now);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(["aps"], propertyNames(root));

        var aps = root.GetProperty("aps");
        Assert.Equal(["timestamp", "event", "stale-date", "content-state"], propertyNames(aps));
        Assert.Equal(now.ToUnixTimeSeconds(), aps.GetProperty("timestamp").GetInt64());
        Assert.Equal("update", aps.GetProperty("event").GetString());
        Assert.Equal(now.AddMinutes(30).ToUnixTimeSeconds(), aps.GetProperty("stale-date").GetInt64());

        // Integer timestamps, not fractional: APNs requires whole seconds there.
        Assert.DoesNotContain('.', aps.GetProperty("timestamp").GetRawText());
        Assert.DoesNotContain('.', aps.GetProperty("stale-date").GetRawText());

        var state = aps.GetProperty("content-state");
        Assert.Equal(contentStateKeys, propertyNames(state));
        Assert.Equal("Clair de Lune", state.GetProperty("title").GetString());
        Assert.Equal("Debussy", state.GetProperty("artist").GetString());
        Assert.True(state.GetProperty("isPlaying").GetBoolean());
        Assert.False(state.GetProperty("isStream").GetBoolean());
        Assert.Equal(42, state.GetProperty("volume").GetInt32());
        Assert.True(state.GetProperty("repeatMode").GetBoolean());
        Assert.Equal(capturedAt.AddMinutes(10).ToUnixTimeMilliseconds() / 1000.0, state.GetProperty("sleepTimerEndsAtUnix").GetDouble(), 3);
        Assert.Equal(capturedAt.ToUnixTimeMilliseconds() / 1000.0, state.GetProperty("updatedAtUnix").GetDouble(), 3);
    }

    [Fact]
    public void SleepTimerEndIsAnExplicitNullWhenNoTimer()
    {
        var json = LiveActivityPayload.BuildUpdate(LiveActivityPayload.CreateContentState(new PlayerStatusSnapshot(new PlayerStatusDto(), capturedAt)), now);

        using var document = JsonDocument.Parse(json);
        var state = document.RootElement.GetProperty("aps").GetProperty("content-state");
        Assert.Equal(contentStateKeys, propertyNames(state));
        Assert.Equal(JsonValueKind.Null, state.GetProperty("sleepTimerEndsAtUnix").ValueKind);
        Assert.Equal(string.Empty, state.GetProperty("artist").GetString());
    }

    [Theory]
    [InlineData("Take Five", "KUSC", "KUSC")]
    [InlineData(null, "KUSC", "")]
    [InlineData("KUSC", "KUSC", "")]
    public void RadioShowsTheStationUnderTheNowPlayingTitle(string? nowPlaying, string streamName, string expectedArtist)
    {
        var status = new PlayerStatusDto
        {
            CurrentSong = nowPlaying is null ? null : new Song { Name = nowPlaying, Path = "http://radio.example/stream" },
            StreamName = streamName,
            IsStream = true,
            StillPlaying = true
        };

        var state = LiveActivityPayload.CreateContentState(new PlayerStatusSnapshot(status, capturedAt));

        Assert.Equal(expectedArtist, state.Artist);
    }

    [Fact]
    public void EndPayloadDismissesImmediately()
    {
        var json = LiveActivityPayload.BuildEnd(LiveActivityPayload.CreateContentState(new PlayerStatusSnapshot(new PlayerStatusDto(), capturedAt)), now);

        using var document = JsonDocument.Parse(json);
        var aps = document.RootElement.GetProperty("aps");
        Assert.Equal(["timestamp", "event", "dismissal-date", "content-state"], propertyNames(aps));
        Assert.Equal("end", aps.GetProperty("event").GetString());
        Assert.Equal(now.ToUnixTimeSeconds(), aps.GetProperty("dismissal-date").GetInt64());
        Assert.Equal(contentStateKeys, propertyNames(aps.GetProperty("content-state")));
    }

    [Theory]
    [InlineData("Song", null, true, "Song")]
    [InlineData(null, "KUSC", true, "KUSC")]
    [InlineData(null, null, true, "Streaming")]
    [InlineData(null, null, false, "Not Playing")]
    public void TitleFallsBackFromSongToStationToPlaybackState(string? songName, string? streamName, bool stillPlaying, string expected)
    {
        var status = new PlayerStatusDto
        {
            CurrentSong = songName is null ? null : new Song { Name = songName },
            StreamName = streamName,
            IsStream = streamName is not null,
            StillPlaying = stillPlaying
        };

        var state = LiveActivityPayload.CreateContentState(new PlayerStatusSnapshot(status, capturedAt));

        Assert.Equal(expected, state.Title);
    }

    private static string[] propertyNames(JsonElement element)
        => element.EnumerateObject().Select(p => p.Name).ToArray();
}
