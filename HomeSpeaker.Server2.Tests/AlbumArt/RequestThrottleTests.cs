using System.Diagnostics;
using HomeSpeaker.Server2.Services.AlbumArt;

namespace HomeSpeaker.Server2.Tests.AlbumArt;

public class RequestThrottleTests
{
    [Fact]
    public async Task WaitAsync_ConcurrentCallers_AreSpacedByTheInterval()
    {
        using var throttle = new RequestThrottle(TimeSpan.FromMilliseconds(150), TimeProvider.System);
        var stopwatch = Stopwatch.StartNew();

        await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => throttle.WaitAsync(CancellationToken.None)));

        // First call is immediate, the next two each wait one interval.
        Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(280), $"took {stopwatch.Elapsed.TotalMilliseconds} ms");
    }

    [Fact]
    public async Task WaitAsync_FirstCall_DoesNotWait()
    {
        using var throttle = new RequestThrottle(TimeSpan.FromSeconds(30), TimeProvider.System);
        var stopwatch = Stopwatch.StartNew();

        await throttle.WaitAsync(CancellationToken.None);

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
    }
}
