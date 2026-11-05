using HomeSpeaker.Server2.Data;
using HomeSpeaker.Shared.Anchors;

namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>Turns people and anchors from any source into the board the pages and the API show.</summary>
public static class AnchorBoardBuilder
{
    /// <summary>How far back streaks look.</summary>
    public const int StreakLookbackDays = 366;

    public static SourcePerson FromEntity(AnchorPersonEntity person) => new(
        AnchorInput.Id(person.Id),
        person.Name,
        person.Color,
        person.Emoji,
        "local",
        [.. person.Anchors.Select(FromEntity)],
        person.FeedPersonId);

    public static SourceAnchor FromEntity(AnchorEntity anchor) => new(
        AnchorInput.Id(anchor.Id),
        anchor.Title,
        anchor.Slot,
        anchor.SortOrder,
        anchor.TargetTime,
        [.. anchor.Periods.OrderBy(p => p.From).Select(p => new AnchorPeriod(p.From, p.To, p.Days))],
        anchor.Logs.GroupBy(l => l.Date).ToDictionary(g => g.Key, g => new SourceLog(g.First().Done, g.First().DoneAt)));

    public static AnchorPersonDto Person(AnchorPersonEntity person, DateOnly from, DateOnly to, DateOnly today) =>
        Person(FromEntity(person), from, to, today);

    public static AnchorPersonDto Person(SourcePerson person, DateOnly from, DateOnly to, DateOnly today)
    {
        var anchors = person.Anchors;
        static bool done(SourceAnchor a, DateOnly d) => a.Logs.TryGetValue(d, out var log) && log.Done;

        var earliest = anchors.SelectMany(a => a.Periods).Select(p => p.From).DefaultIfEmpty(today).Min();
        earliest = earliest < today.AddDays(-StreakLookbackDays) ? today.AddDays(-StreakLookbackDays) : earliest;
        var streak = AnchorRules.PersonStreak(
            date => [.. anchors.Select(a => AnchorRules.CellFor(a.Periods, a.MissingIsUnknown, date, today, done(a, date)))],
            today,
            earliest);

        var todayCells = anchors.Select(a => AnchorRules.CellFor(a.Periods, a.MissingIsUnknown, today, today, done(a, today))).ToList();
        return new AnchorPersonDto(
            person.Id,
            person.Name,
            person.Color,
            person.Emoji,
            person.Source,
            streak,
            todayCells.Count(c => c == AnchorCell.Kept),
            todayCells.Count(c => c is AnchorCell.Kept or AnchorCell.Open),
            [.. anchors.Select(a => anchor(a, from, to, today))],
            person.FeedPersonId);
    }

    private static AnchorDto anchor(SourceAnchor source, DateOnly from, DateOnly to, DateOnly today)
    {
        var cells = new List<AnchorCellDto>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            source.Logs.TryGetValue(date, out var log);
            var state = AnchorRules.CellFor(source.Periods, source.MissingIsUnknown, date, today, log?.Done == true);
            cells.Add(new AnchorCellDto(date, AnchorInput.Wire(state), log?.Pending == true, log?.Done == true ? log.DoneAt : null, log?.ActualTime));
        }

        var doneDates = source.Logs.Where(l => l.Value.Done).Select(l => l.Key).ToHashSet();
        return new AnchorDto(
            source.Id,
            source.Title,
            AnchorInput.Wire(source.Slot),
            source.Order,
            AnchorInput.FormatTime(source.TargetTime),
            AnchorPeriods.CurrentDays(source.Periods),
            !AnchorPeriods.IsActive(source.Periods),
            [.. source.Periods.Select(p => new AnchorPeriodDto(p.From, p.To, p.Days))],
            source.Source,
            source.Editable,
            source.Writable,
            AnchorRules.Streak(source.Periods, source.MissingIsUnknown, doneDates, today),
            cells);
    }
}
