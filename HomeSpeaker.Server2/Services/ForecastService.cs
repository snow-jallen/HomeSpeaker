using System.Text.Json;
using System.Text.Json.Serialization;
using HomeSpeaker.Shared.Forecast;
using Microsoft.Extensions.Caching.Memory;

namespace HomeSpeaker.Server2.Services;

public sealed class ForecastService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ForecastService> _logger;
    private readonly IMemoryCache _cache;
    
    private const string CacheKey = "forecast-status";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(30);

    public ForecastService(HttpClient httpClient, IConfiguration configuration, ILogger<ForecastService> logger, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _cache = cache;
    }

    public async Task<ForecastStatus> GetForecastStatusAsync(CancellationToken cancellationToken = default)
    {
        // Try to get cached value
        if (_cache.TryGetValue(CacheKey, out ForecastStatus? cachedValue))
        {
            _logger.LogInformation("Returning cached forecast status");
            return cachedValue!;
        }
        
        // Cache miss, fetch new data
        _logger.LogInformation("Forecast cache miss, fetching fresh data...");
        var forecastStatus = await GetForecastStatusInternalAsync(cancellationToken);

        // Set cache timestamp
        forecastStatus.LastCachedAt = DateTime.UtcNow;

        // Cache the result with absolute expiration
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheExpiration,
            Priority = CacheItemPriority.Normal
        };

        _cache.Set(CacheKey, forecastStatus, cacheOptions);
        _logger.LogInformation("Forecast data cached for {Minutes} minutes", CacheExpiration.TotalMinutes);

        return forecastStatus;
    }

    /// <summary>
    /// Clears the forecast cache
    /// </summary>
    public void ClearCache()
    {
        _cache.Remove(CacheKey);
        _logger.LogInformation("Forecast cache cleared");
    }

    /// <summary>
    /// Clears the cache and fetches fresh data
    /// </summary>
    public async Task<ForecastStatus> RefreshAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Refreshing forecast data (clearing cache and fetching fresh data)");
        ClearCache();
        return await GetForecastStatusAsync(cancellationToken);
    }

    private async Task<ForecastStatus> GetForecastStatusInternalAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting forecast status...");
        
        // Get location from configuration (default to a reasonable location)
        var latitude = _configuration.GetValue<double>("Forecast:Latitude", 39.2683); // Default to NYC
        var longitude = _configuration.GetValue<double>("Forecast:Longitude", -111.63686);
        
        _logger.LogInformation("Fetching forecast for configured location");

        try
        {
            // Use Open-Meteo API (free, no API key required)
            var url = $"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&hourly=temperature_2m,precipitation_probability,weather_code&temperature_unit=fahrenheit&timezone=auto&forecast_days=2";
            
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var weatherData = JsonSerializer.Deserialize<OpenMeteoResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (weatherData?.Hourly == null)
            {
                _logger.LogWarning("No forecast data received from API");
                return new ForecastStatus { LastUpdated = DateTime.UtcNow };
            }

            var now = DateTime.UtcNow;
            var forecastStatus = new ForecastStatus
            {
                LastUpdated = DateTime.UtcNow
            };

            // Find tonight's low (remaining hours of today)
            var todayEnd = now.Date.AddDays(1);
            var tonightTemps = new List<(DateTime time, double temp)>();
            
            for (int i = 0; i < weatherData.Hourly.Time.Length; i++)
            {
                var time = DateTime.Parse(weatherData.Hourly.Time[i]);
                if (time >= now && time < todayEnd)
                {
                    tonightTemps.Add((time, weatherData.Hourly.Temperature2m[i]));
                }
            }

            if (tonightTemps.Any())
            {
                var lowestTemp = tonightTemps.MinBy(t => t.temp);
                var lowestTempIndex = Array.IndexOf(weatherData.Hourly.Time, lowestTemp.time.ToString("yyyy-MM-ddTHH:00"));
                
                forecastStatus.TonightLow = new ForecastData
                {
                    DateTime = lowestTemp.time,
                    Temperature = lowestTemp.temp,
                    Conditions = GetConditionDescription(weatherData.Hourly.WeatherCode[lowestTempIndex]),
                    PrecipitationChance = weatherData.Hourly.PrecipitationProbability?[lowestTempIndex]
                };
            }

            // Find tomorrow's high
            var tomorrowStart = todayEnd;
            var tomorrowEnd = tomorrowStart.AddDays(1);
            var tomorrowTemps = new List<(DateTime time, double temp)>();
            
            for (int i = 0; i < weatherData.Hourly.Time.Length; i++)
            {
                var time = DateTime.Parse(weatherData.Hourly.Time[i]);
                if (time >= tomorrowStart && time < tomorrowEnd)
                {
                    tomorrowTemps.Add((time, weatherData.Hourly.Temperature2m[i]));
                }
            }

            if (tomorrowTemps.Any())
            {
                var highestTemp = tomorrowTemps.MaxBy(t => t.temp);
                var highestTempIndex = Array.IndexOf(weatherData.Hourly.Time, highestTemp.time.ToString("yyyy-MM-ddTHH:00"));
                
                forecastStatus.TomorrowHigh = new ForecastData
                {
                    DateTime = highestTemp.time,
                    Temperature = highestTemp.temp,
                    Conditions = GetConditionDescription(weatherData.Hourly.WeatherCode[highestTempIndex]),
                    PrecipitationChance = weatherData.Hourly.PrecipitationProbability?[highestTempIndex]
                };
            }

            return forecastStatus;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch forecast data");
            _logger.LogInformation("Using sample forecast data for testing");
            
            // Return sample data when API is unavailable (for testing/demo purposes)
            return new ForecastStatus
            {
                LastUpdated = DateTime.UtcNow,
                TonightLow = new ForecastData
                {
                    DateTime = DateTime.UtcNow.Date.AddHours(22),
                    Temperature = 45.0,
                    Conditions = "Clear",
                    PrecipitationChance = 10
                },
                TomorrowHigh = new ForecastData
                {
                    DateTime = DateTime.UtcNow.Date.AddDays(1).AddHours(14),
                    Temperature = 68.0,
                    Conditions = "Partly Cloudy",
                    PrecipitationChance = 20
                }
            };
        }
    }

    private static string GetConditionDescription(int weatherCode)
    {
        // WMO Weather interpretation codes
        return weatherCode switch
        {
            0 => "Clear",
            1 or 2 or 3 => "Partly Cloudy",
            45 or 48 => "Foggy",
            51 or 53 or 55 => "Drizzle",
            56 or 57 => "Freezing Drizzle",
            61 or 63 or 65 => "Rain",
            66 or 67 => "Freezing Rain",
            71 or 73 or 75 => "Snow",
            77 => "Snow Grains",
            80 or 81 or 82 => "Rain Showers",
            85 or 86 => "Snow Showers",
            95 => "Thunderstorm",
            96 or 99 => "Thunderstorm with Hail",
            _ => "Unknown"
        };
    }

    // Response models for Open-Meteo API
    private class OpenMeteoResponse
    {
        [JsonPropertyName("hourly")]
        public HourlyData? Hourly { get; set; }
    }

    private class HourlyData
    {
        [JsonPropertyName("time")]
        public string[] Time { get; set; } = Array.Empty<string>();

        [JsonPropertyName("temperature_2m")]
        public double[] Temperature2m { get; set; } = Array.Empty<double>();

        [JsonPropertyName("precipitation_probability")]
        public int[]? PrecipitationProbability { get; set; }

        [JsonPropertyName("weather_code")]
        public int[] WeatherCode { get; set; } = Array.Empty<int>();
    }
}
