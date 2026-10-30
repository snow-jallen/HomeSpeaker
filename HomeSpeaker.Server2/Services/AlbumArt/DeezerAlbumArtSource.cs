namespace HomeSpeaker.Server2.Services.AlbumArt;

/// <summary>
/// Deezer's public search API (no key). Albums come from <c>/search/album</c>, single songs from <c>/search/track</c>
/// using the song's album cover. Deezer reports errors as HTTP 200 with an <c>error</c> object.
/// </summary>
public sealed class DeezerAlbumArtSource : IAlbumArtSource, IDisposable
{
    public const string SourceName = "Deezer";
    private const int ResultLimit = 10;

    private static readonly TimeSpan minInterval = TimeSpan.FromMilliseconds(200);

    private readonly IHttpClientFactory httpClientFactory;
    private readonly RequestThrottle throttle;

    public DeezerAlbumArtSource(IHttpClientFactory httpClientFactory, TimeProvider timeProvider)
    {
        this.httpClientFactory = httpClientFactory;
        throttle = new RequestThrottle(minInterval, timeProvider);
    }

    public string Name => SourceName;

    /// <summary>
    /// Plain "artist name" text: Deezer's advanced <c>artist:"…" track:"…"</c> syntax returned nothing from
    /// <c>/search/track</c> and covers by other artists first for "Thriller" when tried in October 2026.
    /// </summary>
    public static Uri BuildUri(AlbumArtQuery query)
    {
        var text = string.Join(' ', new[] { query.Artist, query.Name }.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()));
        var path = query.IsAlbum ? "album" : "track";
        return new Uri($"https://api.deezer.com/search/{path}?q={Uri.EscapeDataString(text)}&limit={ResultLimit}");
    }

    public async Task<IReadOnlyList<AlbumArtCandidate>> SearchAsync(AlbumArtQuery query, CancellationToken cancellationToken)
    {
        await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
        var client = httpClientFactory.CreateClient(AlbumArtHttp.LookupClientName);
        using var document = await AlbumArtJson.GetAsync(client, BuildUri(query), cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        if (AlbumArtJson.Object(root, "error") is { } error)
        {
            throw new HttpRequestException("Deezer error: " + (AlbumArtJson.String(error, "message") ?? error.ToString()));
        }

        var candidates = new List<AlbumArtCandidate>();
        foreach (var item in AlbumArtJson.Array(root, "data"))
        {
            // An album result carries the cover itself; a track result carries it on its album.
            var album = query.IsAlbum ? item : AlbumArtJson.Object(item, "album") ?? default;
            var cover = AlbumArtJson.String(album, "cover_xl") ?? AlbumArtJson.String(album, "cover_big");
            if (string.IsNullOrWhiteSpace(cover))
            {
                continue;
            }

            var artist = AlbumArtJson.Object(item, "artist") is { } artistElement ? AlbumArtJson.String(artistElement, "name") : null;
            candidates.Add(new AlbumArtCandidate(
                SourceName,
                cover,
                AlbumArtJson.String(album, "cover_medium") ?? cover,
                artist,
                AlbumArtJson.String(album, "title"),
                query.IsAlbum ? null : AlbumArtJson.String(item, "title")));
        }

        return candidates;
    }

    public void Dispose() => throttle.Dispose();
}
