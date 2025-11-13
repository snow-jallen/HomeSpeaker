using System;

namespace HomeSpeaker.Shared.Forecast;

#nullable enable

public sealed class ForecastStatus
{
    public ForecastData? TonightLow { get; set; }
    public ForecastData? TomorrowHigh { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public DateTime LastCachedAt { get; set; } = DateTime.UtcNow;
}
