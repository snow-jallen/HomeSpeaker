using System.Text.Json;
using HomeSpeaker.Shared.Temperature;

namespace HomeSpeaker.WebAssembly.Services;

public sealed class TemperatureService : ITemperatureService
{
    private readonly HttpClient httpClient;
    private readonly ILogger<TemperatureService> logger;

    public TemperatureService(HttpClient httpClient, ILogger<TemperatureService> logger)
    {
        this.httpClient = httpClient;
        this.logger = logger;
    }

    private readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<TemperatureStatus> GetTemperatureStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Fetching temperature status from server...");
            var response = await httpClient.GetAsync("/api/temperature", cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var temperatureStatus = JsonSerializer.Deserialize<TemperatureStatus>(json, jsonOptions);

            return temperatureStatus ?? new TemperatureStatus();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch temperature status from server");
            // Return a default status if the server is not available
            return new TemperatureStatus
            {
                ReadingTakenAt = DateTime.UtcNow.ToLocalTime(),
                LastCachedAt = DateTime.UtcNow.ToLocalTime(),
                OutsideTemperature = null,
                YoungerGirlsRoomTemperature = null
            };
        }
    }

    public async Task<bool> ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Clearing temperature cache on server...");
            var response = await httpClient.DeleteAsync("/api/temperature/cache", cancellationToken);
            response.EnsureSuccessStatusCode();

            logger.LogInformation("Temperature cache cleared successfully");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clear temperature cache on server");
            return false;
        }
    }

    public async Task<TemperatureStatus> RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Refreshing temperature data from server...");
            var response = await httpClient.PostAsync("/api/temperature/refresh", null, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var temperatureStatus = JsonSerializer.Deserialize<TemperatureStatus>(json, jsonOptions);

            return temperatureStatus ?? new TemperatureStatus();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh temperature data from server");
            // Return a default status if the server is not available
            return new TemperatureStatus
            {
                ReadingTakenAt = DateTime.UtcNow.ToLocalTime(),
                LastCachedAt = DateTime.UtcNow.ToLocalTime(),
                OutsideTemperature = null,
                YoungerGirlsRoomTemperature = null
            };
        }
    }
}
