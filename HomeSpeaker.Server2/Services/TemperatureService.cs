using System.Text;
using System.Text.Json;
using HomeSpeaker.Shared.Temperature;
using Microsoft.Extensions.Caching.Memory;

namespace HomeSpeaker.Server2.Services;

public sealed class TemperatureService : ITemperatureService
{
    private readonly HttpClient httpClient;
    private readonly IConfiguration configuration;
    private readonly ILogger<TemperatureService> logger;
    private readonly IMemoryCache cache;
    private readonly TimeProvider timeProvider;

    private const string CacheKey = "temperature-status";
    private const string DiagnosticsCacheKey = "temperature-diagnostics";
    private static readonly TimeSpan diagnosticsCacheExpiration = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan cacheExpiration = TimeSpan.FromMinutes(2);

    public TemperatureService(HttpClient httpClient, IConfiguration configuration, ILogger<TemperatureService> logger, IMemoryCache cache, TimeProvider timeProvider)
    {
        this.httpClient = httpClient;
        this.configuration = configuration;
        this.logger = logger;
        this.cache = cache;
        this.timeProvider = timeProvider;

        // Configure the HttpClient for Govee API
        var apiBaseUrl = this.configuration["Temperature:ApiBaseUrl"];
        var apiKey = this.configuration["Temperature:ApiKey"];

        this.logger.LogInformation("Initializing TemperatureService with API Base URL: {ApiBaseUrl}", apiBaseUrl ?? "Not Set");
        if (!string.IsNullOrEmpty(apiBaseUrl))
        {
            this.httpClient.BaseAddress = new Uri(apiBaseUrl);
        }

        if (!string.IsNullOrEmpty(apiKey))
        {
            this.httpClient.DefaultRequestHeaders.Add("Govee-API-Key", apiKey);
            this.logger.LogInformation("Govee API Key configured successfully");
        }
        else
        {
            this.logger.LogWarning("Govee API Key not configured. Temperature monitoring will use default values. To enable real temperature monitoring, add 'Temperature:ApiKey' to your configuration.");
        }
    }

    public async Task<List<Device>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        // Check if API key is configured
        var apiKey = configuration["Temperature:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            logger.LogWarning("Govee API key not configured. Temperature monitoring will use default values.");
            return [];
        }

        try
        {
            logger.LogInformation("Fetching devices from Govee API...");
            var response = await httpClient.GetAsync("user/devices", cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogInformation("Received response from Govee API: {Response}", json);
            var data = JsonSerializer.Deserialize<DeviceResponse>(json);
            return data?.Data ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch devices from Govee API");
            // Return empty list if API is not available
            return [];
        }
    }

    private async Task<DeviceStatePayload?> getDeviceStateAsync(Device device, CancellationToken cancellationToken)
    {
        var apiKey = configuration["Temperature:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            logger.LogWarning("Govee API key not configured; no reading for device {DeviceName}.", device.DeviceName);
            return null;
        }

        try
        {
            logger.LogInformation("Fetching state for device: {DeviceName} ({DeviceId})", device.DeviceName, device.DeviceId);
            var payload = new
            {
                requestId = Guid.NewGuid().ToString(),
                payload = new { sku = device.Sku, device = device.DeviceId }
            };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync("device/state", content, cancellationToken);
            response.EnsureSuccessStatusCode();
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<DeviceStateResponse>(responseJson)?.Payload;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Failed to get state for device {DeviceName}", device.DeviceName);
            return null;
        }
    }

    public async Task<TemperatureStatus> GetTemperatureStatusAsync(CancellationToken cancellationToken = default)
    {
        // Try to get cached value
        if (cache.TryGetValue(CacheKey, out TemperatureStatus? cachedValue))
        {
            logger.LogInformation("Returning cached temperature status: {TemperatureData}", JsonSerializer.Serialize(cachedValue));
            return cachedValue!;
        }

        // Cache miss, fetch new data
        logger.LogInformation("Temperature cache miss, fetching fresh data...");
        var temperatureStatus = await getTemperatureStatusInternalAsync(cancellationToken);

        // Set cache timestamp
        temperatureStatus.LastCachedAt = timeProvider.GetUtcNow().UtcDateTime;

        // Cache the result with absolute expiration
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = cacheExpiration,
            Priority = CacheItemPriority.Normal
        };

        cache.Set(CacheKey, temperatureStatus, cacheOptions);
        logger.LogInformation("Temperature data cached for {Minutes} minutes", cacheExpiration.TotalMinutes);

