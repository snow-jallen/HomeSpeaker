namespace HomeSpeaker.Server2.Services.Photos;

/// <summary>
/// Shuffled rotation: every available photo is shown once per round, and a new round never starts
/// with the photo that ended the previous one. Not thread-safe; PhotoLibrary locks around it.
/// </summary>
public sealed class PhotoCycle(Random random)
{
    private readonly HashSet<string> shownThisRound = new(StringComparer.Ordinal);
    private HashSet<string> available = new(StringComparer.Ordinal);
    private List<string> pending = [];
    private List<string>? nextRound;
    private string? last;

    public void SetAvailable(IEnumerable<string> ids)
    {
        available = new HashSet<string>(ids, StringComparer.Ordinal);
        shownThisRound.IntersectWith(available);
        pending = shuffle(available.Where(id => !shownThisRound.Contains(id)));
        nextRound = null;
    }

    /// <summary>
    /// The next <paramref name="count"/> ids <see cref="Next"/> will return, without consuming them. When the current round
    /// has fewer than that left, the following round is shuffled now and kept, so the peeked order is the order Next() returns.
    /// Returns fewer than <paramref name="count"/> ids if the current and next round together hold fewer.
    /// </summary>
    public IReadOnlyList<string> Peek(int count)
    {
        if (count <= 0 || available.Count == 0)
        {
            return [];
        }

        var result = new List<string>(Math.Min(count, pending.Count + available.Count));
        result.AddRange(pending.Take(count));
        if (result.Count < count)
        {
            nextRound ??= buildNextRound();
            result.AddRange(nextRound.Take(count - result.Count));
        }

        return result;
    }

    public string? Next()
    {
        if (available.Count == 0)
        {
            return null;
        }

        if (pending.Count == 0)
        {
            shownThisRound.Clear();
            pending = nextRound ?? buildNextRound();
            nextRound = null;
        }

        var id = pending[0];
        pending.RemoveAt(0);
        shownThisRound.Add(id);
        last = id;
        return id;
    }

    /// <summary>
    /// Removes and returns the first id still pending in this round that matches, or null when none does. Used to pull a
    /// partner slide forward; it never reaches into the next round, so every photo is still shown once per round.
    /// </summary>
    public string? TakeNext(Func<string, bool> match)
    {
        var index = pending.FindIndex(id => match(id));
        if (index < 0)
        {
            return null;
        }

        var id = pending[index];
        pending.RemoveAt(index);
        shownThisRound.Add(id);
        return id;
    }

    /// <summary>Shuffles a full round that does not start with the photo ending the current one (the last pending id, or the last shown).</summary>
    private List<string> buildNextRound()
    {
        var round = shuffle(available);
        var previous = pending.Count > 0 ? pending[^1] : last;
        if (round.Count > 1 && string.Equals(round[0], previous, StringComparison.Ordinal))
        {
            (round[0], round[^1]) = (round[^1], round[0]);
        }

        return round;
    }

    private List<string> shuffle(IEnumerable<string> ids)
    {
        var list = ids.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }
}
