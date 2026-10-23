using HomeSpeaker.Server2;
using Microsoft.Extensions.Configuration;

namespace HomeSpeaker.Server2.Tests;

public class FeatureFlagsTests
{
    private static IConfiguration build(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void ForecastEnabled_WithBothCoordinates_IsTrue()
    {
        var config = build(new() { ["Forecast:Latitude"] = "40.7", ["Forecast:Longitude"] = "-74.0" });
        Assert.True(FeatureFlags.ForecastEnabled(config));
    }

    [Fact]
    public void ForecastEnabled_MissingLongitude_IsFalse()
    {
        var config = build(new() { ["Forecast:Latitude"] = "40.7" });
        Assert.False(FeatureFlags.ForecastEnabled(config));
    }

    [Fact]
    public void ForecastEnabled_NonNumeric_IsFalse()
    {
        var config = build(new() { ["Forecast:Latitude"] = "abc", ["Forecast:Longitude"] = "-74.0" });
        Assert.False(FeatureFlags.ForecastEnabled(config));
    }

    [Fact]
    public void AnchorsEnabled_DefaultsOff() =>
        Assert.False(FeatureFlags.AnchorsEnabled(build(new())));

    [Theory]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("True")]
    public void AnchorsEnabled_TrueCaseInsensitive(string value) =>
        Assert.True(FeatureFlags.AnchorsEnabled(build(new() { ["Features:Anchors"] = value })));

    [Fact]
    public void PushEnabled_RequiresAllFourApnsValues()
    {
        var full = new Dictionary<string, string?>
        {
            ["PushNotifications:Apns:Topic"] = "com.example.app",
            ["PushNotifications:Apns:TeamId"] = "TEAM123",
            ["PushNotifications:Apns:KeyId"] = "KEY123",
            ["PushNotifications:Apns:PrivateKeyBase64"] = "c2VjcmV0",
        };
        Assert.True(FeatureFlags.PushEnabled(build(new(full))));

        foreach (var missing in full.Keys)
        {
            var partial = new Dictionary<string, string?>(full) { [missing] = "" };
            Assert.False(FeatureFlags.PushEnabled(build(partial)), $"{missing} empty should disable push");
        }
    }

    [Fact]
    public void PushEnabled_AcceptsKeyPathInsteadOfBase64()
    {
        var config = build(new()
        {
            ["PushNotifications:Apns:Topic"] = "com.example.app",
            ["PushNotifications:Apns:TeamId"] = "TEAM123",
            ["PushNotifications:Apns:KeyId"] = "KEY123",
            ["PushNotifications:Apns:PrivateKeyPath"] = "/keys/AuthKey.p8",
        });
        Assert.True(FeatureFlags.PushEnabled(config));
    }
}
