using System.Globalization;
using HomeSpeaker.Shared.Forecast;

namespace HomeSpeaker.Server2.Services.Dashboard;

/// <summary>One column of the Home "Next hours" strip.</summary>
public sealed record HourlyColumn(string Label, WeatherIcon Icon, bool IsDay, string Conditions, double? Temperature, int RainPercent)
{
    public const int DefaultCount = 8;

    /// <summary>The rain chance is only written out from this percent up; the bar shows every value.</summary>
    public const int RainLabelThreshold = 20;

    public bool ShowRainLabel => RainPercent >= RainLabelThreshold;

    /// <summary>
    /// The next <paramref name="count"/> hours starting with the current one (labelled "Now"), chosen against
    /// the current time in the forecast's own time zone so a long-cached forecast never shows hours already past.
    /// Fewer columns (or none) come back when the forecast has run out.
    /// </summary>
    public static IReadOnlyList<HourlyColumn> Select(ForecastStatus? forecast, DateTime utcNow, int count = DefaultCount)
    {
        if (forecast is not { Error: null } || forecast.Hourly.Count == 0)
        {
            return [];
        }

        var now = ForecastTime.LocalNow(forecast.TimeZoneId, utcNow);
        var thisHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Unspecified);

        return [.. forecast.Hourly
            .Where(h => h.DateTime >= thisHour)
            .OrderBy(h => h.DateTime)
            .Take(count)
            .Select(h => fromHour(h, h.DateTime == thisHour))];
    }

    /// <summary>"Now", then "3p", "12a" (12-hour like the clock, no minutes).</summary>
    public static string FormatLabel(DateTime hour, bool isNow)
    {
        if (isNow)
        {
            return "Now";
        }

        var twelve = hour.Hour % 12 == 0 ? 12 : hour.Hour % 12;
        return string.Create(CultureInfo.InvariantCulture, $"{twelve}{(hour.Hour < 12 ? 'a' : 'p')}");
    }

    private static HourlyColumn fromHour(ForecastData hour, bool isNow) => new(
        FormatLabel(hour.DateTime, isNow),
        WeatherIconMap.From(hour.WeatherCode),
        hour.IsDay ?? true,
        hour.Conditions ?? "",
        hour.Temperature,
        (int)Math.Clamp(Math.Round(hour.PrecipitationChance ?? 0), 0, 100));
}
