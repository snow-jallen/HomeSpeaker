using HomeSpeaker.Server2.Services.Photos;

namespace HomeSpeaker.Server2.Tests.Photos;

internal sealed class FakeImmichClient : IImmichClient
{
    public List<ImmichAsset> Assets { get; } = [];
    public Exception? AlbumError { get; set; }
    public HashSet<string> FailingDownloads { get; } = new(StringComparer.Ordinal);
    public HashSet<string> TimedOutDownloads { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, List<ImmichFace>> Faces { get; } = new(StringComparer.Ordinal);
    public List<string> Downloaded { get; } = [];
    public List<string> FacesRequested { get; } = [];

    public Task<IReadOnlyList<ImmichAsset>> GetAlbumAssetsAsync(string albumId, CancellationToken cancellationToken) =>
        AlbumError is not null
            ? Task.FromException<IReadOnlyList<ImmichAsset>>(AlbumError)
            : Task.FromResult<IReadOnlyList<ImmichAsset>>(Assets.ToList());

    public async Task DownloadPreviewAsync(string assetId, Stream destination, CancellationToken cancellationToken)
    {
        if (FailingDownloads.Contains(assetId))
        {
            throw new HttpRequestException("download failed");
        }

        if (TimedOutDownloads.Contains(assetId))
        {
            // Mirrors HttpClient.Timeout: a TaskCanceledException while the caller's token is NOT cancelled.
            throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout elapsing.");
        }

        Downloaded.Add(assetId);
        await destination.WriteAsync(new byte[] { 0xFF, 0xD8 }, cancellationToken);
    }

    public Task<IReadOnlyList<ImmichFace>> GetFacesAsync(string assetId, CancellationToken cancellationToken)
    {
        FacesRequested.Add(assetId);
        return Task.FromResult<IReadOnlyList<ImmichFace>>(Faces.TryGetValue(assetId, out var faces) ? faces : []);
    }

    public Func<string?, CancellationToken, Task<HttpResponseMessage>>? OpenVideo { get; set; }

    public Task<HttpResponseMessage> OpenVideoAsync(string assetId, string? rangeHeader, CancellationToken cancellationToken) =>
        OpenVideo?.Invoke(rangeHeader, cancellationToken) ?? throw new NotSupportedException();
}
