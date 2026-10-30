using System.Net;
using System.Text;
using System.Text.Json;

namespace HomeSpeaker.Server2.Services.AlbumArt;

/// <summary>
/// MusicBrainz search plus the Cover Art Archive. Albums are found with a release search, single songs with a
/// recording search; each hit's release group gives a <c>coverartarchive.org/release-group/{id}/front-500</c> URL
/// (the group's chosen cover, so more hits have art than single releases do). Not every group has a cover, so
/// some of these URLs 404: the review page hides images that fail to load and Apply refuses anything that isn't
/// an image. MusicBrainz asks for a descriptive User-Agent and at most one request a second, and answers 503
/// when it is rate limiting, so a 503 is retried once.
/// </summary>
public sealed class MusicBrainzAlbumArtSource : IAlbumArtSource, IDisposable
{
    public const string SourceName = "MusicBrainz";
    private const int ResultLimit = 10;

    private static readonly TimeSpan minInterval = TimeSpan.FromMilliseconds(1100);

    private readonly IHttpClientFactory httpClientFactory;
    private readonly RequestThrottle throttle;

    public MusicBrainzAlbumArtSource(IHttpClientFactory httpClientFactory, TimeProvider timeProvider)
    {
        this.httpClientFactory = httpClientFactory;
        throttle = new RequestThrottle(minInterval, timeProvider);
    }

    public string Name => SourceName;

    public static Uri BuildUri(AlbumArtQuery query)
    {
        // dismax matches the words against release title and artist together; plain Lucene looks only at the title, so
        // "queen night at the opera" found other bands' "Night at the Opera" and none of Queen's (October 2026).
        if (query.IsFreeText)
        {
            return new Uri($"https://musicbrainz.org/ws/2/release/?query={Uri.EscapeDataString(query.Name)}&dismax=true&fmt=json&limit={ResultLimit}");
        }

        var parts = new List<string>(2)
        {
            query.IsAlbum ? $"release:\"{luceneQuoted(query.Album!)}\"" : $"recording:\"{luceneQuoted(query.Title ?? string.Empty)}\"",
        };
        if (!string.IsNullOrWhiteSpace(query.Artist))
        {
            parts.Add($"artist:\"{luceneQuoted(query.Artist)}\"");
        }

        var entity = query.IsAlbum ? "release" : "recording";
        return new Uri($"https://musicbrainz.org/ws/2/{entity}/?query={Uri.EscapeDataString(string.Join(" AND ", parts))}&fmt=json&limit={ResultLimit}");
    }

    public static string CoverUrl(string releaseGroupId, int size) => $"https://coverartarchive.org/release-group/{releaseGroupId}/front-{size}";

    public async Task<IReadOnlyList<AlbumArtCandidate>> SearchAsync(AlbumArtQuery query, CancellationToken cancellationToken)
    {
        using var document = await getAsync(BuildUri(query), cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;

        var candidates = new List<AlbumArtCandidate>();
        var seenGroups = new HashSet<string>(StringComparer.Ordinal);
        if (query.IsAlbum)
        {
            foreach (var release in AlbumArtJson.Array(root, "releases"))
            {
                addRelease(release, artistCredit(release), title: null);
            }
        }
        else
        {
            foreach (var recording in AlbumArtJson.Array(root, "recordings"))
            {
                var artist = artistCredit(recording);
                var title = AlbumArtJson.String(recording, "title");
                foreach (var release in AlbumArtJson.Array(recording, "releases"))
                {
                    addRelease(release, artistCredit(release) ?? artist, title);
                }
            }
        }

        return candidates;

        void addRelease(JsonElement release, string? artist, string? title)
        {
            var groupId = AlbumArtJson.Object(release, "release-group") is { } group ? AlbumArtJson.String(group, "id") : null;
            if (string.IsNullOrWhiteSpace(groupId) || !seenGroups.Add(groupId))
            {
                return;
            }

            candidates.Add(new AlbumArtCandidate(
                SourceName,
                CoverUrl(groupId, 500),
                CoverUrl(groupId, 250),
                artist,
                AlbumArtJson.String(release, "title"),
                title));
        }
    }

    public void Dispose() => throttle.Dispose();

    /// <summary>MusicBrainz answers 503 when it is rate limiting, even at one request a second; one retry after the next throttle slot usually works.</summary>
    private async Task<JsonDocument> getAsync(Uri uri, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(AlbumArtHttp.LookupClientName);
        await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await AlbumArtJson.GetAsync(client, uri, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            await throttle.WaitAsync(cancellationToken).ConfigureAwait(false);
            return await AlbumArtJson.GetAsync(client, uri, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>"Queen", or "Simon &amp; Garfunkel" from two credits joined by " &amp; ".</summary>
    private static string? artistCredit(JsonElement element)
    {
        var builder = new StringBuilder();
        foreach (var credit in AlbumArtJson.Array(element, "artist-credit"))
        {
            builder.Append(AlbumArtJson.String(credit, "name") ?? string.Empty).Append(AlbumArtJson.String(credit, "joinphrase") ?? string.Empty);
        }

        return builder.Length > 0 ? builder.ToString() : null;
    }

    private static string luceneQuoted(string value) =>
        value.Replace("\\", " ", StringComparison.Ordinal).Replace("\"", " ", StringComparison.Ordinal).Trim();
}
