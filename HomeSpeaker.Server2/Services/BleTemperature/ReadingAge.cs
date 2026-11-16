using System.Globalization;

namespace HomeSpeaker.Server2.Services.BleTemperature;

/// <summary>
/// How old a sensor's last reading is and how to show it: under 5 minutes as is; 5-20 minutes with "N min old";
/// past 20 minutes no value, only "No reading for N min"; never heard, "No reading yet". Whole minutes, rounded down.
/// </summary>
public sealed record ReadingAge(bool ShowsValue, string? Note)
{
    public const int AgingAfterMinutes = 5;
    public const int LostAfterMinutes = 20;

    public static ReadingAge Of(DateTime? lastReadingAt, DateTime now)
    {
        if (lastReadingAt is not { } at)
        {
            return new ReadingAge(false, "No reading yet");
        }

        var minutes = (int)Math.Max(0, Math.Floor((now - at).TotalMinutes));
        if (minutes < AgingAfterMinutes)
        {
            return new ReadingAge(true, null);
        }

        var text = minutes.ToString(CultureInfo.InvariantCulture);
        return minutes <= LostAfterMinutes
            ? new ReadingAge(true, text + " min old")
            : new ReadingAge(false, "No reading for " + text + " min");
    }
}
