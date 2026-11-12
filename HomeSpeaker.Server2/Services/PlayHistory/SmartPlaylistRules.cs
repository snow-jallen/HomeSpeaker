namespace HomeSpeaker.Server2.Services.PlayHistory;

/// <summary>
/// Builds the automatic playlists from play history. Pure, so it can be tested without a database.
/// <list type="bullet">
/// <item><b>Most Loved</b>: played to the end at least <see cref="LovedMinimumPlays"/> times and rarely skipped.</item>
/// <item><b>Rediscover</b>: liked once (played at least <see cref="RediscoverMinimumPlays"/> times, rarely
/// skipped) but not heard for <see cref="RediscoverAfter"/>.</item>
/// </list>
/// </summary>
public static class SmartPlaylistRules
{
    public const string MostLovedKind = "most-loved";
    public const string RediscoverKind = "rediscover";
    public const string MostLovedName = "Most Loved";
    public const string RediscoverName = "Rediscover";

    public const int LovedMinimumPlays = 3;
    public const int RediscoverMinimumPlays = 2;
    public const double MaximumSkipRate = 0.25;
    public const int MaximumSongs = 100;

    /// <summary>An automatic playlist is only created once it has this many songs.</summary>
    public const int MinimumSongsToCreate = 10;

    public static readonly TimeSpan RediscoverAfter = TimeSpan.FromDays(120);

    public static IReadOnlyList<SmartPlaylist> Build(IEnumerable<PlayRecord> history, ISet<string> librarySongPaths, DateTime nowUtc)
    {
        var stats = history
            .Where(play => librarySongPaths.Contains(play.SongPath))
            .GroupBy(play => play.SongPath)
            .Select(group => new SongStats(
                group.Key,
                Plays: group.Count(play => !play.Skipped),
                Skips: group.Count(play => play.Skipped),
                LastPlayedUtc: group.Where(play => !play.Skipped).Select(play => (DateTime?)play.TimestampUtc).Max()))
            .Where(song => song.Plays > 0 && song.SkipRate <= MaximumSkipRate)
            .ToList();

        var loved = stats
            .Where(song => song.Plays >= LovedMinimumPlays)
            .OrderByDescending(song => song.Plays)
            .ThenByDescending(song => song.LastPlayedUtc)
            .ThenBy(song => song.Path, StringComparer.Ordinal)
            .Take(MaximumSongs)
            .Select(song => song.Path)
            .ToList();

        var rediscover = stats
            .Where(song => song.Plays >= RediscoverMinimumPlays && nowUtc - song.LastPlayedUtc >= RediscoverAfter)
            .OrderByDescending(song => song.Plays)
            .ThenBy(song => song.LastPlayedUtc)
            .ThenBy(song => song.Path, StringComparer.Ordinal)
            .Take(MaximumSongs)
            .Select(song => song.Path)
            .ToList();

        return
        [
            new SmartPlaylist(MostLovedKind, MostLovedName, loved),
            new SmartPlaylist(RediscoverKind, RediscoverName, rediscover),
        ];
    }

    private sealed record SongStats(string Path, int Plays, int Skips, DateTime? LastPlayedUtc)
    {
        public double SkipRate => (double)Skips / (Plays + Skips);
    }
}
