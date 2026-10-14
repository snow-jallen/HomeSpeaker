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
}
