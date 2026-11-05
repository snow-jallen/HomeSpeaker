using HomeSpeaker.Server2.Services.Anchors;

namespace HomeSpeaker.Server2.Tests.Anchors;

public sealed class AnchorRulesTests
{
    // 2026-11-04 is a Wednesday.
    private static readonly DateOnly today = new(2026, 11, 4);

    private static DateOnly day(int month, int dayOfMonth) => new(2026, month, dayOfMonth);

    private static IReadOnlyList<AnchorPeriod> everyDayFrom(DateOnly from) => [new AnchorPeriod(from, null, DayMask.Every)];

    private static AnchorCell cell(IReadOnlyList<AnchorPeriod> periods, DateOnly date, bool done = false, bool missingIsUnknown = false) =>
        AnchorRules.CellFor(periods, missingIsUnknown, date, today, done);

    [Fact]
    public void CellFor_BeforeFirstPeriod_IsNone() =>
        Assert.Equal(AnchorCell.None, cell(everyDayFrom(day(11, 1)), day(10, 31)));

    [Fact]
    public void CellFor_UnscheduledWeekday_IsOff()
    {
        var weekdays = DayMask.Of(DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday);
        IReadOnlyList<AnchorPeriod> periods = [new AnchorPeriod(day(10, 1), null, weekdays)];

        Assert.Equal(AnchorCell.Off, cell(periods, day(11, 1))); // a Sunday
    }

    [Fact]
    public void CellFor_TodayNotDone_IsOpen() =>
        Assert.Equal(AnchorCell.Open, cell(everyDayFrom(day(10, 1)), today));

    [Fact]
    public void CellFor_PastNotDone_IsMissed() =>
        Assert.Equal(AnchorCell.Missed, cell(everyDayFrom(day(10, 1)), day(11, 3)));

    [Fact]
    public void CellFor_PastNotDone_MissingIsUnknown_IsUnknown() =>
        Assert.Equal(AnchorCell.Unknown, cell(everyDayFrom(day(10, 1)), day(11, 3), missingIsUnknown: true));

    [Fact]
    public void CellFor_DoneOutsidePeriods_IsKept() =>
        Assert.Equal(AnchorCell.Kept, cell(everyDayFrom(day(11, 1)), day(10, 20), done: true));

    [Fact]
    public void CellFor_GapBetweenPeriods_IsNone()
    {
        IReadOnlyList<AnchorPeriod> periods =
        [
            new AnchorPeriod(day(10, 1), day(10, 10), DayMask.Every),
            new AnchorPeriod(day(10, 20), null, DayMask.Every),
        ];

        Assert.Equal(AnchorCell.None, cell(periods, day(10, 15)));
        Assert.Equal(AnchorCell.Missed, cell(periods, day(10, 9)));
        Assert.Equal(AnchorCell.None, cell(periods, day(10, 10)));
    }

    [Fact]
    public void CellFor_FutureDate_IsNone() =>
        Assert.Equal(AnchorCell.None, cell(everyDayFrom(day(10, 1)), day(11, 5)));

    [Fact]
    public void Streak_CountsKeptBackToFirstMiss()
    {
        var done = new HashSet<DateOnly> { day(11, 4), day(11, 3), day(11, 1) };

        Assert.Equal(2, AnchorRules.Streak(everyDayFrom(day(10, 1)), missingIsUnknown: false, done, today));
    }

    [Fact]
    public void Streak_TodayOpenDoesNotBreak()
    {
        var done = new HashSet<DateOnly> { day(11, 3), day(11, 2) };

        Assert.Equal(2, AnchorRules.Streak(everyDayFrom(day(10, 1)), missingIsUnknown: false, done, today));
    }

    [Fact]
    public void Streak_SkipsOffDays()
    {
        var weekdays = DayMask.Of(DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday);
        IReadOnlyList<AnchorPeriod> periods = [new AnchorPeriod(day(10, 26), null, weekdays)];

        // Thu 10-29, Fri 10-30, (weekend off), Mon 11-2, Tue 11-3, Wed 11-4.
        var done = new HashSet<DateOnly> { day(10, 29), day(10, 30), day(11, 2), day(11, 3), day(11, 4) };

        Assert.Equal(5, AnchorRules.Streak(periods, missingIsUnknown: false, done, today));
    }

    [Fact]
    public void Streak_SkipsGapBetweenPeriods()
    {
        IReadOnlyList<AnchorPeriod> periods =
        [
            new AnchorPeriod(day(10, 1), day(10, 30), DayMask.Every),
            new AnchorPeriod(day(11, 3), null, DayMask.Every),
        ];
        var done = new HashSet<DateOnly> { day(10, 29), day(10, 28), day(11, 3), day(11, 4) };

        Assert.Equal(4, AnchorRules.Streak(periods, missingIsUnknown: false, done, today));
    }

    [Fact]
    public void Streak_UnknownDaysAreSkipped()
    {
        var done = new HashSet<DateOnly> { day(11, 3), day(11, 1) };

        Assert.Equal(2, AnchorRules.Streak(everyDayFrom(day(10, 31)), missingIsUnknown: true, done, today));
    }

    [Fact]
    public void PersonStreak_AllKeptDaysCount()
    {
        IReadOnlyList<AnchorCell> cellsOn(DateOnly date) => date == today ? [AnchorCell.Kept, AnchorCell.Kept] : [AnchorCell.Kept, AnchorCell.Off];

        Assert.Equal(5, AnchorRules.PersonStreak(cellsOn, today, today.AddDays(-4)));
    }

    [Fact]
    public void PersonStreak_AnyMissedStops()
    {
        IReadOnlyList<AnchorCell> cellsOn(DateOnly date) => date == day(11, 2) ? [AnchorCell.Kept, AnchorCell.Missed] : [AnchorCell.Kept];

        Assert.Equal(2, AnchorRules.PersonStreak(cellsOn, today, day(10, 1)));
    }

    [Fact]
    public void PersonStreak_TodayPartlyOpenSkipped()
    {
        IReadOnlyList<AnchorCell> cellsOn(DateOnly date) => date == today ? [AnchorCell.Kept, AnchorCell.Open] : [AnchorCell.Kept];

        Assert.Equal(3, AnchorRules.PersonStreak(cellsOn, today, day(11, 1)));
    }

    [Fact]
    public void PersonStreak_DayWithNothingExpectedSkipped()
    {
        IReadOnlyList<AnchorCell> cellsOn(DateOnly date) => date == day(11, 2) ? [AnchorCell.Off, AnchorCell.None] : [AnchorCell.Kept];

        // 10-30, 10-31, 11-1, (11-2 skipped), 11-3, 11-4.
        Assert.Equal(5, AnchorRules.PersonStreak(cellsOn, today, day(10, 30)));
    }
}
