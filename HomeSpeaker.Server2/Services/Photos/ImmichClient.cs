using System.Globalization;
using System.Text.Json;

namespace HomeSpeaker.Server2.Services.Photos;

public sealed class ImmichClient(HttpClient http, IHttpClientFactory factory) : IImmichClient
{
    /// <summary>Name of the registered HttpClient with no overall timeout, used only for long video streams.</summary>
    public const string VideoClientName = "ImmichVideo";

    private const int PageSize = 250;
    private const int MaxPages = 200;
    private const string ImageType = "IMAGE";
    private const string VideoType = "VIDEO";
    private static readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ImmichAsset>> GetAlbumAssetsAsync(string albumId, CancellationToken cancellationToken)
    {
        var results = new List<ImmichAsset>();
        var page = 1;
        for (var i = 0; i < MaxPages; i++)
        {
            // The search "type" filter is deprecated as of Immich v3.2, so images and videos are filtered client-side.
            var request = new SearchRequest([albumId], WithExif: true, Size: PageSize, Page: page);
            using var response = await http.PostAsJsonAsync("api/search/metadata", request, json, cancellationToken);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<SearchResponse>(json, cancellationToken)
                ?? throw new InvalidOperationException("Immich returned an empty search response");

            foreach (var asset in body.Assets.Items)
            {
                var isVideo = string.Equals(asset.Type, VideoType, StringComparison.Ordinal);
                if (asset.IsTrashed || (!isVideo && !string.Equals(asset.Type, ImageType, StringComparison.Ordinal)))
                {
                    continue;
                }

                results.Add(new ImmichAsset(
                    asset.Id,
                    DateTime.SpecifyKind(asset.LocalDateTime, DateTimeKind.Unspecified),
                    blankToNull(asset.ExifInfo?.City),
                    blankToNull(asset.ExifInfo?.State),
                    blankToNull(asset.ExifInfo?.Country),
                    isVideo,
                    isVideo ? parseDurationMs(asset.Duration) : null));
            }

            if (string.IsNullOrEmpty(body.Assets.NextPage) ||
                !int.TryParse(body.Assets.NextPage, NumberStyles.Integer, CultureInfo.InvariantCulture, out page))
            {
                break;
            }
        }

        return results;
    }

    public async Task DownloadPreviewAsync(string assetId, Stream destination, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(
            $"api/assets/{Uri.EscapeDataString(assetId)}/thumbnail?size=preview",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await source.CopyToAsync(destination, cancellationToken);
    }

    public async Task<IReadOnlyList<ImmichFace>> GetFacesAsync(string assetId, CancellationToken cancellationToken)
    {
        var faces = await http.GetFromJsonAsync<List<FaceDto>>($"api/faces?id={Uri.EscapeDataString(assetId)}", json, cancellationToken) ?? [];
        return faces
            .Select(f => new ImmichFace(f.BoundingBoxX1, f.BoundingBoxY1, f.BoundingBoxX2, f.BoundingBoxY2, f.ImageWidth, f.ImageHeight))
            .ToList();
    }

    public async Task<HttpResponseMessage> OpenVideoAsync(string assetId, string? rangeHeader, CancellationToken cancellationToken)
    {
        // The typed client has a 30s overall Timeout that would cut off long streams, so videos use the named no-timeout client.
        var videoClient = factory.CreateClient(VideoClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/assets/{Uri.EscapeDataString(assetId)}/video/playback");
        if (rangeHeader is not null)
        {
            request.Headers.TryAddWithoutValidation("Range", rangeHeader);
        }

        return await videoClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private static int? parseDurationMs(JsonElement? duration)
    {
        if (duration is not { } element)
        {
            return null;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                return element.TryGetDouble(out var ms) && ms >= 0 && ms <= int.MaxValue ? (int)Math.Round(ms) : null;
            case JsonValueKind.String:
                // Older Immich versions send a .NET-style duration such as "0:00:12.345000".
                return TimeSpan.TryParse(element.GetString(), CultureInfo.InvariantCulture, out var ts) && ts >= TimeSpan.Zero && ts.TotalMilliseconds <= int.MaxValue
                    ? (int)Math.Round(ts.TotalMilliseconds)
                    : null;
            default:
                return null;
        }
    }

    private static string? blankToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private sealed record SearchRequest(string[] AlbumIds, bool WithExif, int Size, int Page);

    private sealed record SearchResponse(SearchAssets Assets);

    private sealed record SearchAssets(List<AssetDto> Items, string? NextPage);

    private sealed record AssetDto(string Id, string Type, DateTime LocalDateTime, bool IsTrashed, ExifDto? ExifInfo, JsonElement? Duration);

    private sealed record ExifDto(string? City, string? State, string? Country);

    private sealed record FaceDto(int BoundingBoxX1, int BoundingBoxY1, int BoundingBoxX2, int BoundingBoxY2, int ImageWidth, int ImageHeight);
}
