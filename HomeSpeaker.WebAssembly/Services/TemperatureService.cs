using System.Text.Json;
using HomeSpeaker.Shared.Temperature;

namespace HomeSpeaker.WebAssembly.Services;

public sealed class TemperatureService : ITemperatureService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TemperatureService> _logger;

    public TemperatureService(HttpClient httpClient, ILogger<TemperatureService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<TemperatureStatus> GetTemperatureStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Fetching temperature status from server...");
            var response = await _httpClient.GetAsync("/api/temperature", cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var temperatureStatus = JsonSerializer.Deserialize<TemperatureStatus>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            return temperatureStatus ?? new TemperatureStatus();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch temperature status from server");
            // Return a default status if the server is not available
            return new TemperatureStatus
            {
                ReadingTakenAt = DateTime.Now,
                OutsideTemperature = null,
                YoungerGirlsRoomTemperature = null
            };
        }
    }
}
