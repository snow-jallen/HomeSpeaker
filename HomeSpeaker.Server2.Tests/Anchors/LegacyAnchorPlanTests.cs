using HomeSpeaker.Server2.Services.Anchors;

namespace HomeSpeaker.Server2.Tests.Anchors;

public sealed class LegacyAnchorPlanTests
{
    private static readonly DateOnly today = new(2026, 11, 4);

    private static DateOnly day(int month, int dayOfMonth) => new(2026, month, dayOfMonth);

    private static LegacyAssignment assigned(string user, int definition, string name, bool active = true) =>
        new(user, definition, name, active, new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc));

    private static IEnumerable<LegacyAnchorRow> rows(string user, int definition, string name, DateOnly from, DateOnly through, bool completed = false)
    {
        for (var date = from; date <= through; date = date.AddDays(1))
        {
            yield return new LegacyAnchorRow(user, definition, name, date, completed, completed ? new DateTime(2026, 1, 1, 15, 0, 0, DateTimeKind.Utc) : null);
        }
    }

    [Fact]
    public void Plan_ConsecutiveRows_OnePeriod()
    {
        var plan = LegacyAnchorPlan.Plan([assigned("Sam", 1, "Read")], [.. rows("Sam", 1, "Read", day(10, 1), day(11, 3))], today);

        var anchor = Assert.Single(Assert.Single(plan).Anchors);
        Assert.Equal("Read", anchor.Title);
        Assert.Equal([new AnchorPeriod(day(10, 1), null, DayMask.Every)], anchor.Periods);
    }

    [Fact]
    public void Plan_GapInRows_TwoPeriods_GapIsNone()
    {
        var plan = LegacyAnchorPlan.Plan(
            [assigned("Sam", 1, "Read")],
            [.. rows("Sam", 1, "Read", day(10, 1), day(10, 10)), .. rows("Sam", 1, "Read", day(10, 15), day(11, 4))],
            today);

        var periods = Assert.Single(Assert.Single(plan).Anchors).Periods;
        Assert.Equal([new AnchorPeriod(day(10, 1), day(10, 11), DayMask.Every), new AnchorPeriod(day(10, 15), null, DayMask.Every)], periods);
        Assert.Equal(AnchorCell.None, AnchorRules.CellFor(periods, missingIsUnknown: false, day(10, 12), today, done: false));
    }

    [Fact]
    public void Plan_ActiveAssignmentEndingLastWeek_ClosedPlusOpenFromToday()
    {
        var plan = LegacyAnchorPlan.Plan([assigned("Sam", 1, "Read")], [.. rows("Sam", 1, "Read", day(10, 1), day(10, 28))], today);

        Assert.Equal(
            [new AnchorPeriod(day(10, 1), day(10, 29), DayMask.Every), new AnchorPeriod(today, null, DayMask.Every)],
            Assert.Single(Assert.Single(plan).Anchors).Periods);
    }

    [Fact]
    public void Plan_AssignedNoRows_OpenFromAssignmentDate() =>
        Assert.Equal(
            [new AnchorPeriod(day(9, 1), null, DayMask.Every)],
            Assert.Single(Assert.Single(LegacyAnchorPlan.Plan([assigned("Sam", 1, "Read")], [], today)).Anchors).Periods);

    [Fact]
    public void Plan_UnassignedDefinition_AllClosed()
    {
        var plan = LegacyAnchorPlan.Plan([], [.. rows("Sam", 1, "Read", day(10, 1), day(11, 4))], today);

        Assert.Equal([new AnchorPeriod(day(10, 1), day(11, 5), DayMask.Every)], Assert.Single(Assert.Single(plan).Anchors).Periods);
    }

    [Fact]
    public void Plan_InactiveDefinition_AllClosed()
    {
        var plan = LegacyAnchorPlan.Plan([assigned("Sam", 1, "Read", active: false)], [.. rows("Sam", 1, "Read", day(10, 1), day(10, 5))], today);

        Assert.Equal([new AnchorPeriod(day(10, 1), day(10, 6), DayMask.Every)], Assert.Single(Assert.Single(plan).Anchors).Periods);
    }

    [Fact]
    public void Plan_InactiveAssignmentWithoutRows_Skipped() =>
        Assert.Empty(LegacyAnchorPlan.Plan([assigned("Sam", 1, "Read", active: false)], [], today));

    [Fact]
    public void Plan_CompletedRowsBecomeLogs()
    {
        var plan = LegacyAnchorPlan.Plan(
            [assigned("Sam", 1, "Read")],
            [.. rows("Sam", 1, "Read", day(11, 1), day(11, 2), completed: true), .. rows("Sam", 1, "Read", day(11, 3), day(11, 4))],
            today);

        Assert.Equal([day(11, 1), day(11, 2)], Assert.Single(Assert.Single(plan).Anchors).Completed.Select(c => c.Date));
    }

    [Fact]
    public void Plan_PeopleOrderedByName_WithDistinctAnchors()
    {
        var plan = LegacyAnchorPlan.Plan([assigned("sam", 1, "Read"), assigned("Emma", 1, "Read"), assigned("Emma", 2, "Walk")], [], today);

        Assert.Equal(["Emma", "sam"], plan.Select(p => p.Name));
        Assert.Equal(["Read", "Walk"], plan[0].Anchors.Select(a => a.Title));
    }
}
