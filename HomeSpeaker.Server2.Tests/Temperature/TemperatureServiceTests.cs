using System.Globalization;
using HomeSpeaker.Server2.Services;
using HomeSpeaker.Server2.Tests.Photos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeSpeaker.Server2.Tests.Temperature;

public sealed class TemperatureServiceTests
{
    private static readonly DateTimeOffset now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    private static string stateJson(double fahrenheit, string? extraCapability = null) =>
        """
        {"payload":{"capabilities":[
          {"type":"devices.capabilities.online","instance":"online","state":{"value":true}},
          {"type":"devices.capabilities.property","instance":"sensorTemperature","state":{"value":TEMP}},
          {"type":"devices.capabilities.property","instance":"sensorHumidity","state":{"value":41}}
          EXTRA
        ]}}
        """
        .Replace("TEMP", fahrenheit.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("EXTRA", extraCapability ?? "", StringComparison.Ordinal);

    private static string devicesJson(params (string Id, string Name)[] devices) =>
        "{\"data\":[" + string.Join(",", devices.Select(d => $"{{\"device\":\"{d.Id}\",\"deviceName\":\"{d.Name}\",\"sku\":\"H5179\"}}")) + "]}";

    private static TemperatureService createService(
        Func<HttpRequestMessage, string?, HttpResponseMessage> respond,
        bool withApiKey = true)
    {
        var settings = new Dictionary<string, string?> { ["Temperature:ApiBaseUrl"] = "https://govee.test/" };
        if (withApiKey)
        {
            settings["Temperature:ApiKey"] = "test-key";
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var client = new HttpClient(new FakeHttpMessageHandler(respond));
        return new TemperatureService(client, configuration, NullLogger<TemperatureService>.Instance, new MemoryCache(new MemoryCacheOptions()), new FixedTimeProvider(now));
    }

    private static HttpResponseMessage respondWith(
        HttpRequestMessage request,
        string? body,
        string devices,
        Func<string, HttpResponseMessage> state)
    {
        if (request.RequestUri!.AbsolutePath.EndsWith("user/devices", StringComparison.Ordinal))
        {
            return FakeHttpMessageHandler.Json(devices);
        }

        // device/state: pick the response by the device id in the request body.
        return state(body!);
    }

    [Fact]
    public async Task FailedRead_LeavesThatRoomNull_InsteadOfAFake70()
    {
        var devices = devicesJson(("out", "Outside"), ("boys", "Boys Room"), ("girls", "Girls Room"));
        var service = createService((request, body) => respondWith(request, body, devices, requestBody =>
            requestBody.Contains("\"boys\"", StringComparison.Ordinal)
                ? new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
                : FakeHttpMessageHandler.Json(stateJson(requestBody.Contains("\"out\"", StringComparison.Ordinal) ? 55.5 : 67.1))));

        var status = await service.GetTemperatureStatusAsync();

        Assert.Null(status.BoysRoomTemperature);
        Assert.Equal(55.5, status.OutsideTemperature);
        Assert.Equal(67.1, status.YoungerGirlsRoomTemperature);
    }

    [Fact]
    public async Task ResponseWithoutTemperatureCapability_IsNull()
    {
        var devices = devicesJson(("boys", "Boys Room"));
        var service = createService((request, body) => respondWith(request, body, devices, _ =>
            FakeHttpMessageHandler.Json("""{"payload":{"capabilities":[{"type":"devices.capabilities.online","instance":"online","state":{"value":true}}]}}""")));

        var status = await service.GetTemperatureStatusAsync();

        Assert.Null(status.BoysRoomTemperature);
    }

    [Fact]
    public async Task MissingApiKey_GivesNoReadings_NotADefault()
    {
        var service = createService((_, _) => throw new InvalidOperationException("no request expected"), withApiKey: false);

        var status = await service.GetTemperatureStatusAsync();

        Assert.Null(status.OutsideTemperature);
        Assert.Null(status.BoysRoomTemperature);
        Assert.Null(status.YoungerGirlsRoomTemperature);
    }

    [Fact]
    public async Task GetDeviceTemperature_ThrowingTransport_ReturnsNull()
    {
        var service = createService((_, _) => throw new HttpRequestException("boom"));
        var device = new HomeSpeaker.Shared.Temperature.Device { DeviceId = "d", DeviceName = "Boys", Sku = "H5179" };

        Assert.Null(await service.GetDeviceTemperatureAsync(device));
    }

    [Fact]
    public async Task WindowAdvice_StillComputedFromOutsideAndYoungerGirls()
    {
        var devices = devicesJson(("out", "Outside"), ("girls", "Girls Room"));
        var service = createService((request, body) => respondWith(request, body, devices, requestBody =>
            FakeHttpMessageHandler.Json(stateJson(requestBody.Contains("\"out\"", StringComparison.Ordinal) ? 60 : 70))));

        var status = await service.GetTemperatureStatusAsync();

        Assert.False(status.ShouldWindowsBeClosed);
        Assert.Equal(10, status.TemperatureDifference);
    }

    [Fact]
    public async Task WindowAdvice_NotComputed_WhenGirlsRoomReadFails()
    {
        var devices = devicesJson(("out", "Outside"), ("girls", "Girls Room"));
        var service = createService((request, body) => respondWith(request, body, devices, requestBody =>
            requestBody.Contains("\"girls\"", StringComparison.Ordinal)
                ? new HttpResponseMessage(System.Net.HttpStatusCode.BadGateway)
                : FakeHttpMessageHandler.Json(stateJson(60))));

        var status = await service.GetTemperatureStatusAsync();

        Assert.Null(status.YoungerGirlsRoomTemperature);
        Assert.Equal(0, status.TemperatureDifference);
    }

    private const string BatteryCapability = """
        ,{"type":"devices.capabilities.property","instance":"battery","state":{"value":3}}
        """;

    [Fact]
    public async Task BatteryCapability_IsParsedPerRoom()
    {
        var devices = devicesJson(("out", "Outside"), ("boys", "Boys Room"));
        var service = createService((request, body) => respondWith(request, body, devices, requestBody =>
            FakeHttpMessageHandler.Json(requestBody.Contains("\"boys\"", StringComparison.Ordinal)
                ? stateJson(70, BatteryCapability)
                : stateJson(55))));

        var status = await service.GetTemperatureStatusAsync();

        var boys = Assert.Single(status.Rooms, r => string.Equals(r.Name, "Boy", StringComparison.Ordinal));
        Assert.Equal(3, boys.BatteryPercent);
        Assert.Equal(70, boys.Temperature);
        var outside = Assert.Single(status.Rooms, r => string.Equals(r.Name, "Outside", StringComparison.Ordinal));
        Assert.Null(outside.BatteryPercent);
        Assert.Equal(70, status.BoysRoomTemperature);
    }

    [Theory]
    [InlineData("battery")]
    [InlineData("batteryLevel")]
    [InlineData("BatteryPercent")]
    public async Task BatteryInstance_MatchedCaseInsensitively(string instance)
    {
        var capability = $",{{\"type\":\"devices.capabilities.property\",\"instance\":\"{instance}\",\"state\":{{\"value\":42}}}}";
        var devices = devicesJson(("boys", "Boys Room"));
        var service = createService((request, body) => respondWith(request, body, devices, _ =>
            FakeHttpMessageHandler.Json(stateJson(70, capability))));

        var status = await service.GetTemperatureStatusAsync();

        Assert.Equal(42, status.Rooms.Single(r => string.Equals(r.Name, "Boy", StringComparison.Ordinal)).BatteryPercent);
    }

    [Fact]
    public async Task NoBatteryCapability_MeansNullBattery()
    {
        var devices = devicesJson(("boys", "Boys Room"));
        var service = createService((request, body) => respondWith(request, body, devices, _ => FakeHttpMessageHandler.Json(stateJson(70))));

        var status = await service.GetTemperatureStatusAsync();

        Assert.All(status.Rooms, r => Assert.Null(r.BatteryPercent));
    }

    [Fact]
    public async Task Rooms_ListsEveryRoom_EvenWithoutADevice()
    {
        var devices = devicesJson(("out", "Outside"));
        var service = createService((request, body) => respondWith(request, body, devices, _ => FakeHttpMessageHandler.Json(stateJson(55))));

        var status = await service.GetTemperatureStatusAsync();

        Assert.Equal(["Outside", "Greenhouse", "Emma", "Boy", "Downstairs", "Girl"], status.Rooms.Select(r => r.Name));
        Assert.Equal(55, status.Rooms[0].Temperature);
        Assert.All(status.Rooms.Skip(1), r => Assert.Null(r.Temperature));
    }

    [Fact]
    public async Task SensorDiagnostics_ListInstancesAndBattery_ForMatchedRoomsOnly()
    {
        var devices = devicesJson(("boys", "Boys Room"), ("misc", "Garage"));
        var service = createService((request, body) => respondWith(request, body, devices, _ =>
            FakeHttpMessageHandler.Json(stateJson(70, BatteryCapability))));

        var diagnostics = await service.GetSensorDiagnosticsAsync();

        var boys = Assert.Single(diagnostics);
        Assert.Equal("Boy", boys.Room);
        Assert.Equal("Boys Room", boys.DeviceName);
        Assert.Equal("H5179", boys.Sku);
        Assert.True(boys.StateAvailable);
        Assert.Equal(["online", "sensorTemperature", "sensorHumidity", "battery"], boys.CapabilityInstances);
        Assert.Equal(3, boys.BatteryPercent);
        Assert.DoesNotContain("test-key", System.Text.Json.JsonSerializer.Serialize(diagnostics), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SensorDiagnostics_FailedStateRead_IsMarkedUnavailable()
    {
        var devices = devicesJson(("boys", "Boys Room"));
        var service = createService((request, body) => respondWith(request, body, devices, _ =>
            new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)));

        var boys = Assert.Single(await service.GetSensorDiagnosticsAsync());

        Assert.False(boys.StateAvailable);
        Assert.Empty(boys.CapabilityInstances);
        Assert.Null(boys.BatteryPercent);
    }
}
