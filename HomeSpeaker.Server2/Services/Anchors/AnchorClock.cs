namespace HomeSpeaker.Server2.Services.Anchors;

public static class AnchorClock
{
    /// <summary>The anchor home's local calendar day.</summary>
    public static DateOnly Today(TimeProvider timeProvider) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(timeProvider.GetUtcNow().UtcDateTime, timeProvider.LocalTimeZone));
}
