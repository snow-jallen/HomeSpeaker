namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>
/// Edits to an anchor's active periods. Every edit takes effect from today, so the past keeps the schedule it had:
/// days a past period covered still read the same afterwards.
/// </summary>
public static class AnchorPeriods
{
    public static IReadOnlyList<AnchorPeriod> Start(DateOnly today, int days) => [new AnchorPeriod(today, null, DayMask.Normalize(days))];

    public static bool IsActive(IReadOnlyList<AnchorPeriod> periods) => periods.Any(p => p.IsOpen);

    /// <summary>The open period's days, else the last period's, else every day.</summary>
    public static int CurrentDays(IReadOnlyList<AnchorPeriod> periods) =>
        periods.FirstOrDefault(p => p.IsOpen)?.Days ?? periods.OrderBy(p => p.From).LastOrDefault()?.Days ?? DayMask.Every;

    public static IReadOnlyList<AnchorPeriod> ChangeDays(IReadOnlyList<AnchorPeriod> periods, int days, DateOnly today)
    {
        days = DayMask.Normalize(days);
        var open = periods.FirstOrDefault(p => p.IsOpen);
        if (open is null || open.Days == days)
        {
            return periods;
        }

        var others = periods.Where(p => p != open);
        if (open.From >= today)
        {
            return [.. others, open with { Days = days }];
        }

        return [.. others, open with { To = today }, new AnchorPeriod(today, null, days)];
    }

    public static IReadOnlyList<AnchorPeriod> Retire(IReadOnlyList<AnchorPeriod> periods, DateOnly today)
    {
        var open = periods.FirstOrDefault(p => p.IsOpen);
        if (open is null)
        {
            return periods;
        }

        var others = periods.Where(p => p != open);
        return open.From >= today ? [.. others] : [.. others, open with { To = today }];
    }

    public static IReadOnlyList<AnchorPeriod> Restore(IReadOnlyList<AnchorPeriod> periods, DateOnly today)
    {
        if (IsActive(periods))
        {
            return periods;
        }

        var last = periods.OrderBy(p => p.From).LastOrDefault();
        if (last is null)
        {
            return Start(today, DayMask.Every);
        }

        if (last.To >= today)
        {
            return [.. periods.Where(p => p != last), last with { To = null }];
        }

        return [.. periods, new AnchorPeriod(today, null, last.Days)];
    }
}
