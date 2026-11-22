using HomeSpeaker.Server2.Services.Anchors;
using HomeSpeaker.Shared.Anchors;

namespace HomeSpeaker.Server2.Tests.Anchors;

public sealed class AnchorViewTests
{
    private static readonly DateOnly today = new(2026, 11, 4);

    private static AnchorDto anchor(string id, string slot, int order, string state, bool pending = false) =>
        new(id, id, slot, order, null, DayMask.Every, false, [], "local", true, true, 0, [new AnchorCellDto(today, state, pending)]);

    private static AnchorPersonDto person(params AnchorDto[] anchors) => new("1", "Sam", "teal", null, "local", 0, 0, 0, [.. anchors]);

    [Fact]
    public void DotClass_PendingWins() =>
        Assert.Equal("is-pending", AnchorView.DotClass(new AnchorCellDto(today, "kept", Pending: true)));

    [Theory]
    [InlineData("kept", "is-kept")]
    [InlineData("open", "is-open")]
    [InlineData("missed", "is-missed")]
    public void DotClass_FollowsState(string state, string expected) =>
        Assert.Equal(expected, AnchorView.DotClass(new AnchorCellDto(today, state)));

    [Fact]
    public void BySlot_GroupsMorningDayEvening_SkippingEmpty()
    {
        var sam = person(anchor("e", "evening", 0, "open"), anchor("m2", "morning", 1, "open"), anchor("m1", "morning", 0, "open"));

        var groups = AnchorView.BySlot(sam.Anchors);

        Assert.Equal(["morning", "evening"], groups.Select(g => g.Slot));
        Assert.Equal(["m1", "m2"], groups[0].Anchors.Select(a => a.Id));
    }

    [Theory]
    [InlineData(DayMask.Every, "Every day")]
    [InlineData(62, "Weekdays")]
    [InlineData(65, "Weekends")]
    [InlineData(42, "Mon, Wed, Fri")]
    public void DaysLabel_Readable(int days, string expected) =>
        Assert.Equal(expected, AnchorView.DaysLabel(days));

    [Fact]
    public void CheckMeta_TargetTimeAndStreak()
    {
        var walk = anchor("w", "morning", 0, "open") with { TargetTime = "06:30", Streak = 4 };

        Assert.Equal("by 6:30 AM · 4-day streak", AnchorView.CheckMeta(walk, walk.Cells[0]));
    }

    [Fact]
    public void CheckMeta_ActualTimeBesideTarget()
    {
        var sleep = anchor("s", "evening", 0, "kept") with { TargetTime = "22:30" };
        var cell = sleep.Cells[0] with { ActualTime = "01:04" };

        Assert.Equal("1:04 AM · target 10:30 PM", AnchorView.CheckMeta(sleep, cell));
    }

    [Theory]
    [InlineData("off", "Not scheduled today")]
    [InlineData("unknown", "No data")]
    public void CheckMeta_ExplainsQuietStates(string state, string expected)
    {
        var walk = anchor("w", "day", 0, state);

        Assert.Equal(expected, AnchorView.CheckMeta(walk, walk.Cells[0]));
    }

    [Fact]
    public void CheckMeta_PendingSaysSo()
    {
        var walk = anchor("w", "day", 0, "kept", pending: true);

        Assert.Equal("Waiting to sync", AnchorView.CheckMeta(walk, walk.Cells[0]));
    }

    [Fact]
    public void CheckMeta_StreakOfOneIsQuiet() =>
        Assert.Equal(string.Empty, AnchorView.CheckMeta(anchor("w", "day", 0, "kept") with { Streak = 1 }, null));

    private static AnchorDto history(string id, params AnchorCellDto[] cells) =>
        new(id, id, "day", 0, null, DayMask.Every, false, [], "local", true, true, 0, [.. cells]);

    [Fact]
    public void CheckableOn_IncludesMissedForPastDays_InSlotOrder()
    {
        var anchors = new[]
        {
            anchor("evening-open", "evening", 0, "open"),
            anchor("day-off", "day", 0, "off"),
            anchor("morning-missed", "morning", 0, "missed"),
            anchor("morning-none", "morning", 1, "none"),
            anchor("day-kept", "day", 1, "kept"),
        };

        Assert.Equal(["morning-missed", "day-kept", "evening-open"], AnchorView.CheckableOn(anchors, today).Select(a => a.Id));
    }

    [Fact]
    public void CheckableOn_IncludesPendingEvenWhenOff() =>
        Assert.Single(AnchorView.CheckableOn([anchor("p", "day", 0, "off", pending: true)], today));

    [Fact]
    public void CheckableOn_SkipsAnchorsWithoutACellThatDay() =>
        Assert.Empty(AnchorView.CheckableOn([anchor("a", "day", 0, "open")], today.AddDays(-1)));

    [Fact]
    public void Progress_CountsExpectedAndKept()
    {
        var sam = person(
            anchor("k", "day", 0, "kept"),
            anchor("m", "day", 1, "missed"),
            anchor("o", "day", 2, "open"),
            anchor("f", "day", 3, "off"),
            anchor("n", "day", 4, "none"));

        Assert.Equal(new AnchorDayProgress(1, 3), AnchorView.Progress(sam, today));
    }

    [Fact]
    public void History_FillsMissingDatesWithNone_OldestFirst()
    {
        var walk = history("w", new AnchorCellDto(today, "kept"), new AnchorCellDto(today.AddDays(-2), "missed"));

        var cells = AnchorView.History(walk, today, 4);

        Assert.Equal([today.AddDays(-3), today.AddDays(-2), today.AddDays(-1), today], cells.Select(c => c.Date));
        Assert.Equal(["none", "missed", "none", "kept"], cells.Select(c => c.State));
    }

    [Fact]
    public void HistoryRows_SkipsAnchorsWithOnlyNoneCells()
    {
        var sam = person(
            history("old", new AnchorCellDto(today, "none"), new AnchorCellDto(today.AddDays(-1), "none")),
            history("live", new AnchorCellDto(today, "none"), new AnchorCellDto(today.AddDays(-1), "kept")));

        Assert.Equal(["live"], AnchorView.HistoryRows(sam).Select(a => a.Id));
    }
}
