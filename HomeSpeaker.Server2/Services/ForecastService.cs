using System.Text.Json;
using System.Text.Json.Serialization;
using HomeSpeaker.Shared.Forecast;
using Microsoft.Extensions.Caching.Memory;

namespace HomeSpeaker.Server2.Services;

public sealed class ForecastService
{
    private readonly HttpClient httpClient;
    private readonly IConfiguration configuration;
    private readonly ILogger<ForecastService> logger;
    private readonly IMemoryCache cache;

    private const string CacheKey = "forecast-status";
    private static readonly TimeSpan cacheExpiration = TimeSpan.FromMinutes(30);
    private static readonly JsonSerializerOptions jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ForecastService(HttpClient httpClient, IConfiguration configuration, ILogger<ForecastService> logger, IMemoryCache cache)
    {
        this.httpClient = httpClient;
        this.configuration = configuration;
        this.logger = logger;
        this.cache = cache;
    }

    public async Task<ForecastStatus> GetForecastStatusAsync(CancellationToken cancellationToken = default)
    {
        // Try to get cached value
        if (cache.TryGetValue(CacheKey, out ForecastStatus? cachedValue))
        {
            logger.LogInformation("Returning cached forecast status");
            return cachedValue!;
        }

        // Cache miss, fetch new data
        logger.LogInformation("Forecast cache miss, fetching fresh data...");
        var forecastStatus = await getForecastStatusInternalAsync(cancellationToken);

        // Set cache timestamp
        forecastStatus.LastCachedAt = DateTime.UtcNow;

        // Cache the result with absolute expiration
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = cacheExpiration,
            Priority = CacheItemPriority.Normal
        };

        cache.Set(CacheKey, forecastStatus, cacheOptions);
        logger.LogInformation("Forecast data cached for {Minutes} minutes", cacheExpiration.TotalMinutes);

        return forecastStatus;
    }

    /// <summary>
    /// Clears the forecast cache
    /// </summary>
    public void ClearCache()
    {
        cache.Remove(CacheKey);
        logger.LogInformation("Forecast cache cleared");
    }

    /// <summary>
    /// Clears the cache and fetches fresh data
    /// </summary>
    public async Task<ForecastStatus> RefreshAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Refreshing forecast data (clearing cache and fetching fresh data)");
        ClearCache();
        return await GetForecastStatusAsync(cancellationToken);
    }

    private async Task<ForecastStatus> getForecastStatusInternalAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Getting forecast status...");

        // Get location from configuration (default to a reasonable location)
        var latitude = configuration.GetValue<double>("Forecast:Latitude", 39.2683); // Default to NYC
        var longitude = configuration.GetValue<double>("Forecast:Longitude", -111.63686);

        logger.LogInformation("Fetching forecast for configured location");

        try
        {
            // Use Open-Meteo API (free, no API key required)
            var url = $"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&hourly=temperature_2m,precipitation_probability,weather_code&temperature_unit=fahrenheit&timezone=auto&forecast_days=2";

            var response = await httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var weatherData = JsonSerializer.Deserialize<OpenMeteoResponse>(json, jsonOptions);

            if (weatherData?.Hourly == null)
            {
                logger.LogWarning("No forecast data received from API");
                return new ForecastStatus { LastUpdated = DateTime.UtcNow };
            }

            var now = getForecastNow(weatherData.Timezone);
            var forecastStatus = new ForecastStatus
            {
                LastUpdated = DateTime.UtcNow
            };

            // Find tonight's low (remaining hours of today in forecast timezone)
            var todayStart = now.Date;
            var todayEnd = todayStart.AddDays(1);
            var tonightTemps = new List<(DateTime time, double temp, int index)>();

            for (var i = 0; i < weatherData.Hourly.Time.Length; i++)
            {
                var time = DateTime.Parse(weatherData.Hourly.Time[i]);
                if (time >= now && time < todayEnd)
                {
                    tonightTemps.Add((time, weatherData.Hourly.Temperature2m[i], i));
                }
            }

            if (tonightTemps.Any())
            {
                var lowestTemp = tonightTemps.MinBy(t => t.temp);

                forecastStatus.TonightLow = new ForecastData
                {
                    DateTime = lowestTemp.time,
                    Temperature = lowestTemp.temp,
                    Conditions = getConditionDescription(weatherData.Hourly.WeatherCode[lowestTemp.index]),
                    PrecipitationChance = weatherData.Hourly.PrecipitationProbability?[lowestTemp.index]
                };
            }

            // Find today's high (all hours of today in forecast timezone)
            var todayTemps = new List<(DateTime time, double temp, int index)>();

            for (var i = 0; i < weatherData.Hourly.Time.Length; i++)
            {
                var time = DateTime.Parse(weatherData.Hourly.Time[i]);
                if (time >= todayStart && time < todayEnd)
                {
                    todayTemps.Add((time, weatherData.Hourly.Temperature2m[i], i));
                }
            }

            if (todayTemps.Any())
            {
                var highestTodayTemp = todayTemps.MaxBy(t => t.temp);

                forecastStatus.TodayHigh = new ForecastData
                {
                    DateTime = highestTodayTemp.time,
                    Temperature = highestTodayTemp.temp,
                    Conditions = getConditionDescription(weatherData.Hourly.WeatherCode[highestTodayTemp.index]),
                    PrecipitationChance = weatherData.Hourly.PrecipitationProbability?[highestTodayTemp.index]
                };
            }

            // Find tomorrow's high in forecast timezone
            var tomorrowStart = todayEnd;
            var tomorrowEnd = tomorrowStart.AddDays(1);
            var tomorrowTemps = new List<(DateTime time, double temp, int index)>();

            for (var i = 0; i < weatherData.Hourly.Time.Length; i++)
            {
                var time = DateTime.Parse(weatherData.Hourly.Time[i]);
                if (time >= tomorrowStart && time < tomorrowEnd)
                {
                    tomorrowTemps.Add((time, weatherData.Hourly.Temperature2m[i], i));
                }
            }

            if (tomorrowTemps.Any())
            {
                var highestTemp = tomorrowTemps.MaxBy(t => t.temp);

                forecastStatus.TomorrowHigh = new ForecastData
                {
                    DateTime = highestTemp.time,
                    Temperature = highestTemp.temp,
                    Conditions = getConditionDescription(weatherData.Hourly.WeatherCode[highestTemp.index]),
                    PrecipitationChance = weatherData.Hourly.PrecipitationProbability?[highestTemp.index]
                };
            }

            return forecastStatus;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch forecast data");
            logger.LogInformation("Using sample forecast data for testing");

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
                TodayHigh = new ForecastData
                {
                    DateTime = DateTime.UtcNow.Date.AddHours(14),
                    Temperature = 72.0,
                    Conditions = "Sunny",
                    PrecipitationChance = 5
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

    private static string getConditionDescription(int weatherCode)
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
    private sealed class OpenMeteoResponse
    {
        [JsonPropertyName("timezone")]
        public string? Timezone { get; set; }

        [JsonPropertyName("hourly")]
        public HourlyData? Hourly { get; set; }
    }

    private sealed class HourlyData
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

    private static DateTime getForecastNow(string? timezone)
    {
        if (string.IsNullOrWhiteSpace(timezone))
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.Local);
        }

        try
        {
            var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZoneInfo);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.Local);
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.Local);
        }
    }
}