using HomeSpeaker.Server2.Services.Anchors;

namespace HomeSpeaker.Server2.Tests.Anchors;

public sealed class AnchorPeriodsTests
{
    private static readonly DateOnly today = new(2026, 11, 4);
    private static readonly int weekdays = DayMask.Of(DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday);

    private static DateOnly day(int month, int dayOfMonth) => new(2026, month, dayOfMonth);

    [Fact]
    public void Start_IsOneOpenPeriodFromToday() =>
        Assert.Equal([new AnchorPeriod(today, null, weekdays)], AnchorPeriods.Start(today, weekdays));

    [Fact]
    public void ChangeDays_ClosesOldPeriodAtToday()
    {
        IReadOnlyList<AnchorPeriod> before = [new AnchorPeriod(day(10, 1), null, DayMask.Every)];

        var after = AnchorPeriods.ChangeDays(before, weekdays, today);

        Assert.Equal([new AnchorPeriod(day(10, 1), today, DayMask.Every), new AnchorPeriod(today, null, weekdays)], after);
    }

    [Fact]
    public void ChangeDays_DoesNotAlterPastCells()
    {
        IReadOnlyList<AnchorPeriod> before = [new AnchorPeriod(day(10, 1), null, weekdays)];
        var after = AnchorPeriods.ChangeDays(before, DayMask.Every, today);

        for (var date = day(10, 1); date < today; date = date.AddDays(1))
        {
            Assert.Equal(
                AnchorRules.CellFor(before, missingIsUnknown: false, date, today, done: false),
                AnchorRules.CellFor(after, missingIsUnknown: false, date, today, done: false));
        }
    }

    [Fact]
    public void ChangeDays_SameDays_Unchanged()
    {
        IReadOnlyList<AnchorPeriod> before = [new AnchorPeriod(day(10, 1), null, DayMask.Every)];

        Assert.Equal(before, AnchorPeriods.ChangeDays(before, DayMask.Every, today));
    }

    [Fact]
    public void ChangeDays_PeriodStartedToday_EditsInPlace()
    {
        IReadOnlyList<AnchorPeriod> before = [new AnchorPeriod(day(10, 1), day(10, 20), DayMask.Every), new AnchorPeriod(today, null, DayMask.Every)];

        var after = AnchorPeriods.ChangeDays(before, weekdays, today);

        Assert.Equal([new AnchorPeriod(day(10, 1), day(10, 20), DayMask.Every), new AnchorPeriod(today, null, weekdays)], after);
    }

    [Fact]
    public void ChangeDays_Retired_Unchanged()
    {
        IReadOnlyList<AnchorPeriod> before = [new AnchorPeriod(day(10, 1), day(10, 20), DayMask.Every)];

        Assert.Equal(before, AnchorPeriods.ChangeDays(before, weekdays, today));
    }

    [Fact]
    public void Retire_ClosesAtToday()
    {
        IReadOnlyList<AnchorPeriod> before = [new AnchorPeriod(day(10, 1), null, DayMask.Every)];

        var after = AnchorPeriods.Retire(before, today);

        Assert.Equal([new AnchorPeriod(day(10, 1), today, DayMask.Every)], after);
        Assert.False(AnchorPeriods.IsActive(after));
    }

    [Fact]
    public void Retire_PeriodStartedToday_RemovesIt() =>
        Assert.Empty(AnchorPeriods.Retire([new AnchorPeriod(today, null, DayMask.Every)], today));

    [Fact]
    public void RetireThenRestoreSameDay_LeavesOneOpenPeriod()
    {
        IReadOnlyList<AnchorPeriod> before = [new AnchorPeriod(day(10, 1), null, DayMask.Every)];

        var after = AnchorPeriods.Restore(AnchorPeriods.Retire(before, today), today);

        Assert.Equal(before, after);
    }

    [Fact]
    public void Restore_AfterGap_AppendsPeriodWithLastDays()
    {
        IReadOnlyList<AnchorPeriod> before = [new AnchorPeriod(day(10, 1), day(10, 20), weekdays)];

        var after = AnchorPeriods.Restore(before, today);

        Assert.Equal([new AnchorPeriod(day(10, 1), day(10, 20), weekdays), new AnchorPeriod(today, null, weekdays)], after);
        Assert.Equal(weekdays, AnchorPeriods.CurrentDays(after));
    }

    [Fact]
    public void Restore_NoPeriods_StartsEveryDay() =>
        Assert.Equal([new AnchorPeriod(today, null, DayMask.Every)], AnchorPeriods.Restore([], today));

    [Fact]
    public void Restore_AlreadyActive_Unchanged()
    {
        IReadOnlyList<AnchorPeriod> before = [new AnchorPeriod(day(10, 1), null, DayMask.Every)];

        Assert.Equal(before, AnchorPeriods.Restore(before, today));
    }
}
