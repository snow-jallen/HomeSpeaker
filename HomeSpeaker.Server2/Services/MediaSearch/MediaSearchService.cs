using HomeSpeaker.Server2.Data;
using HomeSpeaker.Shared;
using Microsoft.EntityFrameworkCore;

namespace HomeSpeaker.Server2.Services.MediaSearch;

/// <summary>
/// Turns a spoken "play ..." into the playlist, radio stream, artist, album or song it
/// most likely means, and plays it in place of whatever is playing.
/// </summary>
public sealed class MediaSearchService
{
    private readonly MusicContext dbContext;
    private readonly Mp3Library library;
    private readonly RadioStreamService radioStreams;
    private readonly PlaylistService playlists;
    private readonly IMusicPlayer player;
    private readonly ILogger<MediaSearchService> logger;

    public MediaSearchService(
        MusicContext dbContext,
        Mp3Library library,
        RadioStreamService radioStreams,
        PlaylistService playlists,
        IMusicPlayer player,
        ILogger<MediaSearchService> logger)
    {
        this.dbContext = dbContext;
        this.library = library;
        this.radioStreams = radioStreams;
        this.playlists = playlists;
        this.player = player;
        this.logger = logger;
    }

    public async Task<MediaCandidate?> FindAsync(MediaQuery query, CancellationToken cancellationToken)
    {
        var match = MediaMatcher.Match(await getCandidatesAsync(cancellationToken), query);
        logger.LogInformation("Media search {Query} (kind {Kind}, artist {Artist}) matched {Match}",
            query.Text, query.Kind, query.Artist, match);
        return match;
    }

    /// <summary>Plays the best match, or returns null when nothing matched or it had nothing to play.</summary>
    public async Task<MediaCandidate?> PlayAsync(MediaQuery query, CancellationToken cancellationToken)
    {
        var match = await FindAsync(query, cancellationToken);
        if (match == null)
        {
            return null;
        }

        return await playAsync(match, cancellationToken) ? match : null;
    }

    private async Task<bool> playAsync(MediaCandidate match, CancellationToken cancellationToken)
    {
        switch (match.Kind)
        {
            case MediaKind.Playlist:
                await playlists.PlayPlaylistAsync(match.Key);
                return true;

            case MediaKind.Stream:
                var stream = await radioStreams.GetStreamByIdAsync(int.Parse(match.Key, System.Globalization.CultureInfo.InvariantCulture));
                if (stream == null)
                {
                    return false;
                }

                await radioStreams.IncrementPlayCountAsync(stream.Id);
                player.PlayStream(stream.Url, stream.Name);
                return true;

            case MediaKind.Artist:
                return playSongs(library.Songs
                    .Where(s => string.Equals(s.Artist, match.Key, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(_ => Random.Shared.Next()));

            case MediaKind.Album:
                return playSongs(library.Songs
                    .Where(s => string.Equals(s.Album, match.Key, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(s => s.Name, StringComparer.Ordinal));

            case MediaKind.Song:
                var songId = int.Parse(match.Key, System.Globalization.CultureInfo.InvariantCulture);
                return playSongs(library.Songs.Where(s => s.SongId == songId));

            default:
                return false;
        }
    }

    private bool playSongs(IEnumerable<Song> songs)
    {
        var list = songs.ToList();
        if (list.Count == 0)
        {
            return false;
        }

        player.ClearQueue();
        player.PlaySong(list[0]);
        foreach (var song in list.Skip(1))
        {
            player.EnqueueSong(song);
        }

        return true;
    }

    private async Task<List<MediaCandidate>> getCandidatesAsync(CancellationToken cancellationToken)
    {
        var candidates = new List<MediaCandidate>();

        var playlistNames = await dbContext.Playlists.AsNoTracking().Select(p => p.Name).ToListAsync(cancellationToken);
        candidates.AddRange(playlistNames.Select(name => new MediaCandidate(MediaKind.Playlist, name, name)));

        var streams = await radioStreams.GetAllStreamsAsync();
        candidates.AddRange(streams.Select(s => new MediaCandidate(
            MediaKind.Stream, s.Name, s.Id.ToString(System.Globalization.CultureInfo.InvariantCulture))));

        var songs = library.Songs.ToList();
        candidates.AddRange(songs
            .Where(s => !string.IsNullOrWhiteSpace(s.Artist))
            .Select(s => s.Artist!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(artist => new MediaCandidate(MediaKind.Artist, artist, artist)));
        candidates.AddRange(songs
            .Where(s => !string.IsNullOrWhiteSpace(s.Album))
            .GroupBy(s => s.Album!, StringComparer.OrdinalIgnoreCase)
            .Select(album =>
            {
                var artists = album.Select(s => s.Artist).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                return new MediaCandidate(MediaKind.Album, album.Key, album.Key, artists.Count == 1 ? artists[0] : null);
            }));
        candidates.AddRange(songs.Select(s => new MediaCandidate(
            MediaKind.Song, s.Name, s.SongId.ToString(System.Globalization.CultureInfo.InvariantCulture), s.Artist)));

        return candidates;
    }
}
