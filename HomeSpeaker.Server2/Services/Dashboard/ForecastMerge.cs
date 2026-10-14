using HomeSpeaker.Shared.Forecast;

namespace HomeSpeaker.Server2.Services.Dashboard;

public static class ForecastMerge
{
    /// <summary>
    /// The forecast to show after a refresh. A failed refresh (an error status, or nothing) does not replace a
    /// previous good forecast that still has hours: the strip picks the upcoming hours at render time, so the
    /// old data keeps moving on with the clock instead of the card vanishing for a minute.
    /// </summary>
    public static ForecastStatus? Latest(ForecastStatus? previous, ForecastStatus? refreshed)
    {
        var previousUsable = previous is { Error: null, Hourly.Count: > 0 };
        if (refreshed is null || (refreshed.Error is not null && previousUsable))
        {
            return previous;
        }

        return refreshed;
    }
}
