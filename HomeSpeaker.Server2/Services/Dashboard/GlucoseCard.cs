using System.Globalization;
using HomeSpeaker.Shared.BloodSugar;

namespace HomeSpeaker.Server2.Services.Dashboard;

/// <summary>What the Home glucose card shows, worked out from the cached status and the current time.</summary>
public sealed record GlucoseCard(
    string Value,
    string? Arrow,
    string? DeltaText,
    bool IsStale,
    string AgeText,
    string StatusText,
    GlucoseChart? Chart)
{
    /// <summary>A reading older than this is stale: dimmed, and its delta no longer means anything.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(15);

    /// <summary>Null when there is no reading at all.</summary>
    public static GlucoseCard? From(BloodSugarStatus? status, DateTime nowUtc)
    {
        if (status?.CurrentReading is not { } reading)
        {
            return null;
        }

        // Age is measured now, not at fetch time, because the status can be cached for a couple of minutes.
        var age = nowUtc - reading.Date.ToUniversalTime();
        if (age < TimeSpan.Zero)
        {
            age = TimeSpan.Zero;
        }

        var stale = age > StaleAfter;
        var arrow = string.Equals(reading.DirectionIcon, "?", StringComparison.Ordinal) ? null : reading.DirectionIcon;

        return new GlucoseCard(
            reading.Sgv.ToString("0", CultureInfo.InvariantCulture),
            arrow,
            stale ? null : FormatDelta(status.Delta),
            stale,
            FormatAge(age, stale),
            status.StatusText,
            GlucoseChart.Build(status.RecentReadings, nowUtc));
    }

    /// <summary>Signed with a real minus sign, e.g. "+6", "−3", "±0"; null when there is no delta.</summary>
    public static string? FormatDelta(double? delta)
    {
        if (delta is not { } value)
        {
            return null;
        }

        var rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return rounded switch
        {
            > 0 => string.Create(CultureInfo.InvariantCulture, $"+{rounded}"),
            < 0 => string.Create(CultureInfo.InvariantCulture, $"−{-rounded}"),
            _ => "±0",
        };
    }

    /// <summary>"just now", "3 min ago", or when stale "Stale · 42 min ago" / "Stale · 1 h 5 min ago".</summary>
    public static string FormatAge(TimeSpan age, bool stale)
    {
        var minutes = (int)age.TotalMinutes;
        var ago = minutes < 1
            ? "just now"
            : minutes < 60
                ? string.Create(CultureInfo.InvariantCulture, $"{minutes} min ago")
                : string.Create(CultureInfo.InvariantCulture, $"{minutes / 60} h {minutes % 60} min ago");

        return stale ? "Stale · " + ago : ago;
    }
}
