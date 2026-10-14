using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using HomeSpeaker.Shared.Forecast;
using Microsoft.Extensions.Caching.Memory;

namespace HomeSpeaker.Server2.Services;

public sealed class ForecastService : IForecastService
{
    private readonly HttpClient httpClient;
    private readonly IConfiguration configuration;
    private readonly ILogger<ForecastService> logger;
    private readonly IMemoryCache cache;
    private readonly TimeProvider timeProvider;

    private const string CacheKey = "forecast-status";
    private static readonly TimeSpan cacheExpiration = TimeSpan.FromMinutes(30);
    private static readonly JsonSerializerOptions jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ForecastService(HttpClient httpClient, IConfiguration configuration, ILogger<ForecastService> logger, IMemoryCache cache, TimeProvider timeProvider)
    {
        this.httpClient = httpClient;
        this.configuration = configuration;
        this.logger = logger;
        this.cache = cache;
        this.timeProvider = timeProvider;
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

        ForecastStatus forecastStatus;
        try
        {
            forecastStatus = await getForecastStatusInternalAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Surface the failure honestly instead of caching placeholder data.
            // Not cached, so the next request retries rather than showing a stale error for 30 minutes.
            logger.LogError(ex, "Failed to fetch forecast data");
            return new ForecastStatus
            {
                LastUpdated = timeProvider.GetUtcNow().UtcDateTime,
                Error = ex.Message
            };
        }

        // Set cache timestamp
        forecastStatus.LastCachedAt = timeProvider.GetUtcNow().UtcDateTime;

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

    public Task<bool> ClearCacheAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            ClearCache();
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clear forecast cache");
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Clears the cache and fetches fresh data
    /// </summary>
    public async Task<ForecastStatus> RefreshAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Refreshing forecast data (clearing cache and fetching fresh data)");
        await ClearCacheAsync(cancellationToken);
        return await GetForecastStatusAsync(cancellationToken);
    }

    private async Task<ForecastStatus> getForecastStatusInternalAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Getting forecast status...");

        // Get location from configuration (default to a reasonable location)
        var latitude = configuration.GetValue<double>("Forecast:Latitude", 39.2683); // Default to NYC
        var longitude = configuration.GetValue<double>("Forecast:Longitude", -111.63686);

        logger.LogInformation("Fetching forecast for configured location");

        // Use Open-Meteo API (free, no API key required). Exceptions propagate to
        // GetForecastStatusAsync, which reports the failure rather than caching placeholder data.
        var url = $"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&hourly=temperature_2m,precipitation_probability,weather_code,is_day&temperature_unit=fahrenheit&timezone=auto&forecast_days=2";

        var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var weatherData = JsonSerializer.Deserialize<OpenMeteoResponse>(json, jsonOptions);

        if (weatherData?.Hourly == null)
        {
            logger.LogWarning("No forecast data received from API");
            return new ForecastStatus { LastUpdated = timeProvider.GetUtcNow().UtcDateTime };
        }

        var now = getForecastNow(weatherData.Timezone);
        var forecastStatus = new ForecastStatus
        {
            LastUpdated = timeProvider.GetUtcNow().UtcDateTime,
            TimeZoneId = weatherData.Timezone,
            Hourly = buildHourly(weatherData.Hourly)
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

    private static List<ForecastData> buildHourly(HourlyData hourly)
    {
        var count = new[] { hourly.Time.Length, hourly.Temperature2m.Length, hourly.WeatherCode.Length }.Min();
        var entries = new List<ForecastData>(count);
        for (var i = 0; i < count; i++)
        {
            entries.Add(new ForecastData
            {
                DateTime = DateTime.Parse(hourly.Time[i], CultureInfo.InvariantCulture),
                Temperature = hourly.Temperature2m[i],
                Conditions = getConditionDescription(hourly.WeatherCode[i]),
                PrecipitationChance = hourly.PrecipitationProbability is { } rain && i < rain.Length ? rain[i] : null,
                WeatherCode = hourly.WeatherCode[i],
                IsDay = hourly.IsDay is { } day && i < day.Length ? day[i] != 0 : null,
            });
        }

        return entries;
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

        [JsonPropertyName("is_day")]
        public int[]? IsDay { get; set; }
    }

    private DateTime getForecastNow(string? timezone) =>
        ForecastTime.LocalNow(timezone, timeProvider.GetUtcNow().UtcDateTime);
}
