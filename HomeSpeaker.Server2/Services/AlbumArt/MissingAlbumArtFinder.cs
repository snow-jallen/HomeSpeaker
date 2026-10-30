using System.Diagnostics;
using HomeSpeaker.Server2.Telemetry;
using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Services.AlbumArt;

/// <summary>
/// Finds library songs with no embedded picture and groups them for review. Checking means opening every MP3's
/// tags, so the result is cached until the library rescans, and each file's answer is cached by its last-write
/// time so a later check only reopens files that changed.
/// </summary>
public sealed class MissingAlbumArtFinder : IDisposable
{
    private readonly Mp3Library library;
    private readonly ILogger<MissingAlbumArtFinder> logger;
    private readonly SemaphoreSlim scanLock = new(1, 1);
    private readonly object stateLock = new();
    private readonly Dictionary<string, (DateTime WrittenUtc, bool HasArt)> fileCache = new(StringComparer.Ordinal);
    private IReadOnlyList<AlbumArtGroup>? groups;
    private long groupsGeneration = -1;

    public MissingAlbumArtFinder(Mp3Library library, ILogger<MissingAlbumArtFinder> logger)
    {
        this.library = library;
        this.logger = logger;
    }

    /// <summary>Songs checked by the most recent scan.</summary>
    public int LastScanSongCount { get; private set; }

    /// <summary>
    /// The groups of songs missing art, sorted by artist then name. Scans on a background thread when the library
    /// changed since the last scan, or always when <paramref name="rescan"/> is true (which also forgets the per-file cache).
    /// </summary>
    public async Task<IReadOnlyList<AlbumArtGroup>> GetGroupsAsync(bool rescan, CancellationToken cancellationToken)
    {
        await scanLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => getGroups(rescan, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            scanLock.Release();
        }
    }

    /// <summary>Drops songs that now have art from the cached groups (and groups left empty), without a rescan.</summary>
    public void ForgetSongs(IEnumerable<string> paths)
    {
        var fixedPaths = paths.ToHashSet(StringComparer.Ordinal);
        lock (stateLock)
        {
            foreach (var path in fixedPaths)
            {
                fileCache[path] = (lastWriteUtc(path), true);
            }

            groups = groups?
                .Select(group => group with { Songs = group.Songs.Where(song => !fixedPaths.Contains(song.Path)).ToList() })
                .Where(group => group.Songs.Count > 0)
                .ToList();
        }
    }

    public static IReadOnlyList<AlbumArtGroup> Group(IEnumerable<Song> songsMissingArt) =>
        songsMissingArt
            .Where(song => song.Path is not null)
            .Select(song => (Song: song, Artist: string.IsNullOrWhiteSpace(song.Artist) ? AlbumArtGroup.UnknownArtist : song.Artist.Trim()))
            .GroupBy(item => AlbumArtGroup.KeyFor(item.Artist, item.Song.Album, item.Song.Path!), StringComparer.Ordinal)
            .Select(group =>
            {
                var first = group.First();
                var songs = group
                    .Select(item => new AlbumArtSong(item.Song.Path!, item.Song.Name))
                    .OrderBy(song => song.Path, StringComparer.Ordinal)
                    .ToList();
                var album = string.IsNullOrWhiteSpace(first.Song.Album) ? null : first.Song.Album.Trim();
                return new AlbumArtGroup(group.Key, first.Artist, album, songs);
            })
            .OrderBy(group => group.Artist, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public void Dispose() => scanLock.Dispose();

    private IReadOnlyList<AlbumArtGroup> getGroups(bool rescan, CancellationToken cancellationToken)
    {
        // Reading Songs rescans the library first if it is dirty (another reader may be doing so; then this sees the previous list).
        var songs = library.Songs.ToList();
        var generation = library.Generation;
        lock (stateLock)
        {
            if (!rescan && groups is not null && generation == groupsGeneration)
            {
                return groups;
            }

            if (rescan)
            {
                fileCache.Clear();
            }
        }

        using var activity = HomeSpeakerTelemetry.ActivitySource.StartActivity("album-art.scan");
        var stopwatch = Stopwatch.StartNew();
        var missing = new List<Song>();
        var opened = 0;
        var unreadable = 0;
        foreach (var song in songs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (song.Path is null)
            {
                continue;
            }

            var written = lastWriteUtc(song.Path);
            if (written == DateTime.MinValue)
            {
                continue; // deleted since the library scanned it
            }

            bool hasArt;
            lock (stateLock)
            {
                if (fileCache.TryGetValue(song.Path, out var cached) && cached.WrittenUtc == written)
                {
                    if (!cached.HasArt)
                    {
                        missing.Add(song);
                    }

                    continue;
                }
            }

            try
            {
                opened++;
                hasArt = AlbumArtTags.HasPicture(song.Path);
            }
            catch (Exception ex)
            {
                // Leave unreadable files out: Apply could not write to them either.
                unreadable++;
                logger.LogDebug(ex, "Could not read tags of {Path} while looking for missing album art", song.Path);
                continue;
            }

            lock (stateLock)
            {
                fileCache[song.Path] = (written, hasArt);
            }

            if (!hasArt)
            {
                missing.Add(song);
            }
        }

        var result = Group(missing);
        lock (stateLock)
        {
            var current = songs.Where(song => song.Path is not null).Select(song => song.Path!).ToHashSet(StringComparer.Ordinal);
            foreach (var stale in fileCache.Keys.Where(path => !current.Contains(path)).ToList())
            {
                fileCache.Remove(stale);
            }

            groups = result;
            groupsGeneration = generation;
            LastScanSongCount = songs.Count;
        }

        activity?.SetTag("homespeaker.album_art.songs", songs.Count);
        activity?.SetTag("homespeaker.album_art.missing", missing.Count);
        logger.LogInformation(
            "Album art check: {Missing} of {Songs} songs have no picture ({Groups} groups); opened {Opened} files, {Unreadable} unreadable, {Seconds:F1}s",
            missing.Count, songs.Count, result.Count, opened, unreadable, stopwatch.Elapsed.TotalSeconds);
        return result;
    }

    private static DateTime lastWriteUtc(string path)
    {
        try
        {
            return System.IO.File.Exists(path) ? System.IO.File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
        }
        catch (IOException)
        {
            return DateTime.MinValue;
        }
        catch (UnauthorizedAccessException)
        {
            return DateTime.MinValue;
        }
    }
}
