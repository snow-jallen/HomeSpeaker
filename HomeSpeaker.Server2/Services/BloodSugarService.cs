using System.Text.Json;
using HomeSpeaker.Shared.BloodSugar;

namespace HomeSpeaker.Server2.Services;

public sealed class BloodSugarService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BloodSugarService> _logger;
    private readonly IConfiguration _configuration;

    public BloodSugarService(HttpClient httpClient, ILogger<BloodSugarService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<BloodSugarStatus> GetBloodSugarStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var nightscoutUrl = _configuration["NIGHTSCOUT_URL"];
            if (string.IsNullOrEmpty(nightscoutUrl))
            {
                _logger.LogWarning("NIGHTSCOUT_URL not configured");
                return new BloodSugarStatus
                {
                    LastUpdated = DateTime.Now,
                    IsStale = true,
                    CurrentReading = null
                };
            }

            // Get the latest entry from NightScout
            var apiUrl = $"{nightscoutUrl.TrimEnd('/')}/api/v1/entries.json?count=1";
            _logger.LogInformation("Fetching blood sugar data from: {ApiUrl}", apiUrl);
            
            var response = await _httpClient.GetAsync(apiUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var entries = JsonSerializer.Deserialize<BloodSugarReading[]>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (entries == null || entries.Length == 0)
            {
                _logger.LogWarning("No blood sugar entries found");
                return new BloodSugarStatus
                {
                    LastUpdated = DateTime.Now,
                    IsStale = true,
                    CurrentReading = null
                };
            }

            var latestReading = entries[0];
            var now = DateTime.UtcNow;
            var readingTime = latestReading.Date;
            var timeSinceReading = now - readingTime;
            
            // Consider data stale if it's more than 15 minutes old
            var isStale = timeSinceReading.TotalMinutes > 15;

            return new BloodSugarStatus
            {
                CurrentReading = latestReading,
                LastUpdated = now,
                IsStale = isStale,
                TimeSinceLastReading = timeSinceReading
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch blood sugar data from NightScout");
            return new BloodSugarStatus
            {
                LastUpdated = DateTime.Now,
                IsStale = true,
                CurrentReading = null
            };
        }
    }
}
