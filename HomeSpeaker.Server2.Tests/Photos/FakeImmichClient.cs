using HomeSpeaker.Server2.Services.Photos;

namespace HomeSpeaker.Server2.Tests.Photos;

internal sealed class FakeImmichClient : IImmichClient
{
    public List<ImmichAsset> Assets { get; } = [];
    public Exception? AlbumError { get; set; }
    public HashSet<string> FailingDownloads { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, List<ImmichFace>> Faces { get; } = new(StringComparer.Ordinal);
    public List<string> Downloaded { get; } = [];

    public Task<IReadOnlyList<ImmichAsset>> GetAlbumImagesAsync(string albumId, CancellationToken cancellationToken) =>
        AlbumError is not null
            ? Task.FromException<IReadOnlyList<ImmichAsset>>(AlbumError)
            : Task.FromResult<IReadOnlyList<ImmichAsset>>(Assets.ToList());

    public async Task DownloadPreviewAsync(string assetId, Stream destination, CancellationToken cancellationToken)
    {
        if (FailingDownloads.Contains(assetId))
        {
            throw new HttpRequestException("download failed");
        }

        Downloaded.Add(assetId);
        await destination.WriteAsync(new byte[] { 0xFF, 0xD8 }, cancellationToken);
    }

    public Task<IReadOnlyList<ImmichFace>> GetFacesAsync(string assetId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ImmichFace>>(Faces.TryGetValue(assetId, out var faces) ? faces : []);
}
