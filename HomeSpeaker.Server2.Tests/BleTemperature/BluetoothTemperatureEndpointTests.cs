using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HomeSpeaker.Shared.Temperature;
using Microsoft.Extensions.DependencyInjection;

namespace HomeSpeaker.Server2.Tests.BleTemperature;

public sealed class BluetoothTemperatureEndpointTests
{
    private const string BoysMac = "C9:38:30:34:17:76";
    private const string EmmalindaMac = "C2:38:30:34:69:67";
    private const string OutsideMac = "C9:38:30:34:17:1C";

    private static readonly Uri readingsUri = new("/api/temperature/readings", UriKind.Relative);
    private static readonly Uri temperatureUri = new("/api/temperature", UriKind.Relative);

    // Blank Govee settings so appsettings.Development.json's cloud URL doesn't turn temperature on by itself.
    private static TestServerFactory hub() => new TestServerFactory().WithSettings(new Dictionary<string, string?>
    {
        ["Temperature:ApiBaseUrl"] = "",
        ["Temperature:Source"] = "bluetooth",
        ["Temperature:BluetoothRooms"] = $"Outside: {OutsideMac}; Boy's room: {BoysMac}; Emmalinda: {EmmalindaMac}",
    });

    private static SensorReading reading(string mac, double celsius, int battery, int rssi = -60, string reporter = "rpi42", DateTime? at = null) => new()
    {
        Mac = mac,
        Name = "GVH5100_" + mac[^5..].Replace(":", "", StringComparison.Ordinal),
        TemperatureC = celsius,
        Humidity = 50.4,
        BatteryPercent = battery,
        Rssi = rssi,
        ObservedAt = at ?? DateTime.UtcNow,
        Reporter = reporter,
    };

    [Fact]
    public async Task Features_BluetoothSource_TurnsTemperatureOn()
    {
        using var factory = hub();
        using var client = factory.CreateClient();

        using var json = JsonDocument.Parse(await client.GetStringAsync(new Uri("/api/features", UriKind.Relative)));

        Assert.True(json.RootElement.GetProperty("temperatureEnabled").GetBoolean());
    }

    [Fact]
    public async Task Readings_WhenTemperatureOff_AreNotAccepted()
    {
        using var factory = new TestServerFactory().WithSettings(new Dictionary<string, string?> { ["Temperature:ApiBaseUrl"] = "" });
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(readingsUri, new[] { reading(BoysMac, 20, 100) });

        // Unmapped: the Blazor page fallback answers GET only, so a POST gets 405 rather than 404.
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
    }

    [Fact]
    public async Task PostedReadings_ShowUpInRooms()
    {
        using var factory = hub();
        using var client = factory.CreateClient();

        var post = await client.PostAsJsonAsync(readingsUri, new[] { reading(BoysMac, 20, 100), reading(EmmalindaMac, 25, 10) });
        Assert.Equal(HttpStatusCode.NoContent, post.StatusCode);

        var status = await client.GetFromJsonAsync<TemperatureStatus>(temperatureUri);

        Assert.NotNull(status);
        Assert.True(status.ShowsReadingAge);
        Assert.Equal(["Outside", "Boy's room", "Emmalinda"], status.Rooms.Select(r => r.Name));
        var boys = status.Rooms[1];
        Assert.Equal(68, boys.Temperature!.Value, 3);
        Assert.Equal(50.4, boys.Humidity);
        Assert.Equal(100, boys.BatteryPercent);
        Assert.NotNull(boys.LastReadingAt);
        Assert.Equal(10, status.Rooms[2].BatteryPercent);
        Assert.Null(status.Rooms[0].Temperature);
        Assert.Null(status.Rooms[0].LastReadingAt);
        Assert.Equal("Outside", status.OutsideRoomName);
    }

    [Fact]
    public async Task StrongerSpeaker_WinsForTheSameMoment()
    {
        using var factory = hub();
        using var client = factory.CreateClient();
        var now = DateTime.UtcNow;

        await client.PostAsJsonAsync(readingsUri, new[] { reading(BoysMac, 20, 100, rssi: -60, reporter: "rpi42", at: now) });
        await client.PostAsJsonAsync(readingsUri, new[] { reading(BoysMac, 21, 100, rssi: -80, reporter: "kitchen", at: now.AddSeconds(5)) });

        var status = await client.GetFromJsonAsync<TemperatureStatus>(temperatureUri);

        Assert.Equal(68, status!.Rooms[1].Temperature!.Value, 3);
    }

    [Fact]
    public async Task Sensors_ListsEveryHeardSensor_MappedOrNot()
    {
        using var factory = hub();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync(readingsUri, new[] { reading(BoysMac, 20, 100), reading("CA:38:30:34:5F:7E", 15, 96) });

        var sensors = await client.GetFromJsonAsync<List<SensorReading>>(new Uri("/api/temperature/sensors", UriKind.Relative));

        Assert.Equal(2, sensors!.Count);
    }

    [Theory]
    [InlineData("not-a-mac", 20.0, 100)]
    [InlineData(BoysMac, 20.0, 101)]
    [InlineData(BoysMac, 200.0, 100)]
    public async Task InvalidReading_Returns400(string mac, double celsius, int battery)
    {
        using var factory = hub();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(readingsUri, new[] { reading(mac, celsius, battery) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReadingFromTheFuture_Returns400()
    {
        using var factory = hub();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(readingsUri, new[] { reading(BoysMac, 20, 100, at: DateTime.UtcNow.AddHours(1)) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReadingsPersist_AcrossRestart()
    {
        using var first = hub();
        var dbPath = first.DatabasePath;
        using (var client = first.CreateClient())
        {
            await client.PostAsJsonAsync(readingsUri, new[] { reading(BoysMac, 20, 100) });
        }

        var store = first.Services.GetRequiredService<HomeSpeaker.Server2.Services.BleTemperature.SensorReadingStore>();
        await store.SaveAsync(CancellationToken.None);

        using var second = hub().WithSettings(new Dictionary<string, string?> { ["SqliteConnectionString"] = $"Data Source={dbPath}" });
        var reloaded = second.Services.GetRequiredService<HomeSpeaker.Server2.Services.BleTemperature.SensorReadingStore>();
        await reloaded.LoadAsync(CancellationToken.None);

        Assert.Equal(BoysMac, Assert.Single(reloaded.All).Mac);
    }

    [Fact]
    public async Task Readings_OnANonHubSpeaker_AreRefused()
    {
        using var factory = hub().WithSettings(new Dictionary<string, string?> { ["Temperature:HubUrl"] = "https://kitchen.example" });
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(readingsUri, new[] { reading(BoysMac, 20, 100) });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Readings_MoreThan64InOnePost_Returns400()
    {
        using var factory = hub();
        using var client = factory.CreateClient();
        var many = Enumerable.Range(0, 65).Select(i => reading($"C9:38:30:34:00:{i:X2}", 20, 100)).ToArray();

        var response = await client.PostAsJsonAsync(readingsUri, many);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
