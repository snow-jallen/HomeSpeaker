using System;

namespace HomeSpeaker.Shared.Forecast;

#nullable enable

public sealed class ForecastStatus
{
    public ForecastData? TonightLow { get; set; }
    public ForecastData? TodayHigh { get; set; }
    public ForecastData? TomorrowHigh { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public DateTime LastCachedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Non-null when the forecast could not be fetched. The UI should show an
    /// "unavailable" state rather than presenting stale/placeholder numbers as real.
    /// </summary>
    public string? Error { get; set; }
}
