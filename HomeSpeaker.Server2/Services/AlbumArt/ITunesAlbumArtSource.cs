using System.Text.RegularExpressions;

namespace HomeSpeaker.Server2.Services.AlbumArt;

/// <summary>
/// The iTunes Search API (no key). Its artwork URLs end in a size like <c>100x100bb.jpg</c>; any size can be asked for,
/// so they are rewritten to 600x600. Apple allows roughly 20 calls a minute, hence the throttle.
/// </summary>
public sealed partial class ITunesAlbumArtSource : IAlbumArtSource, IDisposable
{
    public const string SourceName = "iTunes";
    private const int ResultLimit = 10;

    private static readonly TimeSpan minInterval = TimeSpan.FromSeconds(3);

    private readonly IHttpClientFactory httpClientFactory;
    private readonly RequestThrottle throttle;

    public ITunesAlbumArtSource(IHttpClientFactory httpClientFactory, TimeProvider timeProvider)
    {
        this.httpClientFactory = httpClientFactory;
        throttle = new RequestThrottle(minInterval, timeProvider);
    }

    public string Name => SourceName;

    public static Uri BuildUri(AlbumArtQuery query)
    {
        var term = string.Join(' ', new[] { query.Artist, query.Name }.Where(part => !string.IsNullOrWhiteSpace(part)));
        var entity = query.IsAlbum ? "album" : "song";
        return new Uri($"https://itunes.apple.com/search?term={Uri.EscapeDataString(term)}&media=music&entity={entity}&limit={ResultLimit}");
    }

    /// <summary>Rewrites the size segment of an mzstatic artwork URL (<c>.../100x100bb.jpg</c>); other URLs are returned unchanged.</summary>
    public static string Resize(string artworkUrl, int size) => sizeSegment().Replace(artworkUrl, $"/{size}x{size}bb.", 1);

    public async Task<IReadOnlyList<AlbumArtCandidate>> SearchAsync(AlbumArtQuery query, CancellationToken cancellationToken)
    {
        await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
        var client = httpClientFactory.CreateClient(AlbumArtHttp.LookupClientName);
        using var document = await AlbumArtJson.GetAsync(client, BuildUri(query), cancellationToken).ConfigureAwait(false);

        var candidates = new List<AlbumArtCandidate>();
        foreach (var result in AlbumArtJson.Array(document.RootElement, "results"))
        {
            var artwork = AlbumArtJson.String(result, "artworkUrl100") ?? AlbumArtJson.String(result, "artworkUrl60");
            if (string.IsNullOrWhiteSpace(artwork))
            {
                continue;
            }

            candidates.Add(new AlbumArtCandidate(
                SourceName,
                Resize(artwork, 600),
                Resize(artwork, 200),
                AlbumArtJson.String(result, "artistName"),
                AlbumArtJson.String(result, "collectionName"),
                AlbumArtJson.String(result, "trackName")));
        }

        return candidates;
    }

    public void Dispose() => throttle.Dispose();

    [GeneratedRegex(@"/\d+x\d+bb\.")]
    private static partial Regex sizeSegment();
}
