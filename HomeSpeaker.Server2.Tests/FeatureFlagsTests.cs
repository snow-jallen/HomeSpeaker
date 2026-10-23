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
}
