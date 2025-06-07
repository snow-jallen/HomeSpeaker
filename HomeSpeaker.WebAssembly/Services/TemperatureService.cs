using System.Text;
using System.Text.Json;
using HomeSpeaker.WebAssembly.Models.Temperature;
using Microsoft.Extensions.Configuration;

namespace HomeSpeaker.WebAssembly.Services;

public sealed class TemperatureService : ITemperatureService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public TemperatureService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        
        // Configure the HttpClient for Govee API
        var apiBaseUrl = _configuration["Temperature:ApiBaseUrl"];
        var apiKey = _configuration["Temperature:ApiKey"];
        
        if (!string.IsNullOrEmpty(apiBaseUrl))
        {
            _httpClient.BaseAddress = new Uri(apiBaseUrl);
        }
        
        if (!string.IsNullOrEmpty(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Govee-API-Key", apiKey);
        }
    }

    public async Task<List<Device>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/user/devices", cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var data = JsonSerializer.Deserialize<DeviceResponse>(json);
            return data?.Data ?? [];
        }
        catch (Exception)
        {
            // Return empty list if API is not available
            return [];
        }
    }

    public async Task<double> GetDeviceTemperatureAsync(Device device, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                requestId = Guid.NewGuid().ToString(),
                payload = new { sku = device.Sku, device = device.DeviceId }
            };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/device/state", content, cancellationToken);
            response.EnsureSuccessStatusCode();
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var stateResponse = JsonSerializer.Deserialize<DeviceStateResponse>(responseJson);
            
            return stateResponse?.Payload?.SensorTemperature 
                ?? throw new InvalidOperationException("Temperature capability not found in device state response.");
        }
        catch (Exception)
        {
            // Return a default temperature if device is not available
            return 70.0; // Default room temperature
        }
    }

    public async Task<TemperatureStatus> GetTemperatureStatusAsync(CancellationToken cancellationToken = default)
    {
        var threshold = _configuration.GetValue<double>("Temperature:TemperatureThreshold", 2.0);
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
