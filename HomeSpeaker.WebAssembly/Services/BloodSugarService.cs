using System.Text.Json;
using HomeSpeaker.Shared.BloodSugar;

namespace HomeSpeaker.WebAssembly.Services;

public sealed class BloodSugarService : IBloodSugarService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BloodSugarService> _logger;

    public BloodSugarService(HttpClient httpClient, ILogger<BloodSugarService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<BloodSugarStatus> GetBloodSugarStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching blood sugar status from server...");
            var response = await _httpClient.GetAsync("/api/bloodsugar", cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var bloodSugarStatus = JsonSerializer.Deserialize<BloodSugarStatus>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            return bloodSugarStatus ?? new BloodSugarStatus();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch blood sugar status from server");
            // Return a default status if the server is not available
            return new BloodSugarStatus
            {
                LastUpdated = DateTime.Now,
                IsStale = true,
                CurrentReading = null
            };
        }
    }

    public async Task<bool> ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Clearing blood sugar cache on server...");
            var response = await _httpClient.DeleteAsync("/api/bloodsugar/cache", cancellationToken);
            response.EnsureSuccessStatusCode();
            
            _logger.LogInformation("Blood sugar cache cleared successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear blood sugar cache on server");
            return false;
        }
    }

    public async Task<BloodSugarStatus> RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Refreshing blood sugar data from server...");
            var response = await _httpClient.PostAsync("/api/bloodsugar/refresh", null, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var bloodSugarStatus = JsonSerializer.Deserialize<BloodSugarStatus>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            return bloodSugarStatus ?? new BloodSugarStatus();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh blood sugar data from server");
            // Return a default status if the server is not available
            return new BloodSugarStatus
            {
                LastUpdated = DateTime.Now,
                IsStale = true,
                CurrentReading = null
            };
        }
    }
}
