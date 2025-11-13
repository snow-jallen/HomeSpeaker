using System.Text.Json;
using HomeSpeaker.Shared.Forecast;

namespace HomeSpeaker.WebAssembly.Services;

public sealed class ForecastService : IForecastService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ForecastService> _logger;

    public ForecastService(HttpClient httpClient, ILogger<ForecastService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ForecastStatus> GetForecastStatusAsync()
    {
        try
        {
            _logger.LogInformation("Fetching forecast status from server...");
            var response = await _httpClient.GetAsync("/api/forecast");
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            var forecastStatus = JsonSerializer.Deserialize<ForecastStatus>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            return forecastStatus ?? new ForecastStatus();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch forecast status from server");
            // Return a default status if the server is not available
            return new ForecastStatus
            {
                LastUpdated = DateTime.UtcNow,
                LastCachedAt = DateTime.UtcNow
            };
        }
    }

    public async Task<bool> ClearCacheAsync()
    {
        try
        {
            _logger.LogInformation("Clearing forecast cache on server...");
            var response = await _httpClient.DeleteAsync("/api/forecast/cache");
            response.EnsureSuccessStatusCode();
            
            _logger.LogInformation("Forecast cache cleared successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear forecast cache on server");
            return false;
        }
    }

    public async Task<ForecastStatus> RefreshAsync()
    {
        try
        {
            _logger.LogInformation("Refreshing forecast data from server...");
            var response = await _httpClient.PostAsync("/api/forecast/refresh", null);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync();
            var forecastStatus = JsonSerializer.Deserialize<ForecastStatus>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            return forecastStatus ?? new ForecastStatus();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh forecast data from server");
            // Return a default status if the server is not available
            return new ForecastStatus
            {
                LastUpdated = DateTime.UtcNow,
                LastCachedAt = DateTime.UtcNow
            };
        }
    }
}
