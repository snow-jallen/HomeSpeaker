using HomeSpeaker.Server2.Services.PlayHistory;
using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Tests.PlayHistory;

public sealed class PlayTrackerTests
{
    private static readonly DateTimeOffset start = new(2026, 11, 11, 18, 0, 0, TimeSpan.Zero);
    private readonly PlayTracker tracker = new();

    [Fact]
    public void SongPlayedToTheEndIsAPlayWhenTheNextOneStarts()
    {
        tracker.Observe(playing("/music/a.mp3", 1, 200), start);
        tracker.Observe(playing("/music/a.mp3", 199, 200), start.AddSeconds(198));

        var done = tracker.Observe(playing("/music/b.mp3", 0, 180), start.AddSeconds(200));

        Assert.NotNull(done);
        Assert.Equal("/music/a.mp3", done.SongPath);
        Assert.False(done.Skipped);
        Assert.Equal(start, done.StartedAt);
        Assert.Equal(TimeSpan.FromSeconds(199), done.Heard);
    }

    [Fact]
    public void SkipBeforeHalfwayIsASkip()
    {
        tracker.Observe(playing("/music/a.mp3", 10, 200), start);
        tracker.Observe(playing("/music/a.mp3", 40, 200), start.AddSeconds(30));

        var done = tracker.Skipped();

        Assert.NotNull(done);
        Assert.True(done.Skipped);
    }

    [Fact]
    public void SkipAfterHalfwayCountsAsAPlay()
    {
        tracker.Observe(playing("/music/a.mp3", 150, 200), start);

        var done = tracker.Skipped();

        Assert.NotNull(done);
        Assert.False(done.Skipped);
    }

    [Fact]
    public void SkippedSongStillShownForAMomentIsNotCountedAgain()
    {
        tracker.Observe(playing("/music/a.mp3", 10, 200), start);
        tracker.Skipped();

        Assert.Null(tracker.Observe(playing("/music/a.mp3", 12, 200), start.AddSeconds(2)));
        Assert.Null(tracker.Observe(playing("/music/b.mp3", 0, 200), start.AddSeconds(4)));
        Assert.Null(tracker.Observe(playing("/music/b.mp3", 95, 200), start.AddSeconds(99)));
        var done = tracker.Observe(playing("/music/c.mp3", 0, 200), start.AddSeconds(100));

        Assert.Equal("/music/b.mp3", done?.SongPath);
    }

    [Fact]
    public void SongReplacedWithinSecondsWithoutASkipIsIgnored()
    {
        tracker.Observe(playing("/music/a.mp3", 3, 200), start);

        Assert.Null(tracker.Observe(playing("/music/b.mp3", 0, 200), start.AddSeconds(3)));
    }

    [Fact]
    public void PauseAndResumeIsOnePlay()
    {
        tracker.Observe(playing("/music/a.mp3", 60, 200), start);
        Assert.Null(tracker.Observe(new PlayerStatus(), start.AddSeconds(2)));
        Assert.Null(tracker.Observe(playing("/music/a.mp3", 61, 200), start.AddMinutes(3)));

        var done = tracker.Observe(playing("/music/b.mp3", 0, 200), start.AddMinutes(6));

        Assert.Equal(TimeSpan.FromSeconds(61), done?.Heard);
    }

    [Fact]
    public void LongSilenceFinishesTheSong()
    {
        tracker.Observe(playing("/music/a.mp3", 120, 200), start);
        tracker.Observe(new PlayerStatus(), start.AddSeconds(2));

        var done = tracker.Observe(new PlayerStatus(), start.AddSeconds(2) + PlayTracker.IdleLimit);

        Assert.Equal("/music/a.mp3", done?.SongPath);
    }

    [Fact]
    public void SameSongStartingOverIsASecondPlay()
    {
        tracker.Observe(playing("/music/a.mp3", 190, 200), start);

        var done = tracker.Observe(playing("/music/a.mp3", 1, 200), start.AddSeconds(12));

        Assert.Equal("/music/a.mp3", done?.SongPath);
        Assert.Null(tracker.Observe(playing("/music/a.mp3", 3, 200), start.AddSeconds(14)));
    }

    [Fact]
    public void StreamsAreNotTracked()
    {
        tracker.Observe(new PlayerStatus { StillPlaying = true, IsStream = true, Elapsed = TimeSpan.FromMinutes(5) }, start);

        Assert.Null(tracker.Skipped());
    }

    private static PlayerStatus playing(string path, int elapsedSeconds, int lengthSeconds) => new()
    {
        StillPlaying = true,
        CurrentSong = new Song { Name = Path.GetFileNameWithoutExtension(path), Path = path },
        Elapsed = TimeSpan.FromSeconds(elapsedSeconds),
        Remaining = TimeSpan.FromSeconds(lengthSeconds - elapsedSeconds),
    };
}
