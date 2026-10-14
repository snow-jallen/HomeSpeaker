using HomeSpeaker.Server2.Services.LiveActivities;

namespace HomeSpeaker.Server2.Tests;

public sealed class LiveActivityRegistryTests
{
    private const string Token = "0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string OtherToken = "fedcba9876543210fedcba9876543210fedcba9876543210";

    private readonly ManualTimeProvider time = new(DateTimeOffset.Parse("2026-10-14T12:00:00Z"));

    [Fact]
    public void RegistrationExpiresAfterTwelveHours()
    {
        var registry = new LiveActivityRegistry(time);
        registry.Register("activity-1", Token);

        time.Advance(TimeSpan.FromHours(12) - TimeSpan.FromSeconds(1));
        Assert.Single(registry.GetActive());

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Empty(registry.GetActive());
        Assert.Equal(0, registry.Count);
    }

    [Fact]
    public void RemoveIsIdempotent()
    {
        var registry = new LiveActivityRegistry(time);
        registry.Register("activity-1", Token);

        Assert.True(registry.Remove("activity-1"));
        Assert.False(registry.Remove("activity-1"));
        Assert.False(registry.Remove("never-registered"));
        Assert.Empty(registry.GetActive());
    }

    [Fact]
    public void NewTokenReplacesOldButKeepsOriginalExpiry()
    {
        var registry = new LiveActivityRegistry(time);
        var first = registry.Register("activity-1", Token);

        time.Advance(TimeSpan.FromHours(1));
        var second = registry.Register("activity-1", OtherToken);

        var active = Assert.Single(registry.GetActive());
        Assert.Same(second, active);
        Assert.Equal(OtherToken, active.PushToken);
        Assert.Equal(first.ExpiresAt, active.ExpiresAt);
    }

    [Fact]
    public void ReRegisteringTheSameTokenKeepsTheRegistrationAndItsPushState()
    {
        var registry = new LiveActivityRegistry(time);
        var first = registry.Register("activity-1", Token);
        first.Push.LastLowPrioritySentAt = time.GetUtcNow();

        var again = registry.Register("activity-1", Token.ToUpperInvariant());

        Assert.Same(first, again);
    }

    [Fact]
    public void RemovingAStaleRegistrationLeavesItsReplacement()
    {
        var registry = new LiveActivityRegistry(time);
        var stale = registry.Register("activity-1", Token);
        var replacement = registry.Register("activity-1", OtherToken);

        Assert.False(registry.Remove(stale));
        Assert.Same(replacement, Assert.Single(registry.GetActive()));
    }

    [Fact]
    public void TokenIsNormalizedFromNsDataDescription()
    {
        var registry = new LiveActivityRegistry(time);

        var registration = registry.Register(" activity-1 ", "<01234567 89abcdef 01234567 89abcdef>");

        Assert.Equal("activity-1", registration.ActivityId);
        Assert.Equal("0123456789abcdef0123456789abcdef", registration.PushToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-hex-not-hex-not-hex-not-hex-not-hex")]
    [InlineData("abcdef")]
    public void RejectsInvalidTokens(string? token)
    {
        var registry = new LiveActivityRegistry(time);

        Assert.Throws<ArgumentException>(() => registry.Register("activity-1", token));
        Assert.Empty(registry.GetActive());
    }

    [Fact]
    public void RejectsMissingActivityId()
    {
        var registry = new LiveActivityRegistry(time);

        Assert.Throws<ArgumentException>(() => registry.Register("  ", Token));
    }
}