        return temperatureStatus;
    }

    /// <summary>
    /// Clears the temperature cache
    /// </summary>
    public void ClearCache()
    {
        cache.Remove(CacheKey);
        logger.LogInformation("Temperature cache cleared");
    }

    public Task<bool> ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            ClearCache();
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clear temperature cache");
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Clears the cache and fetches fresh data
    /// </summary>
    public async Task<TemperatureStatus> RefreshAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Refreshing temperature data (clearing cache and fetching fresh data)");
        await ClearCacheAsync(cancellationToken);
        return await GetTemperatureStatusAsync(cancellationToken);
    }

    /// <summary>
    /// What each matched sensor reports, plus the devices no room claimed. Never includes the API key or device ids.
    /// Cached briefly, because it costs one Govee call per sensor and the account has a request quota.
    /// </summary>
    public async Task<SensorDiagnosticsReport> GetSensorDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(DiagnosticsCacheKey, out SensorDiagnosticsReport? cached) && cached is not null)
        {
            return cached;
        }

        var devices = await GetDevicesAsync(cancellationToken);
        var rooms = TemperatureDeviceMatcher.EffectiveRooms(devices, TemperatureRoomConfig.FromConfiguration(configuration).Rooms);
        var deviceMap = TemperatureDeviceMatcher.Match(devices, rooms);
        var report = new SensorDiagnosticsReport();
        foreach (var (room, _) in rooms)
        {
            if (deviceMap[room] is not { } device)
            {
                continue;
            }

            var state = await getDeviceStateAsync(device, cancellationToken);
            report.Sensors.Add(new SensorDiagnostic
            {
                Room = room,
                DeviceName = device.DeviceName,
                Sku = device.Sku,
                StateAvailable = state is not null,
                CapabilityInstances = state?.CapabilityInstances ?? [],
                BatteryPercent = state?.Battery,
            });
        }

        var claimed = deviceMap.Values.OfType<Device>().Select(d => d.DeviceId).ToHashSet(StringComparer.Ordinal);
        report.UnmatchedDevices.AddRange(devices
            .Where(d => !claimed.Contains(d.DeviceId))
            .Select(d => new UnmatchedDevice { Name = d.DeviceName, Sku = d.Sku }));

        cache.Set(DiagnosticsCacheKey, report, diagnosticsCacheExpiration);
        return report;
    }

    private async Task<TemperatureStatus> getTemperatureStatusInternalAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Getting temperature status...");
        var settings = TemperatureRoomConfig.FromConfiguration(configuration);
        var threshold = settings.ThresholdF;
        var devices = await GetDevicesAsync(cancellationToken);

        var temperatureStatus = new TemperatureStatus
        {
            ReadingTakenAt = timeProvider.GetUtcNow().UtcDateTime,
            Threshold = threshold
        };

        var rooms = TemperatureDeviceMatcher.EffectiveRooms(devices, settings.Rooms);
        var deviceMap = TemperatureDeviceMatcher.Match(devices, rooms);

        // One state request per matched device gives both the temperature and (when reported) the battery.
        var readings = new List<RoomReading>();
        foreach (var (room, _) in rooms)
        {
            var device = deviceMap[room];
            var state = device is null ? null : await getDeviceStateAsync(device, cancellationToken);
            if (device is not null && state is not null && state.SensorTemperature is null)
            {
                logger.LogWarning("Temperature capability not found in state for device {DeviceName}", device.DeviceName);
            }

            readings.Add(new RoomReading { Name = room, Temperature = state?.SensorTemperature, BatteryPercent = state?.Battery });
        }

        temperatureStatus.Rooms = readings;

        var outside = findRoom(readings, settings.OutsideRoom);
        temperatureStatus.OutsideRoomName = outside?.Name;
        temperatureStatus.OutsideTemperature = outside?.Temperature;

        if (settings.WindowAdvice)
        {
            // An unconfigured indoor room defaults to the first room that is not the outside one.
            var indoor = findRoom(readings, settings.IndoorRoom)
                ?? readings.FirstOrDefault(r => !string.Equals(r.Name, outside?.Name, StringComparison.OrdinalIgnoreCase));
            temperatureStatus.IndoorRoomName = indoor?.Name;
            temperatureStatus.IndoorTemperature = indoor?.Temperature;
        }

        // Calculate temperature difference and determine if within threshold
        if (temperatureStatus.OutsideTemperature is { } outsideTemp && temperatureStatus.IndoorTemperature is { } indoorTemp)
        {
            temperatureStatus.TemperatureDifference = Math.Abs(outsideTemp - indoorTemp);
            temperatureStatus.IsWithinThreshold = temperatureStatus.TemperatureDifference <= threshold;
            temperatureStatus.ShouldWindowsBeClosed = outsideTemp >= indoorTemp || outsideTemp < settings.WindowMinOutsideF;
        }

        return temperatureStatus;
    }

    private static RoomReading? findRoom(IReadOnlyList<RoomReading> readings, string? name) =>
        name is null ? null : readings.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
}
