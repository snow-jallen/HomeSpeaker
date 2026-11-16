using System.Net;
using System.Text.Json;
using HomeSpeaker.Server2.Services.BleTemperature;
using HomeSpeaker.Server2.Tests.Photos;
using HomeSpeaker.Shared.Temperature;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeSpeaker.Server2.Tests.BleTemperature;

public sealed class BluetoothTemperatureServiceTests
{
    private static readonly DateTimeOffset start = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private static TemperatureStatus hubStatus(DateTime readingAt) => new()
    {
        OutsideRoomName = "Outside",
        OutsideTemperature = 41,
        IndoorRoomName = "Kitchen",
        IndoorTemperature = 70,
        ShouldWindowsBeClosed = true,
        TemperatureDifference = 29,
        ShowsReadingAge = true,
        Rooms =
        [
            new RoomReading { Name = "Outside", Temperature = 41, Humidity = 76, LastReadingAt = readingAt },
            new RoomReading { Name = "Kitchen", Temperature = 70, Humidity = 50, LastReadingAt = readingAt },
        ],
    };

    private static (BluetoothTemperatureService Service, FakeHttpMessageHandler Handler, ManualTimeProvider Clock) create(Func<HttpRequestMessage, string?, HttpResponseMessage> respond)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Temperature:Source"] = "bluetooth",
            ["Temperature:HubUrl"] = "https://kitchen.example",
        }).Build();
        var clock = new ManualTimeProvider(start);
        var handler = new FakeHttpMessageHandler(respond);
        var store = new SensorReadingStore(new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), clock);
        var service = new BluetoothTemperatureService(
            new FakeHttpClientFactory(new HttpClient(handler)),
            configuration,
            store,
            new MemoryCache(new MemoryCacheOptions()),
            clock,
            NullLogger<BluetoothTemperatureService>.Instance);
        return (service, handler, clock);
    }

    [Fact]
    public async Task HubDown_IsRememberedBriefly_SoPagesDontWaitEveryTime()
    {
        var (service, handler, _) = create((_, _) => throw new HttpRequestException("down"));

        await service.GetTemperatureStatusAsync();
        await service.GetTemperatureStatusAsync();

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task HubDown_ShowsTheHubsLastRooms_WithValuesAgedOut()
    {
        var hubUp = true;
        var (service, _, clock) = create((_, _) => hubUp
            ? FakeHttpMessageHandler.Json(JsonSerializer.Serialize(hubStatus(start.UtcDateTime), JsonSerializerOptions.Web))
            : throw new HttpRequestException("down"));

        await service.GetTemperatureStatusAsync();
        hubUp = false;
        clock.Advance(TimeSpan.FromMinutes(25));
        await service.ClearCacheAsync(); // The memory cache runs on the real clock; this is its 30 s passing.
        var status = await service.GetTemperatureStatusAsync();

        Assert.Equal(["Outside", "Kitchen"], status.Rooms.Select(r => r.Name));
        Assert.All(status.Rooms, r => Assert.Null(r.Temperature));
        Assert.All(status.Rooms, r => Assert.Equal(start.UtcDateTime, r.LastReadingAt));
        Assert.Null(status.OutsideTemperature);
        Assert.Null(status.IndoorTemperature);
        Assert.False(status.ShouldWindowsBeClosed);
        Assert.Equal(0, status.TemperatureDifference);
    }

    [Fact]
    public async Task HubDown_RecentHubValuesStillShow()
    {
        var hubUp = true;
        var (service, _, clock) = create((_, _) => hubUp
            ? FakeHttpMessageHandler.Json(JsonSerializer.Serialize(hubStatus(start.UtcDateTime), JsonSerializerOptions.Web))
            : throw new HttpRequestException("down"));

        await service.GetTemperatureStatusAsync();
        hubUp = false;
        clock.Advance(TimeSpan.FromMinutes(6));
        await service.ClearCacheAsync(); // The memory cache runs on the real clock; this is its 30 s passing.
        var status = await service.GetTemperatureStatusAsync();

        Assert.Equal(41, status.OutsideTemperature);
        Assert.True(status.ShouldWindowsBeClosed);
    }
}
