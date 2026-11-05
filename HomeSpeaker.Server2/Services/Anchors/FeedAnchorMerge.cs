namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>
/// Folds an external feed into this device's people. A local person linked to a feed person gets that person's feed
/// anchors beside their own; feed people nobody is linked to appear as people of their own. Feed anchors are never
/// editable here, and ticks still waiting in the outbox override what the feed last said.
/// </summary>
public static class FeedAnchorMerge
{
    public const string IdPrefix = "feed:";

    public static string WireId(string feedId) => IdPrefix + feedId;

    public static bool TryFeedId(string id, out string feedId)
    {
        if (id.StartsWith(IdPrefix, StringComparison.Ordinal) && id.Length > IdPrefix.Length)
        {
            feedId = id[IdPrefix.Length..];
            return true;
        }

        feedId = string.Empty;
        return false;
    }

    public static IReadOnlyList<SourcePerson> Merge(
        IReadOnlyList<SourcePerson> local,
        FeedPayload feed,
        string feedName,
        IReadOnlyDictionary<(string AnchorId, DateOnly Date), bool> pending)
    {
        var logsByAnchor = feed.Logs.GroupBy(l => l.AnchorId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        var anchorsByPerson = feed.Anchors
            .GroupBy(a => a.PersonId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(a => toSource(a, logsByAnchor, feedName, pending)).ToList(), StringComparer.Ordinal);

        var linked = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<SourcePerson>();
        foreach (var person in local)
        {
            if (person.FeedPersonId is { } feedPersonId && anchorsByPerson.TryGetValue(feedPersonId, out var extra))
            {
                linked.Add(feedPersonId);
                result.Add(person with { Anchors = [.. person.Anchors, .. extra] });
            }
            else
            {
                result.Add(person);
            }
        }

        var colorIndex = local.Count;
        foreach (var person in feed.People.Where(p => !linked.Contains(p.Id) && anchorsByPerson.ContainsKey(p.Id)))
        {
            result.Add(new SourcePerson(WireId(person.Id), person.Name, AnchorColors.ForIndex(colorIndex++), null, feedName, anchorsByPerson[person.Id]));
        }

        return result;
    }

    private static SourceAnchor toSource(FeedAnchor anchor, Dictionary<string, List<FeedLog>> logsByAnchor, string feedName, IReadOnlyDictionary<(string AnchorId, DateOnly Date), bool> pending)
    {
        var id = WireId(anchor.Id);
        var logs = new Dictionary<DateOnly, SourceLog>();
        if (logsByAnchor.TryGetValue(anchor.Id, out var feedLogs))
        {
            foreach (var log in feedLogs)
            {
                logs[log.Date] = new SourceLog(log.Done, log.DoneAt, log.ActualTime);
            }
        }

        foreach (var ((anchorId, date), done) in pending)
        {
            if (string.Equals(anchorId, id, StringComparison.Ordinal))
            {
                logs[date] = (logs.TryGetValue(date, out var fetched) ? fetched : new SourceLog(done)) with { Done = done, Pending = true };
            }
        }

        var slot = AnchorInput.TrySlot(anchor.Slot, out var parsed) ? parsed : AnchorSlot.Day;
        var target = AnchorInput.TryTargetTime(anchor.TargetTime, out var time) ? time : null;
        return new SourceAnchor(
            id,
            anchor.Title,
            slot,
            anchor.Order,
            target,
            [new AnchorPeriod(anchor.ActiveFrom, anchor.ActiveUntil, DayMask.Every)],
            logs,
            feedName,
            Editable: false,
            Writable: anchor.Writable,
            MissingIsUnknown: string.Equals(anchor.MissingIs, "unknown", StringComparison.OrdinalIgnoreCase));
    }
}
