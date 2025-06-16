using System.Text;
using System.Text.Json;
using HomeSpeaker.Shared.Temperature;
using Microsoft.Extensions.Caching.Memory;

namespace HomeSpeaker.Server2.Services;

public sealed class TemperatureService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TemperatureService> _logger;
    private readonly IMemoryCache _cache;
    
    private const string CacheKey = "temperature-status";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(2);

    public TemperatureService(HttpClient httpClient, IConfiguration configuration, ILogger<TemperatureService> logger, IMemoryCache cache)    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _cache = cache;

        // Configure the HttpClient for Govee API
        var apiBaseUrl = _configuration["Temperature:ApiBaseUrl"];
        var apiKey = _configuration["Temperature:ApiKey"];
        
        _logger.LogInformation("Initializing TemperatureService with API Base URL: {ApiBaseUrl}", apiBaseUrl ?? "Not Set");
        if (!string.IsNullOrEmpty(apiBaseUrl))
        {
            _httpClient.BaseAddress = new Uri(apiBaseUrl);
        }
        
        _logger.LogInformation("Using Govee API Key: {ApiKey}", apiKey ?? "Not Set");
        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Govee-API-Key", apiKey);
        }
    }

    public async Task<List<Device>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching devices from Govee API...");
            var response = await _httpClient.GetAsync("user/devices", cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation("Received response from Govee API: {Response}", json);
            var data = JsonSerializer.Deserialize<DeviceResponse>(json);
            return data?.Data ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch devices from Govee API");
            // Return empty list if API is not available
            return [];
        }
    }

    public async Task<double> GetDeviceTemperatureAsync(Device device, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching temperature for device: {DeviceName} ({DeviceId})", device.DeviceName, device.DeviceId);
            var payload = new
            {
                requestId = Guid.NewGuid().ToString(),
                payload = new { sku = device.Sku, device = device.DeviceId }
            };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("device/state", content, cancellationToken);
            response.EnsureSuccessStatusCode();
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var stateResponse = JsonSerializer.Deserialize<DeviceStateResponse>(responseJson);
            
            return stateResponse?.Payload?.SensorTemperature 
                ?? throw new InvalidOperationException("Temperature capability not found in device state response.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get temperature for device {DeviceName}", device.DeviceName);
            // Return a default temperature if device is not available
            
            return 70.0; // Default room temperature
        }
    }

    public async Task<TemperatureStatus> GetTemperatureStatusAsync(CancellationToken cancellationToken = default)
    {
        // Try to get cached value
        if (_cache.TryGetValue(CacheKey, out TemperatureStatus? cachedValue))
        {
            _logger.LogInformation("Returning cached temperature status: {TemperatureData}", JsonSerializer.Serialize(cachedValue));
            return cachedValue!;
        }        // Cache miss, fetch new data
        _logger.LogInformation("Temperature cache miss, fetching fresh data...");
        var temperatureStatus = await GetTemperatureStatusInternalAsync(cancellationToken);

        // Set cache timestamp
        temperatureStatus.LastCachedAt = DateTime.Now;

        // Cache the result with absolute expiration
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheExpiration,
            Priority = CacheItemPriority.Normal
        };

        _cache.Set(CacheKey, temperatureStatus, cacheOptions);
        _logger.LogInformation("Temperature data cached for {Minutes} minutes", CacheExpiration.TotalMinutes);

        return temperatureStatus;
    }

    /// <summary>
    /// Clears the temperature cache
    /// </summary>
    public void ClearCache()
    {
        _cache.Remove(CacheKey);
        _logger.LogInformation("Temperature cache cleared");
    }

    /// <summary>
    /// Clears the cache and fetches fresh data
    /// </summary>
    public async Task<TemperatureStatus> RefreshAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Refreshing temperature data (clearing cache and fetching fresh data)");
        ClearCache();
        return await GetTemperatureStatusAsync(cancellationToken);
    }

    private async Task<TemperatureStatus> GetTemperatureStatusInternalAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting temperature status...");
        var threshold = _configuration.GetValue<double>("TemperatureThreshold", 2.0);
        var devices = await GetDevicesAsync(cancellationToken);
        
        var temperatureStatus = new TemperatureStatus
        {
            ReadingTakenAt = DateTime.Now,
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
        temperatureStatus.OutsideTemperature = await GetTemperatureForDevice(deviceMap["Outside"], cancellationToken);
        temperatureStatus.YoungerGirlsRoomTemperature = await GetTemperatureForDevice(deviceMap["Girl"], cancellationToken);
        temperatureStatus.OlderGirlsRoomTemperature = await GetTemperatureForDevice(deviceMap["Emma"], cancellationToken);
        temperatureStatus.BoysRoomTemperature = await GetTemperatureForDevice(deviceMap["Boy"], cancellationToken);
        temperatureStatus.MomAndDadsRoomTemperature = await GetTemperatureForDevice(deviceMap["Downstairs"], cancellationToken);
        temperatureStatus.GreenhouseTemperature = await GetTemperatureForDevice(deviceMap["Greenhouse"], cancellationToken);

        // Calculate temperature difference and determine if within threshold
        if (temperatureStatus.OutsideTemperature is { } outsideTemp && 
            temperatureStatus.YoungerGirlsRoomTemperature is { } girlsRoomTemp)
        {
            temperatureStatus.TemperatureDifference = Math.Abs(outsideTemp - girlsRoomTemp);
            temperatureStatus.IsWithinThreshold = temperatureStatus.TemperatureDifference <= threshold;
            temperatureStatus.ShouldWindowsBeClosed = outsideTemp >= girlsRoomTemp;
        }

        return temperatureStatus;
    }

    private async Task<double?> GetTemperatureForDevice(Device? device, CancellationToken cancellationToken)
        => device is not null 
            ? await GetDeviceTemperatureAsync(device, cancellationToken) 
            : null;
}
