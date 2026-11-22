using System.Globalization;
using HomeSpeaker.Shared.Anchors;

namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>Presentation helpers shared by the Home card and the anchor pages, kept out of markup so they can be tested.</summary>
public static class AnchorView
{
    private static readonly string[] slotOrder = ["morning", "day", "evening"];

    public static int SlotRank(string slot)
    {
        var index = Array.IndexOf(slotOrder, slot);
        return index < 0 ? slotOrder.Length : index;
    }

    public static string SlotLabel(string slot) => slot switch
    {
        "morning" => "Morning",
        "evening" => "Evening",
        _ => "During the day",
    };

    /// <summary>The cell for <paramref name="date"/>, or null when the anchor's cells don't include it.</summary>
    public static AnchorCellDto? CellOn(AnchorDto anchor, DateOnly date) => anchor.Cells.FirstOrDefault(c => c.Date == date);

    /// <summary>Anchors in display order: slot, then their order within it.</summary>
    public static IEnumerable<AnchorDto> Ordered(IEnumerable<AnchorDto> anchors) =>
        anchors.OrderBy(a => SlotRank(a.Slot)).ThenBy(a => a.Order);

    /// <summary>Anchors worth a check chip on <paramref name="date"/>: expected (open), kept, or missed so they can still be ticked, in display order.</summary>
    public static IReadOnlyList<AnchorDto> CheckableOn(IEnumerable<AnchorDto> anchors, DateOnly date) =>
        [.. Ordered(anchors).Where(a => CellOn(a, date) is { } cell && (cell.State is "open" or "kept" or "missed" || cell.Pending))];

    /// <summary>How many of a person's anchors were expected on <paramref name="date"/> and how many of those were kept.</summary>
    public static AnchorDayProgress Progress(AnchorPersonDto person, DateOnly date)
    {
        var cells = person.Anchors.Select(a => CellOn(a, date)).OfType<AnchorCellDto>().Where(c => c.State is "open" or "kept" or "missed").ToList();
        return new AnchorDayProgress(cells.Count(c => c.State == "kept"), cells.Count);
    }

    /// <summary>Exactly <paramref name="days"/> cells ending on <paramref name="to"/>, oldest first; a date without a cell reads as none.</summary>
    public static IReadOnlyList<AnchorCellDto> History(AnchorDto anchor, DateOnly to, int days) =>
        [.. Enumerable.Range(0, days).Select(i => to.AddDays(i - days + 1)).Select(date => CellOn(anchor, date) ?? new AnchorCellDto(date, "none"))];

    /// <summary>Anchors that were active at some point in the board's range (a retired one still shows the days it was kept).</summary>
    public static IReadOnlyList<AnchorDto> HistoryRows(AnchorPersonDto person) =>
        [.. Ordered(person.Anchors).Where(a => a.Cells.Any(c => c.State != "none"))];

    public static string DotClass(AnchorCellDto cell) => cell.Pending ? "is-pending" : "is-" + cell.State;

    public static IReadOnlyList<AnchorSlotGroup> BySlot(IEnumerable<AnchorDto> anchors) =>
        [.. Ordered(anchors)
            .GroupBy(a => a.Slot)
            .Select(g => new AnchorSlotGroup(g.Key, SlotLabel(g.Key), [.. g]))];

    public static string DaysLabel(int days)
    {
        days = DayMask.Normalize(days);
        if (days == DayMask.Every)
        {
            return "Every day";
        }

        if (days == DayMask.Of(DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday))
        {
            return "Weekdays";
        }

        if (days == DayMask.Of(DayOfWeek.Saturday, DayOfWeek.Sunday))
        {
            return "Weekends";
        }

        // Monday first, the way a family week reads.
        DayOfWeek[] week = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];
        return string.Join(", ", week.Where(d => DayMask.Includes(days, d)).Select(d => d.ToString()[..3]));
    }

    /// <summary>The small line under an anchor's title: why it looks the way it does, its times, its streak.</summary>
    public static string CheckMeta(AnchorDto anchor, AnchorCellDto? cell)
    {
        if (cell?.Pending == true)
        {
            return "Waiting to sync";
        }

        switch (cell?.State)
        {
            case "off":
                return "Not scheduled today";
            case "unknown":
                return "No data";
        }

        var parts = new List<string>();
        if (cell?.ActualTime is { } actual)
        {
            parts.Add(Clock(actual));
            if (anchor.TargetTime is { } target)
            {
                parts.Add("target " + Clock(target));
            }
        }
        else if (anchor.TargetTime is { } target)
        {
            parts.Add("by " + Clock(target));
        }

        if (anchor.Streak >= 2)
        {
            parts.Add($"{anchor.Streak}-day streak");
        }

        return string.Join(" · ", parts);
    }

    /// <summary>"06:30" → "6:30 AM"; anything unparseable is shown as given.</summary>
    public static string Clock(string time) =>
        TimeOnly.TryParseExact(time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.ToString("h:mm tt", CultureInfo.InvariantCulture)
            : time;
}
