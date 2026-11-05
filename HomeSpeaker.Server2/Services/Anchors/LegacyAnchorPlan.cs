namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>
/// Turns the old anchor tables into people, anchors, periods and logs. The old worker only wrote a day's rows when it
/// ran, so each unbroken run of dated rows becomes one active period and the gaps read as "not active", never as misses.
/// </summary>
public static class LegacyAnchorPlan
{
    public static IReadOnlyList<PlannedPerson> Plan(IReadOnlyList<LegacyAssignment> assignments, IReadOnlyList<LegacyAnchorRow> dailyRows, DateOnly today)
    {
        var keys = assignments.Select(a => (User: a.UserId.Trim(), a.DefinitionId))
            .Concat(dailyRows.Select(r => (User: r.UserId.Trim(), r.DefinitionId)))
            .Where(k => k.User.Length > 0)
            .Distinct()
            .ToList();

        var people = new List<PlannedPerson>();
        foreach (var person in keys.GroupBy(k => k.User, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var anchors = new List<PlannedAnchor>();
            foreach (var definitionId in person.Select(k => k.DefinitionId).Distinct())
            {
                var assignment = assignments.FirstOrDefault(a => a.DefinitionId == definitionId && string.Equals(a.UserId.Trim(), person.Key, StringComparison.OrdinalIgnoreCase));
                var rows = dailyRows
                    .Where(r => r.DefinitionId == definitionId && string.Equals(r.UserId.Trim(), person.Key, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(r => r.Date)
                    .ToList();
                var anchor = planAnchor(assignment, rows, today);
                if (anchor is not null)
                {
                    anchors.Add(anchor);
                }
            }

            if (anchors.Count > 0)
            {
                people.Add(new PlannedPerson(person.Key, [.. anchors.OrderBy(a => a.Title, StringComparer.OrdinalIgnoreCase)]));
            }
        }

        return people;
    }

    private static PlannedAnchor? planAnchor(LegacyAssignment? assignment, List<LegacyAnchorRow> rows, DateOnly today)
    {
        var stillActive = assignment is { DefinitionActive: true };
        var title = (assignment?.Name ?? rows.LastOrDefault()?.Name ?? string.Empty).Trim();
        if (title.Length == 0)
        {
            return null;
        }

        var periods = new List<AnchorPeriod>();
        foreach (var run in runsOf(rows.Select(r => r.Date).Distinct()))
        {
            periods.Add(new AnchorPeriod(run.From, run.Through.AddDays(1), DayMask.Every));
        }

        if (stillActive)
        {
            var last = periods.LastOrDefault();
            if (last is null)
            {
                periods.Add(new AnchorPeriod(DateOnly.FromDateTime(assignment!.AssignedAt), null, DayMask.Every));
            }
            else if (last.To >= today)
            {
                periods[^1] = last with { To = null };
            }
            else
            {
                periods.Add(new AnchorPeriod(today, null, DayMask.Every));
            }
        }

        if (periods.Count == 0)
        {
            return null;
        }

        var completed = rows.Where(r => r.Completed)
            .GroupBy(r => r.Date)
            .Select(g => new PlannedCompletion(g.Key, g.First().CompletedAt))
            .ToList();
        return new PlannedAnchor(title, periods, completed);
    }

    private static IEnumerable<(DateOnly From, DateOnly Through)> runsOf(IEnumerable<DateOnly> dates)
    {
        DateOnly? from = null;
        DateOnly previous = default;
        foreach (var date in dates.OrderBy(d => d))
        {
            if (from is null)
            {
                from = date;
            }
            else if (date != previous.AddDays(1))
            {
                yield return (from.Value, previous);
                from = date;
            }

            previous = date;
        }

        if (from is not null)
        {
            yield return (from.Value, previous);
        }
    }
}
