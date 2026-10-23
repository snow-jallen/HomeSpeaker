using System.Text.Json;

namespace HomeSpeaker.Server2.Services.Photos;

/// <summary>
/// Lists the albums on an Immich server for the settings page, using the URL and key the user typed rather
/// than the saved ones (so it works before saving). Uses the <see cref="ClientName"/> client, which has no base
/// address.
/// </summary>
public sealed class ImmichAlbumBrowser(IHttpClientFactory httpClientFactory)
{
    public const string ClientName = "ImmichAlbumBrowser";

    /// <exception cref="ArgumentException">The URL is not an absolute http(s) URL.</exception>
    /// <exception cref="HttpRequestException">Immich could not be reached or refused the key.</exception>
    public async Task<IReadOnlyList<ImmichAlbum>> GetAlbumsAsync(string baseUrl, string apiKey, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(baseUrl?.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var root) ||
            (root.Scheme != Uri.UriSchemeHttp && root.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Enter the Immich address, like https://immich.example.com", nameof(baseUrl));
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(root, "api/albums"));
        request.Headers.Add("x-api-key", apiKey);

        var client = httpClientFactory.CreateClient(ClientName);
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var albums = new List<ImmichAlbum>();
        if (json.RootElement.ValueKind != JsonValueKind.Array)
        {
            return albums;
        }

        foreach (var album in json.RootElement.EnumerateArray())
        {
            if (album.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String && id.GetString() is { Length: > 0 } albumId)
            {
                var name = album.TryGetProperty("albumName", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() ?? "" : "";
                var count = album.TryGetProperty("assetCount", out var c) && c.TryGetInt32(out var assets) ? assets : 0;
                albums.Add(new ImmichAlbum(albumId, name, count));
            }
        }

        return albums;
    }
}
