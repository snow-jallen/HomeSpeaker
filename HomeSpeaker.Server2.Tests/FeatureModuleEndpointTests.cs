using System.Net;
using System.Text.Json;

namespace HomeSpeaker.Server2.Tests;

public class FeatureModuleEndpointTests
{
    private static Dictionary<string, string?> settings(params (string Key, string Value)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => (string?)p.Value);

    [Fact]
    public async Task Features_DefaultConfig_AllPersonalModulesOff()
    {
        using var factory = new TestServerFactory();
        using var client = factory.CreateClient();

        using var json = JsonDocument.Parse(await client.GetStringAsync(new Uri("/api/features", UriKind.Relative)));
        var root = json.RootElement;

        Assert.False(root.GetProperty("forecastEnabled").GetBoolean());
        Assert.False(root.GetProperty("anchorsEnabled").GetBoolean());
        Assert.False(root.GetProperty("pushEnabled").GetBoolean());
    }

    [Fact]
    public async Task Features_DefaultConfig_TemperatureUnitIsFahrenheit()
    {
        using var factory = new TestServerFactory();
        using var client = factory.CreateClient();

        using var json = JsonDocument.Parse(await client.GetStringAsync(new Uri("/api/features", UriKind.Relative)));

        Assert.Equal("F", json.RootElement.GetProperty("temperatureUnit").GetString());
    }

    [Fact]
    public async Task AnchorApi_WhenDisabled_Returns404()
    {
        using var factory = new TestServerFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/anchors/definitions", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AnchorApi_WhenEnabled_Returns200()
    {
        using var factory = new TestServerFactory().WithSettings(settings(("Features:Anchors", "true")));
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/anchors/definitions", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task BloodSugarApi_WhenNightscoutUnset_Returns404()
    {
        using var factory = new TestServerFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/bloodsugar", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
