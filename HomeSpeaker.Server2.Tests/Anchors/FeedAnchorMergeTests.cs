using HomeSpeaker.Server2.Services.Anchors;

namespace HomeSpeaker.Server2.Tests.Anchors;

public sealed class FeedAnchorMergeTests
{
    private static readonly DateOnly today = new(2026, 11, 4);

    private static readonly IReadOnlyDictionary<(string, DateOnly), bool> nothingPending = new Dictionary<(string, DateOnly), bool>();

    private static SourcePerson local(string id, string name, string? feedPersonId = null) =>
        new(id, name, "teal", null, "local",
            [new SourceAnchor(id + "-walk", "Walk", AnchorSlot.Day, 0, null, [new AnchorPeriod(today.AddDays(-10), null, DayMask.Every)], new Dictionary<DateOnly, SourceLog>())],
            feedPersonId);

    private static FeedPayload feed(params FeedAnchor[] anchors) => new(
        [new FeedPerson("u1", "Dad"), new FeedPerson("u2", "Emma"), new FeedPerson("u3", "Nobody shared")],
        [.. anchors],
        [new FeedLog("h1", today.AddDays(-1), true, null, null), new FeedLog("h2", today.AddDays(-1), true, null, "23:10")]);

    private static FeedAnchor check(string id, string person) =>
        new(id, person, "Stretch", "morning", 0, null, today.AddDays(-5), null, null, true);

    private static FeedAnchor sleep(string id, string person) =>
        new(id, person, "Lights out", "evening", 1, "22:30", today.AddDays(-5), null, "unknown", false);

    [Fact]
    public void LinkedPerson_GetsFeedAnchors_ReadOnlyDefinitions()
    {
        var merged = FeedAnchorMerge.Merge([local("1", "Dad", "u1")], feed(check("h1", "u1")), "Metanoeo", nothingPending);

        var dad = Assert.Single(merged, p => p.Id == "1");
        var stretch = Assert.Single(dad.Anchors, a => a.Title == "Stretch");
        Assert.Equal(("feed:h1", "Metanoeo", false, true, AnchorSlot.Morning), (stretch.Id, stretch.Source, stretch.Editable, stretch.Writable, stretch.Slot));
        Assert.True(stretch.Logs[today.AddDays(-1)].Done);
        Assert.Equal([new AnchorPeriod(today.AddDays(-5), null, DayMask.Every)], stretch.Periods);
    }

    [Fact]
    public void UnlinkedFeedPerson_AppearsAsOwnPerson_OnlyWithSharedAnchors()
    {
        var merged = FeedAnchorMerge.Merge([local("1", "Dad")], feed(check("h1", "u2")), "Metanoeo", nothingPending);

        Assert.Equal(["1", "feed:u2"], merged.Select(p => p.Id));
        var emma = merged[1];
        Assert.Equal(("Emma", "Metanoeo"), (emma.Name, emma.Source));
        Assert.True(AnchorColors.IsValid(emma.Color));
    }

    [Fact]
    public void SleepAnchor_MissingIsUnknown_NotWritable_KeepsActualTime()
    {
        var merged = FeedAnchorMerge.Merge([local("1", "Dad", "u1")], feed(sleep("h2", "u1")), "Metanoeo", nothingPending);

        var lightsOut = merged[0].Anchors.Single(a => a.Id == "feed:h2");
        Assert.True(lightsOut.MissingIsUnknown);
        Assert.False(lightsOut.Writable);
        Assert.Equal(new TimeOnly(22, 30), lightsOut.TargetTime);
        Assert.Equal("23:10", lightsOut.Logs[today.AddDays(-1)].ActualTime);
        Assert.Equal(AnchorCell.Unknown, AnchorRules.CellFor(lightsOut.Periods, lightsOut.MissingIsUnknown, today.AddDays(-2), today, done: false));
    }

    [Fact]
    public void Pending_OverridesFetchedValue_AndIsMarked()
    {
        var pending = new Dictionary<(string, DateOnly), bool> { [("feed:h1", today)] = true, [("feed:h1", today.AddDays(-1))] = false };

        var stretch = FeedAnchorMerge.Merge([local("1", "Dad", "u1")], feed(check("h1", "u1")), "Metanoeo", pending)[0].Anchors.Single(a => a.Id == "feed:h1");

        Assert.Equal(new SourceLog(true, null, null, Pending: true), stretch.Logs[today]);
        Assert.False(stretch.Logs[today.AddDays(-1)].Done);
        Assert.True(stretch.Logs[today.AddDays(-1)].Pending);
    }

    [Fact]
    public void UnknownSlot_FallsBackToDay_ActiveUntilBecomesClosedPeriod()
    {
        var anchor = check("h1", "u1") with { Slot = "noon", ActiveUntil = today };

        var merged = FeedAnchorMerge.Merge([local("1", "Dad", "u1")], feed(anchor), "Metanoeo", nothingPending)[0].Anchors.Single(a => a.Id == "feed:h1");

        Assert.Equal(AnchorSlot.Day, merged.Slot);
        Assert.Equal([new AnchorPeriod(today.AddDays(-5), today, DayMask.Every)], merged.Periods);
    }

    [Fact]
    public void LinkedPersonBoard_CountsFeedAnchorsToday()
    {
        var merged = FeedAnchorMerge.Merge([local("1", "Dad", "u1")], feed(check("h1", "u1")), "Metanoeo", nothingPending);

        var dad = AnchorBoardBuilder.Person(merged[0], today, today, today);

        Assert.Equal((0, 2), (dad.TodayDone, dad.TodayTotal));
        Assert.Equal("u1", dad.FeedPersonId);
    }
}
