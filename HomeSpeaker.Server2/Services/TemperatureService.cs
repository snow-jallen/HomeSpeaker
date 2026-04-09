using System.Text;
using System.Text.Json;
using HomeSpeaker.Shared.Temperature;
using Microsoft.Extensions.Caching.Memory;

namespace HomeSpeaker.Server2.Services;

public sealed class TemperatureService
{
    private readonly HttpClient httpClient;
    private readonly IConfiguration configuration;
    private readonly ILogger<TemperatureService> logger;
    private readonly IMemoryCache cache;

    private const string CacheKey = "temperature-status";
    private static readonly TimeSpan cacheExpiration = TimeSpan.FromMinutes(2);

    public TemperatureService(HttpClient httpClient, IConfiguration configuration, ILogger<TemperatureService> logger, IMemoryCache cache)
    {
        this.httpClient = httpClient;
        this.configuration = configuration;
        this.logger = logger;
        this.cache = cache;

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

    public async Task<double> GetDeviceTemperatureAsync(Device device, CancellationToken cancellationToken = default)
    {
        // Check if API key is configured
        var apiKey = configuration["Temperature:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            logger.LogWarning("Govee API key not configured. Using default temperature for device {DeviceName}.", device.DeviceName);
            return 70.0; // Default room temperature
        }

        try
        {
            logger.LogInformation("Fetching temperature for device: {DeviceName} ({DeviceId})", device.DeviceName, device.DeviceId);
            var payload = new
            {
                requestId = Guid.NewGuid().ToString(),
                payload = new { sku = device.Sku, device = device.DeviceId }
            };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync("device/state", content, cancellationToken);
            response.EnsureSuccessStatusCode();
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var stateResponse = JsonSerializer.Deserialize<DeviceStateResponse>(responseJson);

            return stateResponse?.Payload?.SensorTemperature
                ?? throw new InvalidOperationException("Temperature capability not found in device state response.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get temperature for device {DeviceName}", device.DeviceName);
            // Return a default temperature if device is not available
            return 70.0; // Default room temperature
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
        temperatureStatus.LastCachedAt = DateTime.UtcNow.ToLocalTime();

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

    /// <summary>
    /// Clears the cache and fetches fresh data
    /// </summary>
    public async Task<TemperatureStatus> RefreshAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Refreshing temperature data (clearing cache and fetching fresh data)");
        ClearCache();
        return await GetTemperatureStatusAsync(cancellationToken);
    }

    private async Task<TemperatureStatus> getTemperatureStatusInternalAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Getting temperature status...");
        var threshold = configuration.GetValue<double>("TemperatureThreshold", 2.0);
        var devices = await GetDevicesAsync(cancellationToken);

        var temperatureStatus = new TemperatureStatus
        {
            ReadingTakenAt = DateTime.UtcNow.ToLocalTime(),
            Threshold = threshold
        };

        // Define device search patterns
        var devicePatterns = new Dictionary<string, string[]>
        {
            ["Outside"] = ["Outside"],
            ["Girl"] = ["Girl"],
            ["Emma"] = ["Emma"],
            ["Boy"] = ["Boy"],
            ["Downstairs"] = ["Downstairs", "Mom", "Dad", "Parent"],
            ["Greenhouse"] = ["Greenhouse"]
        };

        // Find devices using modern LINQ
        var deviceMap = devicePatterns.ToDictionary(
            kvp => kvp.Key,
            kvp => devices.FirstOrDefault(d => kvp.Value.Any(pattern =>
                d.DeviceName.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
        );

        // Get temperatures for found devices
        temperatureStatus.OutsideTemperature = await getTemperatureForDevice(deviceMap["Outside"], cancellationToken);
        temperatureStatus.YoungerGirlsRoomTemperature = await getTemperatureForDevice(deviceMap["Girl"], cancellationToken);
        temperatureStatus.OlderGirlsRoomTemperature = await getTemperatureForDevice(deviceMap["Emma"], cancellationToken);
        temperatureStatus.BoysRoomTemperature = await getTemperatureForDevice(deviceMap["Boy"], cancellationToken);
        temperatureStatus.MomAndDadsRoomTemperature = await getTemperatureForDevice(deviceMap["Downstairs"], cancellationToken);
        temperatureStatus.GreenhouseTemperature = await getTemperatureForDevice(deviceMap["Greenhouse"], cancellationToken);

        // Calculate temperature difference and determine if within threshold
        if (temperatureStatus.OutsideTemperature is { } outsideTemp &&
            temperatureStatus.YoungerGirlsRoomTemperature is { } girlsRoomTemp)
        {
            temperatureStatus.TemperatureDifference = Math.Abs(outsideTemp - girlsRoomTemp);
            temperatureStatus.IsWithinThreshold = temperatureStatus.TemperatureDifference <= threshold;
            temperatureStatus.ShouldWindowsBeClosed = outsideTemp >= girlsRoomTemp || outsideTemp < 50;
        }

        return temperatureStatus;
    }

    private async Task<double?> getTemperatureForDevice(Device? device, CancellationToken cancellationToken)
        => device is not null
            ? await GetDeviceTemperatureAsync(device, cancellationToken)
            : null;
}