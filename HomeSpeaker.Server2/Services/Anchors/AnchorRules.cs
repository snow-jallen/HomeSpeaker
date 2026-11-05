namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>
/// What counts as expected, kept and missed. Pure: expectations are computed from an anchor's periods, never stored,
/// so a day with no data is never mistaken for a failure.
/// </summary>
public static class AnchorRules
{
    public static AnchorCell CellFor(IReadOnlyList<AnchorPeriod> periods, bool missingIsUnknown, DateOnly date, DateOnly today, bool done)
    {
        if (done)
        {
            return AnchorCell.Kept;
        }

        var period = periods.FirstOrDefault(p => p.Covers(date));
        if (period is null || date > today)
        {
            return AnchorCell.None;
        }

        if (!DayMask.Includes(period.Days, date.DayOfWeek))
        {
            return AnchorCell.Off;
        }

        if (date == today)
        {
            return AnchorCell.Open;
        }

        return missingIsUnknown ? AnchorCell.Unknown : AnchorCell.Missed;
    }

    /// <summary>Consecutive expected days kept, counting back from today. Today still open doesn't break it.</summary>
    public static int Streak(IReadOnlyList<AnchorPeriod> periods, bool missingIsUnknown, IReadOnlySet<DateOnly> doneDates, DateOnly today)
    {
        if (periods.Count == 0)
        {
            return 0;
        }

        var earliest = periods.Min(p => p.From);
        var streak = 0;
        for (var date = today; date >= earliest; date = date.AddDays(-1))
        {
            var state = CellFor(periods, missingIsUnknown, date, today, doneDates.Contains(date));
            if (state == AnchorCell.Missed)
            {
                break;
            }

            if (state == AnchorCell.Kept)
            {
                streak++;
            }
        }

        return streak;
    }

    /// <summary>
    /// Consecutive "anchored days" counting back from today: days with at least one anchor kept and none missed.
    /// Days with nothing expected, and today while anything is still open, are skipped rather than counted or broken.
    /// </summary>
    public static int PersonStreak(Func<DateOnly, IReadOnlyList<AnchorCell>> cellsOn, DateOnly today, DateOnly earliest)
    {
        var streak = 0;
        for (var date = today; date >= earliest; date = date.AddDays(-1))
        {
            var cells = cellsOn(date);
            if (cells.Contains(AnchorCell.Missed))
            {
                break;
            }

            if (cells.Contains(AnchorCell.Open) || !cells.Contains(AnchorCell.Kept))
            {
                continue;
            }

            streak++;
        }

        return streak;
    }
}
