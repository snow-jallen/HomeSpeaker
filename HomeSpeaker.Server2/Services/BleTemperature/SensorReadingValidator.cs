using System.Text.RegularExpressions;
using HomeSpeaker.Shared.Temperature;

namespace HomeSpeaker.Server2.Services.BleTemperature;

/// <summary>Checks a reading posted to the hub before it is kept.</summary>
public static partial class SensorReadingValidator
{
    private const int MaxTextLength = 64;
    private static readonly TimeSpan clockSkew = TimeSpan.FromMinutes(2);

    /// <summary>Why the reading is rejected, or null when it is fine.</summary>
    public static string? Problem(SensorReading? reading, DateTime now)
    {
        if (reading is null)
        {
            return "Missing reading.";
        }

        if (reading.Mac is null || !macPattern().IsMatch(reading.Mac))
        {
            return "Mac must look like C9:38:30:34:17:76.";
        }

        if (string.IsNullOrWhiteSpace(reading.Name) || reading.Name.Length > MaxTextLength || (reading.Reporter?.Length ?? 0) > MaxTextLength)
        {
            return $"Name is required; name and reporter are at most {MaxTextLength} characters.";
        }

        if (!double.IsFinite(reading.TemperatureC) || reading.TemperatureC is < -50 or > 100)
        {
            return "TemperatureC must be between -50 and 100.";
        }

        if (!double.IsFinite(reading.Humidity) || reading.Humidity is < 0 or > 100 || reading.BatteryPercent is < 0 or > 100)
        {
            return "Humidity and BatteryPercent must be between 0 and 100.";
        }

        return reading.ObservedAt.ToUniversalTime() > now + clockSkew ? "ObservedAt is in the future." : null;
    }

    [GeneratedRegex("^([0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}$", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex macPattern();
}
