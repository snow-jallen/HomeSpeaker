using System.Text.Json;
using HomeSpeaker.Shared.BloodSugar;

namespace HomeSpeaker.WebAssembly.Services;

public sealed class BloodSugarService : IBloodSugarService
{
    private readonly HttpClient httpClient;
    private readonly ILogger<BloodSugarService> logger;

    public BloodSugarService(HttpClient httpClient, ILogger<BloodSugarService> logger)
    {
        this.httpClient = httpClient;
        this.logger = logger;
    }

    public async Task<BloodSugarStatus> GetBloodSugarStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Fetching blood sugar status from server...");
            var response = await httpClient.GetAsync("/api/bloodsugar", cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var bloodSugarStatus = JsonSerializer.Deserialize<BloodSugarStatus>(json, SerializationHelpers.PropertyNameCaseInsensitive);

            return bloodSugarStatus ?? new BloodSugarStatus();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch blood sugar status from server");
            // Return a default status if the server is not available
            return new BloodSugarStatus
            {
                LastUpdated = DateTime.UtcNow.ToLocalTime(),
                IsStale = true,
                CurrentReading = null
            };
        }
    }

    public async Task<bool> ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Clearing blood sugar cache on server...");
            var response = await httpClient.DeleteAsync("/api/bloodsugar/cache", cancellationToken);
            response.EnsureSuccessStatusCode();

            logger.LogInformation("Blood sugar cache cleared successfully");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clear blood sugar cache on server");
            return false;
        }
    }

    public async Task<BloodSugarStatus> RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Refreshing blood sugar data from server...");
            var response = await httpClient.PostAsync("/api/bloodsugar/refresh", null, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var bloodSugarStatus = JsonSerializer.Deserialize<BloodSugarStatus>(json, SerializationHelpers.PropertyNameCaseInsensitive);

            return bloodSugarStatus ?? new BloodSugarStatus();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh blood sugar data from server");
            // Return a default status if the server is not available
            return new BloodSugarStatus
            {
                LastUpdated = DateTime.UtcNow.ToLocalTime(),
                IsStale = true,
                CurrentReading = null
            };
        }
    }
}
