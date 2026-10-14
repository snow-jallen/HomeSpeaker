using HomeSpeaker.Server2.Services;
using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Tests;

public sealed class PlayerStatusChangeDetectorTests
{
    private static readonly DateTimeOffset start = DateTimeOffset.Parse("2026-10-14T12:00:00Z");
    private static readonly Song song = new() { SongId = 7, Name = "Clair de Lune", Path = "/music/debussy.mp3", Artist = "Debussy" };

    [Fact]
    public void FirstSnapshotIsAMaterialHighPriorityChange()
    {
        var change = PlayerStatusChangeDetector.Compare(null, snapshot(playing()));

        Assert.True(change.IsMaterial);
        Assert.True(change.IsHighPriority);
    }

    [Fact]
    public void ElapsedTimeTickingIsNotMaterial()
    {
        var before = snapshot(playing() with { Elapsed = TimeSpan.FromSeconds(10), Remaining = TimeSpan.FromSeconds(50), PercentComplete = 0.16m });
        var after = snapshot(playing() with { Elapsed = TimeSpan.FromSeconds(11), Remaining = TimeSpan.FromSeconds(49), PercentComplete = 0.18m }, TimeSpan.FromSeconds(1));

        var change = PlayerStatusChangeDetector.Compare(before, after);

        Assert.False(change.IsMaterial);
        Assert.Equal(PlayerStatusChange.None, change);
    }

    [Fact]
    public void ADifferentSongIsHighPriority()
    {
        var next = song with { SongId = 8, Path = "/music/satie.mp3", Name = "Gymnopedie" };

        var change = PlayerStatusChangeDetector.Compare(snapshot(playing()), snapshot(playing() with { CurrentSong = next }));

        Assert.True(change.SongChanged);
        Assert.True(change.IsHighPriority);
    }

    [Fact]
    public void SameSongWithSamePathButNewIdIsADifferentSong()
    {
        var change = PlayerStatusChangeDetector.Compare(snapshot(playing()), snapshot(playing() with { CurrentSong = song with { SongId = 99 } }));

        Assert.True(change.SongChanged);
    }

    [Fact]
    public void StoppingIsHighPriority()
    {
        var change = PlayerStatusChangeDetector.Compare(snapshot(playing()), snapshot(playing() with { StillPlaying = false }));

        Assert.True(change.PlaybackChanged);
        Assert.True(change.IsHighPriority);
    }

    [Fact]
    public void SwitchingToAStreamIsHighPriority()
    {
        var stream = playing() with
        {
            IsStream = true,
            StreamName = "KUSC",
            CurrentSong = new Song { Name = "KUSC", Path = "https://kusc.example/stream" }
        };

        var change = PlayerStatusChangeDetector.Compare(snapshot(playing()), snapshot(stream));

        Assert.True(change.StreamChanged);
        Assert.True(change.IsHighPriority);
    }

    [Fact]
    public void IcyTitleChangeIsMaterialButLowPriority()
    {
        var stream = playing() with
        {
            IsStream = true,
            StreamName = "KUSC",
            CurrentSong = new Song { Name = "KUSC", Path = "https://kusc.example/stream" }
        };
        var nowPlaying = stream with { CurrentSong = stream.CurrentSong! with { Name = "Bolero - Ravel" } };

        var change = PlayerStatusChangeDetector.Compare(snapshot(stream), snapshot(nowPlaying));

        Assert.True(change.TitleChanged);
        Assert.False(change.SongChanged);
        Assert.True(change.IsMaterial);
        Assert.False(change.IsHighPriority);
    }

    [Theory]
    [InlineData("volume")]
    [InlineData("repeat")]
    [InlineData("sleep")]
    public void SettingsChangesAreMaterialButLowPriority(string setting)
    {
        var before = playing();
        var after = setting switch
        {
            "volume" => before with { Volume = before.Volume + 5 },
            "repeat" => before with { RepeatMode = !before.RepeatMode },
            _ => before with { SleepTimerActive = true, SleepTimerRemainingMinutes = 30 }
        };

        var change = PlayerStatusChangeDetector.Compare(snapshot(before), snapshot(after));

        Assert.True(change.IsMaterial);
        Assert.False(change.IsHighPriority);
    }

    [Fact]
    public void SleepTimerCountingDownIsNotMaterial()
    {
        var before = snapshot(playing() with { SleepTimerActive = true, SleepTimerRemainingMinutes = 30 });
        var after = snapshot(playing() with { SleepTimerActive = true, SleepTimerRemainingMinutes = 25 }, TimeSpan.FromMinutes(5));

        Assert.False(PlayerStatusChangeDetector.Compare(before, after).IsMaterial);
    }

    [Fact]
    public void SleepTimerEndJitterWithinToleranceIsNotMaterial()
    {
        var before = snapshot(playing() with { SleepTimerActive = true, SleepTimerRemainingMinutes = 30 });
        var after = snapshot(playing() with { SleepTimerActive = true, SleepTimerRemainingMinutes = 30 }, TimeSpan.FromSeconds(20));

        Assert.False(PlayerStatusChangeDetector.Compare(before, after).IsMaterial);
    }

    [Fact]
    public void SleepTimerEndMovingMoreThanThirtySecondsIsMaterial()
    {
        var before = snapshot(playing() with { SleepTimerActive = true, SleepTimerRemainingMinutes = 30 });
        var after = snapshot(playing() with { SleepTimerActive = true, SleepTimerRemainingMinutes = 60 }, TimeSpan.FromSeconds(1));

        var change = PlayerStatusChangeDetector.Compare(before, after);

        Assert.True(change.SleepTimerChanged);
        Assert.False(change.IsHighPriority);
    }

    private static PlayerStatusDto playing() => new()
    {
        StillPlaying = true,
        CurrentSong = song,
        Volume = 40,
        Elapsed = TimeSpan.FromSeconds(10),
        Remaining = TimeSpan.FromSeconds(50)
    };

    private static PlayerStatusSnapshot snapshot(PlayerStatusDto status, TimeSpan? offset = null)
        => new(status, start + (offset ?? TimeSpan.Zero));
}
