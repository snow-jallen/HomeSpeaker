namespace HomeSpeaker.Server2.Services;

/// <summary>"Now" as the forecast sees it, shared by the forecast service and the Home hourly strip.</summary>
public static class ForecastTime
{
    /// <summary>
    /// The current local time in the forecast's time zone, falling back to the server's local zone when the
    /// zone is missing or unknown.
    /// </summary>
    public static DateTime LocalNow(string? timezone, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(timezone))
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utcNow, TimeZoneInfo.Local);
        }

        try
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utcNow, TimeZoneInfo.FindSystemTimeZoneById(timezone));
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utcNow, TimeZoneInfo.Local);
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utcNow, TimeZoneInfo.Local);
        }
    }
}
