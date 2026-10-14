using HomeSpeaker.Server2.Services.Photos;

namespace HomeSpeaker.Server2.Tests.Photos;

public sealed class VideoEvictionPolicyTests
{
    private static VideoPrefetchSnapshot snapshot(long served, string[] visible, string[] upcoming, params (string Id, long At)[] lastServed) =>
        new(upcoming, visible.ToHashSet(StringComparer.Ordinal), served, lastServed.ToDictionary(x => x.Id, x => x.At, StringComparer.Ordinal));

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(25, true)]
    public void ServedVideo_IsEvictedOnceTenMoreSlidesHaveBeenServed(long served, bool evicted)
    {
        var policy = new VideoEvictionPolicy();

        var result = policy.SelectEvictions(["a"], snapshot(served, ["a", "b"], [], ("a", 0)));

        Assert.Equal(evicted, result.Contains("a"));
    }

    [Fact]
    public void VideoInTheLookAhead_IsKeptEvenWhenLongSinceServed()
    {
        var policy = new VideoEvictionPolicy();

        var result = policy.SelectEvictions(["a"], snapshot(50, ["a"], ["a"], ("a", 1)));

        Assert.Empty(result);
    }

    [Fact]
    public void VideoThatIsNoLongerVisible_IsEvictedImmediately()
    {
        var policy = new VideoEvictionPolicy();

        var result = policy.SelectEvictions(["a", "b"], snapshot(0, ["b"], ["b"]));

        Assert.Equal(["a"], result);
    }

    [Fact]
    public void VideoPrefetchedButNeverServed_AgesFromTheFirstPassThatSawIt()
    {
        var policy = new VideoEvictionPolicy();

        Assert.Empty(policy.SelectEvictions(["a"], snapshot(5, ["a"], ["a"])));
        Assert.Empty(policy.SelectEvictions(["a"], snapshot(14, ["a"], [])));
        Assert.Equal(["a"], policy.SelectEvictions(["a"], snapshot(15, ["a"], [])));
    }

    [Fact]
    public void NeverServedVideoThatIsEvictedAndCachedAgain_StartsAFreshTenSlideAge()
    {
        var policy = new VideoEvictionPolicy();
        Assert.Empty(policy.SelectEvictions(["a"], snapshot(0, ["a"], ["a"])));
        Assert.Equal(["a"], policy.SelectEvictions(["a"], snapshot(11, ["a"], [])));

        // Downloaded again at slide 11 and out of the window: it must not inherit the old first-seen of slide 0.
        Assert.Empty(policy.SelectEvictions(["a"], snapshot(12, ["a"], [])));
        Assert.Empty(policy.SelectEvictions(["a"], snapshot(21, ["a"], [])));
        Assert.Equal(["a"], policy.SelectEvictions(["a"], snapshot(22, ["a"], [])));
    }

    [Fact]
    public void ServedVideoIsKeptWhileItIsServedAgain()
    {
        var policy = new VideoEvictionPolicy();

        // Served at slide 0, then again at slide 8: ten slides after the first serve is not ten after the last.
        Assert.Empty(policy.SelectEvictions(["a"], snapshot(12, ["a"], [], ("a", 8))));
        Assert.Equal(["a"], policy.SelectEvictions(["a"], snapshot(18, ["a"], [], ("a", 8))));
    }
}
