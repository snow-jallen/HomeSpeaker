namespace HomeSpeaker.Server2.Services;

public static class RadioStreamOrdering
{
    public static IEnumerable<T> ByPopularity<T>(IEnumerable<T> streams, Func<T, int> playCount, Func<T, string> name)
    {
        return streams
            .OrderByDescending(playCount)
            .ThenBy(name, StringComparer.OrdinalIgnoreCase);
    }
}
