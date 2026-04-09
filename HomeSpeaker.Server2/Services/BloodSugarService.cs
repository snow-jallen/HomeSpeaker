using System.Text.Json;
using HomeSpeaker.Shared.BloodSugar;
using Microsoft.Extensions.Caching.Memory;

namespace HomeSpeaker.Server2.Services;

public sealed class BloodSugarService
{
    private readonly HttpClient httpClient;
    private readonly ILogger<BloodSugarService> logger;
    private readonly IConfiguration configuration;
    private readonly IMemoryCache cache;

    private const string CacheKey = "blood-sugar-status";
    private static readonly JsonSerializerOptions jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public BloodSugarService(HttpClient httpClient, ILogger<BloodSugarService> logger, IConfiguration configuration, IMemoryCache cache)
    {
        this.httpClient = httpClient;
        this.logger = logger;
        this.configuration = configuration;
        this.cache = cache;
    }

    public async Task<BloodSugarStatus> GetBloodSugarStatusAsync(CancellationToken cancellationToken = default)
    {
        // Try to get cached value and check if it needs refresh based on smart logic
        if (cache.TryGetValue(CacheKey, out BloodSugarStatus? cachedValue) && cachedValue != null)
        {
            var shouldRefresh = shouldRefreshBloodSugarCache(cachedValue);
            if (!shouldRefresh)
            {
                logger.LogInformation("Returning cached blood sugar status {CachedValue}", JsonSerializer.Serialize(cachedValue));
                return cachedValue;
            }
        }

        // Cache miss or needs refresh, fetch new data
        logger.LogInformation("Blood sugar cache refresh needed, fetching fresh data...");
        var bloodSugarStatus = await getBloodSugarStatusInternalAsync(cancellationToken);

        // Cache with smart expiration based on reading age
        var cacheExpiration = calculateCacheExpiration(bloodSugarStatus);
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = cacheExpiration,
            Priority = CacheItemPriority.High
        };

        cache.Set(CacheKey, bloodSugarStatus, cacheOptions);
        logger.LogInformation("Blood sugar data cached for {Seconds} seconds {CachedValue}", cacheExpiration.TotalSeconds, JsonSerializer.Serialize(bloodSugarStatus));

        return bloodSugarStatus;
    }

    private bool shouldRefreshBloodSugarCache(BloodSugarStatus cachedStatus)
    {
        // If no current reading, refresh more frequently
        if (cachedStatus.CurrentReading == null)
        {
            return true; // Always refresh when no data
        }

        var readingAge = DateTime.UtcNow - cachedStatus.CurrentReading.Date;
        var cacheAge = DateTime.UtcNow - cachedStatus.LastUpdated;

        // If reading is very fresh (< 2 minutes), can cache longer
        if (readingAge.TotalMinutes < 2)
        {
            return cacheAge.TotalMinutes >= 2;
        }

        // If reading is getting older (2-4 minutes), refresh more often
        if (readingAge.TotalMinutes < 4)
        {
            return cacheAge.TotalMinutes >= 1;
        }

        // If reading is 4+ minutes old, refresh frequently (next update expected soon)
        return cacheAge.TotalSeconds >= 30;
    }

    private TimeSpan calculateCacheExpiration(BloodSugarStatus status)
    {
        // If no reading, cache for 1 minute
        if (status.CurrentReading == null)
        {
            return TimeSpan.FromMinutes(1);
        }

        var readingAge = DateTime.UtcNow - status.CurrentReading.Date;

        // Fresh reading (< 2 min): cache for 2 minutes
        if (readingAge.TotalMinutes < 2)
        {
            return TimeSpan.FromMinutes(2);
        }

        // Getting close to next update (2-4 min): cache for 1 minute
        if (readingAge.TotalMinutes < 4)
        {
            return TimeSpan.FromMinutes(1);
        }

        // Very close to next update (4+ min): cache for 30 seconds
        return TimeSpan.FromSeconds(30);
    }

    private async Task<BloodSugarStatus> getBloodSugarStatusInternalAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var nightscoutUrl = configuration["NIGHTSCOUT_URL"];
            if (string.IsNullOrEmpty(nightscoutUrl))
            {
                logger.LogWarning("NIGHTSCOUT_URL not configured");
                return new BloodSugarStatus
                {
                    LastUpdated = DateTime.UtcNow.ToLocalTime(),
                    IsStale = true,
                    CurrentReading = null
                };
            }

            // Get the latest entry from NightScout
            var apiUrl = $"{nightscoutUrl.TrimEnd('/')}/api/v1/entries.json?count=1";
            logger.LogInformation("Fetching blood sugar data from: {ApiUrl}", apiUrl);

            var response = await httpClient.GetAsync(apiUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var entries = JsonSerializer.Deserialize<BloodSugarReading[]>(json, jsonOptions);

            if (entries == null || entries.Length == 0)
            {
                logger.LogWarning("No blood sugar entries found");
                return new BloodSugarStatus
                {
                    LastUpdated = DateTime.UtcNow.ToLocalTime(),
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
            logger.LogError(ex, "Failed to fetch blood sugar data from NightScout");
            return new BloodSugarStatus
            {
                LastUpdated = DateTime.UtcNow.ToLocalTime(),
                IsStale = true,
                CurrentReading = null
            };
        }
    }

    /// <summary>
    /// Clears the blood sugar cache
    /// </summary>
    public void ClearCache()
    {
        cache.Remove(CacheKey);
        logger.LogInformation("Blood sugar cache cleared");
    }

    /// <summary>
    /// Clears the cache and fetches fresh data
    /// </summary>
    public async Task<BloodSugarStatus> RefreshAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Refreshing blood sugar data (clearing cache and fetching fresh data)");
        ClearCache();
        return await GetBloodSugarStatusAsync(cancellationToken);
    }
}