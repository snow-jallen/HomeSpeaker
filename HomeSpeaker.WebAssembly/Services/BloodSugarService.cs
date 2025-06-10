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
}
