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
    private string? last;

    public void SetAvailable(IEnumerable<string> ids)
    {
        available = new HashSet<string>(ids, StringComparer.Ordinal);
        shownThisRound.IntersectWith(available);
        pending = shuffle(available.Where(id => !shownThisRound.Contains(id)));
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
            pending = shuffle(available);
            if (pending.Count > 1 && string.Equals(pending[0], last, StringComparison.Ordinal))
            {
                (pending[0], pending[^1]) = (pending[^1], pending[0]);
            }
        }

        var id = pending[0];
        pending.RemoveAt(0);
        shownThisRound.Add(id);
        last = id;
        return id;
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
