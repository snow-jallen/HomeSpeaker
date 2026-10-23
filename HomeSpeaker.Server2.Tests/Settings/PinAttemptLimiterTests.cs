using HomeSpeaker.Server2.Settings;

namespace HomeSpeaker.Server2.Tests.Settings;

public class PinAttemptLimiterTests
{
    private static (PinAttemptLimiter Limiter, ManualTimeProvider Time) create()
    {
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        return (new PinAttemptLimiter(time), time);
    }

    [Fact]
    public void FiveFailures_Locks()
    {
        var (limiter, _) = create();
        for (var i = 0; i < 4; i++)
        {
            limiter.RecordFailure("a");
        }

        Assert.False(limiter.IsLocked("a"));
        limiter.RecordFailure("a");
        Assert.True(limiter.IsLocked("a"));
        Assert.False(limiter.IsLocked("b"));
    }

    [Fact]
    public void LockExpiresAfter60Seconds()
    {
        var (limiter, time) = create();
        for (var i = 0; i < 5; i++)
        {
            limiter.RecordFailure("a");
        }

        time.Advance(TimeSpan.FromSeconds(59));
        Assert.True(limiter.IsLocked("a"));
        time.Advance(TimeSpan.FromSeconds(2));
        Assert.False(limiter.IsLocked("a"));
    }

    [Fact]
    public void Reset_ClearsFailures()
    {
        var (limiter, _) = create();
        for (var i = 0; i < 4; i++)
        {
            limiter.RecordFailure("a");
        }

        limiter.Reset("a");
        limiter.RecordFailure("a");

        Assert.False(limiter.IsLocked("a"));
    }

    [Fact]
    public void FailuresOlderThan60Seconds_DoNotCount()
    {
        var (limiter, time) = create();
        for (var i = 0; i < 4; i++)
        {
            limiter.RecordFailure("a");
        }

        time.Advance(TimeSpan.FromSeconds(61));
        limiter.RecordFailure("a");

        Assert.False(limiter.IsLocked("a"));
    }
}
