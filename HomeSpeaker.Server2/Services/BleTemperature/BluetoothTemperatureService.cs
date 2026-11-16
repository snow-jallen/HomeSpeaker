using HomeSpeaker.Shared.Temperature;
using Microsoft.Extensions.Caching.Memory;

namespace HomeSpeaker.Server2.Services.BleTemperature;

/// <summary>
/// Room temperatures from Bluetooth sensors. The hub builds them from <see cref="SensorReadingStore"/>; any other
/// speaker asks the hub (<c>Temperature:HubUrl</c>). While the hub can't be reached (remembered for 30 s, so pages
/// don't each wait for the timeout) it shows the hub's last status aged by <see cref="HubStatusFallback"/>, or, if it
/// never reached the hub, its own readings.
/// </summary>
public sealed class BluetoothTemperatureService : ITemperatureService
{
    public const string HubClient = "temperature-hub";

    private const string RemoteCacheKey = "temperature-hub-status";
    private const string HubDownKey = "temperature-hub-down";
    private const string LastHubStatusKey = "temperature-hub-last-status";
    private static readonly TimeSpan remoteCacheFor = TimeSpan.FromSeconds(30);

    private readonly IHttpClientFactory httpClientFactory;
    private readonly IConfiguration configuration;
    private readonly SensorReadingStore store;
    private readonly IMemoryCache cache;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<BluetoothTemperatureService> logger;

    public BluetoothTemperatureService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        SensorReadingStore store,
        IMemoryCache cache,
        TimeProvider timeProvider,
        ILogger<BluetoothTemperatureService> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.configuration = configuration;
        this.store = store;
        this.cache = cache;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    /// <summary>The sensors this speaker has heard (or been sent), as devices named by address for the room picker.</summary>
    public Task<List<Device>> GetDevicesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(store.All.Select(s => new Device { DeviceId = s.Mac, DeviceName = s.Mac, Sku = s.Name }).ToList());

    public async Task<TemperatureStatus> GetTemperatureStatusAsync(CancellationToken cancellationToken = default)
    {
        if (TemperatureSource.HubUrl(configuration) is not { } hub)
        {
            return buildLocal();
        }

        if (cache.TryGetValue(RemoteCacheKey, out TemperatureStatus? cached) && cached is not null)
        {
            return cached;
        }

        if (!cache.TryGetValue(HubDownKey, out _))
        {
            try
            {
                using var client = httpClientFactory.CreateClient(HubClient);
                var status = await client.GetFromJsonAsync<TemperatureStatus>(new Uri(hub, "api/temperature"), cancellationToken)
                    ?? throw new InvalidOperationException("The hub sent no temperature status.");
                cache.Set(RemoteCacheKey, status, remoteCacheFor);
                cache.Set(LastHubStatusKey, status);
                return status;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or System.Text.Json.JsonException && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Temperature hub {Hub} unavailable ({Error}); showing its last rooms or this speaker's own readings.", hub, ex.Message);
                cache.Set(HubDownKey, true, remoteCacheFor);
            }
        }

        return cache.TryGetValue(LastHubStatusKey, out TemperatureStatus? last) && last is not null
            ? HubStatusFallback.Age(last, timeProvider.GetUtcNow().UtcDateTime)
            : buildLocal();
    }

    public Task<SensorDiagnosticsReport> GetSensorDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        var settings = TemperatureRoomConfig.FromConfiguration(configuration);
        var sensors = store.All;
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var report = new SensorDiagnosticsReport();
        foreach (var room in settings.Rooms)
        {
            var sensor = BluetoothRoomReadings.FindSensor(room, sensors);
            if (sensor is null)
            {
                continue;
            }

            claimed.Add(sensor.Mac);
            report.Sensors.Add(new SensorDiagnostic
            {
                Room = room.Room,
                DeviceName = $"{sensor.Name} ({sensor.Mac}), {sensor.Rssi} dBm via {sensor.Reporter}",
                Sku = "Bluetooth",
                StateAvailable = true,
                CapabilityInstances = ["temperature", "humidity", "battery"],
                BatteryPercent = sensor.BatteryPercent,
            });
        }

        report.UnmatchedDevices.AddRange(sensors.Where(s => !claimed.Contains(s.Mac)).Select(s => new UnmatchedDevice { Name = $"{s.Name} ({s.Mac})", Sku = "Bluetooth" }));
        return Task.FromResult(report);
    }

    public Task<bool> ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        cache.Remove(RemoteCacheKey);
        cache.Remove(HubDownKey);
        return Task.FromResult(true);
    }

    public async Task<TemperatureStatus> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await ClearCacheAsync(cancellationToken);
        return await GetTemperatureStatusAsync(cancellationToken);
    }

    private TemperatureStatus buildLocal()
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var settings = TemperatureRoomConfig.FromConfiguration(configuration);
        var status = TemperatureStatusBuilder.Build(BluetoothRoomReadings.Build(settings.Rooms, store.All, now), settings, now);
        status.ShowsReadingAge = true;
        status.LastCachedAt = now;
        return status;
    }
}
