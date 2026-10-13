using System.Globalization;
using System.Text.Json;

namespace HomeSpeaker.Server2.Services.Photos;

public sealed class ImmichClient(HttpClient http) : IImmichClient
{
    private const int PageSize = 250;
    private const int MaxPages = 200;
    private static readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ImmichAsset>> GetAlbumImagesAsync(string albumId, CancellationToken cancellationToken)
    {
        var results = new List<ImmichAsset>();
        var page = 1;
        for (var i = 0; i < MaxPages; i++)
        {
            var request = new SearchRequest([albumId], "IMAGE", WithExif: true, Size: PageSize, Page: page);
            using var response = await http.PostAsJsonAsync("api/search/metadata", request, json, cancellationToken);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<SearchResponse>(json, cancellationToken)
                ?? throw new InvalidOperationException("Immich returned an empty search response");

            foreach (var asset in body.Assets.Items)
            {
                if (asset.IsTrashed || !string.Equals(asset.Type, "IMAGE", StringComparison.Ordinal))
                {
                    continue;
                }

                results.Add(new ImmichAsset(
                    asset.Id,
                    DateTime.SpecifyKind(asset.LocalDateTime, DateTimeKind.Unspecified),
                    blankToNull(asset.ExifInfo?.City),
                    blankToNull(asset.ExifInfo?.State),
                    blankToNull(asset.ExifInfo?.Country)));
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

    private static string? blankToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private sealed record SearchRequest(string[] AlbumIds, string Type, bool WithExif, int Size, int Page);

    private sealed record SearchResponse(SearchAssets Assets);

    private sealed record SearchAssets(List<AssetDto> Items, string? NextPage);

    private sealed record AssetDto(string Id, string Type, DateTime LocalDateTime, bool IsTrashed, ExifDto? ExifInfo);

    private sealed record ExifDto(string? City, string? State, string? Country);

    private sealed record FaceDto(int BoundingBoxX1, int BoundingBoxY1, int BoundingBoxX2, int BoundingBoxY2, int ImageWidth, int ImageHeight);
}
