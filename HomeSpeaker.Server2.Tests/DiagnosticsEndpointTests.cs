using System.Net;
using System.Text.Json;
using HomeSpeaker.Server2.Settings;

namespace HomeSpeaker.Server2.Tests;

public class DiagnosticsEndpointTests
{
    [Fact]
    public async Task Diagnostics_NoPin_ReturnsLibraryAndLogs()
    {
        using var factory = new TestServerFactory();
        using var client = factory.CreateClient();

        using var json = JsonDocument.Parse(await client.GetStringAsync(new Uri("/api/diagnostics", UriKind.Relative)));
        var root = json.RootElement;

        var syncs = root.GetProperty("library").GetProperty("recentSyncs");
        Assert.Equal("startup", syncs[0].GetProperty("reason").GetString());
        Assert.True(root.GetProperty("logs").TryGetProperty("byCategory", out _));
        Assert.Equal(JsonValueKind.Array, root.GetProperty("samples").ValueKind);
    }

    [Fact]
    public async Task Diagnostics_PinSetAndNotLocal_Forbidden()
    {
        using var factory = new TestServerFactory()
            .WithSettings(new Dictionary<string, string?> { [AdminPin.ConfigKey] = AdminPin.Hash("123456") });
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/api/diagnostics", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
