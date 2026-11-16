using System.Net;
using System.Text.Json;
using HomeSpeaker.Server2.Services.BleTemperature;
using HomeSpeaker.Server2.Tests.Photos;
using HomeSpeaker.Shared.Temperature;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeSpeaker.Server2.Tests.BleTemperature;

public sealed class BleScanCycleTests
{
    private const string BoysMac = "C9:38:30:34:17:76";
    private static readonly DateTimeOffset start = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyDictionary<ushort, byte[]> boysFrame = new Dictionary<ushort, byte[]>
    {
        [0x004c] = [0x02, 0x15],
        [0x0001] = Convert.FromHexString("0101031eda64"),
    };

    private sealed class FakeScanner(params BleAdvertisement[] advertisements) : IBleScanner
    {
        public int Scans { get; private set; }

        public Task ScanAsync(TimeSpan duration, Action<BleAdvertisement> onAdvertisement, CancellationToken cancellationToken)
        {
            Scans++;
            foreach (var advertisement in advertisements)
            {
                onAdvertisement(advertisement);
            }

            return Task.CompletedTask;
        }
    }

    private static (BleScanCycle Cycle, SensorReadingStore Store, FakeHttpMessageHandler Handler) create(
        IBleScanner scanner,
        string? hubUrl = null,
        Func<HttpRequestMessage, string?, HttpResponseMessage>? respond = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Temperature:Source"] = "bluetooth",
            ["Temperature:HubUrl"] = hubUrl,
        }).Build();
        var clock = new ManualTimeProvider(start);
        var store = new SensorReadingStore(new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), clock);
        var handler = new FakeHttpMessageHandler(respond ?? ((_, _) => new HttpResponseMessage(HttpStatusCode.NoContent)));
        var sender = new HubReadingSender(new FakeHttpClientFactory(new HttpClient(handler)), NullLogger<HubReadingSender>.Instance);
        var cycle = new BleScanCycle(scanner, store, sender, configuration, clock, NullLogger<BleScanCycle>.Instance, reporter: "kitchen");
        return (cycle, store, handler);
    }

    [Fact]
    public async Task GoveeFrames_AreDecodedIntoTheStore_OtherDataIgnored()
    {
        var (cycle, store, _) = create(new FakeScanner(
            new BleAdvertisement(BoysMac, "GVH5100_1776", -63, boysFrame),
            new BleAdvertisement("C5:38:30:34:66:84", "Govee_H5151_6684", -65, new Dictionary<ushort, byte[]> { [0x8801] = [1, 2, 3, 4, 5, 6] }),
            new BleAdvertisement("74:9A:AF:42:E8:C0", null, -66, new Dictionary<ushort, byte[]> { [0x004c] = [0x10] })));

        await cycle.RunOnceAsync(CancellationToken.None);

        var reading = Assert.Single(store.All);
        Assert.Equal(BoysMac, reading.Mac);
        Assert.Equal("GVH5100_1776", reading.Name);
        Assert.Equal(20.4, reading.TemperatureC, 3);
        Assert.Equal(-63, reading.Rssi);
        Assert.Equal("kitchen", reading.Reporter);
        Assert.Equal(start.UtcDateTime, reading.ObservedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("SomePhone")]
    public async Task Company0001FrameFromNonGoveeName_IsIgnored(string? name)
    {
        // 0x0001 is Nokia's company id; govee-ble also requires a Govee name before trusting it.
        var (cycle, store, _) = create(new FakeScanner(new BleAdvertisement(BoysMac, name, -63, boysFrame)));

        await cycle.RunOnceAsync(CancellationToken.None);

        Assert.Empty(store.All);
    }

    [Fact]
    public async Task Sender_RejectedBatch_IsNotResent()
    {
        var handler = new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.BadRequest));
        var sender = new HubReadingSender(new FakeHttpClientFactory(new HttpClient(handler)), NullLogger<HubReadingSender>.Instance);
        var hub = new Uri("https://kitchen.example/");
        var reading = new SensorReading { Mac = BoysMac, Name = "GVH5100_1776", ObservedAt = start.UtcDateTime.AddMinutes(5) };

        await sender.SendAsync(hub, [reading], CancellationToken.None);
        await sender.SendAsync(hub, [], CancellationToken.None);

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Sender_NewestHeardReplacesQueued_EvenWithEarlierClock()
    {
        var handler = new FakeHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var sender = new HubReadingSender(new FakeHttpClientFactory(new HttpClient(handler)), NullLogger<HubReadingSender>.Instance);
        var hub = new Uri("https://kitchen.example/");

        // A clock that ran fast, then got corrected by NTP.
        await sender.SendAsync(hub, [new SensorReading { Mac = BoysMac, Name = "GVH5100_1776", Rssi = -60, ObservedAt = start.UtcDateTime.AddMinutes(5) }], CancellationToken.None);
        await sender.SendAsync(hub, [new SensorReading { Mac = BoysMac, Name = "GVH5100_1776", Rssi = -70, ObservedAt = start.UtcDateTime }], CancellationToken.None);

        var sent = Assert.Single(JsonSerializer.Deserialize<SensorReading[]>(handler.Requests[1].Body!, JsonSerializerOptions.Web)!);
        Assert.Equal(-70, sent.Rssi);
    }

    [Fact]
    public async Task OnTheHub_NothingIsSent()
    {
        var (cycle, _, handler) = create(new FakeScanner(new BleAdvertisement(BoysMac, "GVH5100_1776", -63, boysFrame)));

        await cycle.RunOnceAsync(CancellationToken.None);

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task OffTheHub_LatestReadingPerSensorIsPostedToTheHub()
    {
        var (cycle, _, handler) = create(
            new FakeScanner(
                new BleAdvertisement(BoysMac, "GVH5100_1776", -63, boysFrame),
                new BleAdvertisement(BoysMac, "GVH5100_1776", -61, boysFrame)),
            hubUrl: "https://kitchen.example");

        await cycle.RunOnceAsync(CancellationToken.None);

        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri("https://kitchen.example/api/temperature/readings"), request.RequestUri);
        var sent = Assert.Single(JsonSerializer.Deserialize<SensorReading[]>(body!, JsonSerializerOptions.Web)!);
        Assert.Equal(-61, sent.Rssi);
    }

    [Fact]
    public async Task HubDown_ReadingsAreResentNextCycle()
    {
        var fail = true;
        var (cycle, _, handler) = create(
            new FakeScanner(new BleAdvertisement(BoysMac, "GVH5100_1776", -63, boysFrame)),
            hubUrl: "https://kitchen.example",
            respond: (_, _) => new HttpResponseMessage(fail ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.NoContent));

        await cycle.RunOnceAsync(CancellationToken.None);
        fail = false;
        await cycle.RunOnceAsync(CancellationToken.None);
        await cycle.RunOnceAsync(CancellationToken.None);

        Assert.Equal(3, handler.Requests.Count);
        Assert.Single(JsonSerializer.Deserialize<SensorReading[]>(handler.Requests[1].Body!, JsonSerializerOptions.Web)!);
    }

    [Fact]
    public async Task NothingHeard_NothingSent()
    {
        var (cycle, _, handler) = create(new FakeScanner(), hubUrl: "https://kitchen.example");

        await cycle.RunOnceAsync(CancellationToken.None);

        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("/org/bluez/hci0/dev_C9_38_30_34_17_76", "C9:38:30:34:17:76")]
    [InlineData("/org/bluez/hci0/dev_c9_38_30_34_17_76", "C9:38:30:34:17:76")]
    [InlineData("/org/bluez/hci0", null)]
    [InlineData("/org/bluez/hci0/dev_C9_38_30_34_17_76/service0010", null)]
    public void MacFromPath(string path, string? mac) => Assert.Equal(mac, BlueZScanner.MacFromPath(path));
}
