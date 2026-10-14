namespace HomeSpeaker.Shared.Forecast;

#nullable enable

public sealed class ForecastStatus
{
    public ForecastData? TonightLow { get; set; }
    public ForecastData? TodayHigh { get; set; }
    public ForecastData? TomorrowHigh { get; set; }
    /// <summary>
    /// Every forecast hour (about 48), oldest first. Each <see cref="ForecastData.DateTime"/> is local time in the
    /// forecast's own time zone (<see cref="TimeZoneId"/>), not UTC. The status can be cached for a while, so
    /// pick the upcoming hours from this at display time rather than trusting its start.
    /// </summary>
    public List<ForecastData> Hourly { get; set; } = [];

    /// <summary>The IANA time zone the hourly times are in, e.g. America/Denver; null when unknown.</summary>
    public string? TimeZoneId { get; set; }

    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public DateTime LastCachedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Non-null when the forecast could not be fetched. The UI should show an
    /// "unavailable" state rather than presenting stale/placeholder numbers as real.
    /// </summary>
    public string? Error { get; set; }
}
