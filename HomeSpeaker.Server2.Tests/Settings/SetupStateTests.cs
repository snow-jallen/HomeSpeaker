using HomeSpeaker.Server2.Settings;
using Microsoft.Extensions.Configuration;

namespace HomeSpeaker.Server2.Tests.Settings;

public sealed class SetupStateTests
{
    private static IConfiguration config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value))
            .Build();

    [Fact]
    public void IsNeeded_FreshConfig_True() => Assert.True(SetupState.IsNeeded(config()));

    [Fact]
    public void IsNeeded_CompletedTrue_False() =>
        Assert.False(SetupState.IsNeeded(config((SetupState.CompletedKey, "true"))));

    [Fact]
    public void IsNeeded_LocationConfigured_False() =>
        Assert.False(SetupState.IsNeeded(config(("Forecast:Latitude", "39.36"), ("Forecast:Longitude", "-111.58"))));

    [Fact]
    public void IsNeeded_CompletedFalseString_True() =>
        Assert.True(SetupState.IsNeeded(config((SetupState.CompletedKey, "false"))));
}
