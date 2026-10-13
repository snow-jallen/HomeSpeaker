namespace HomeSpeaker.Server2.Services.Photos;

public interface IImmichClient
{
    Task<IReadOnlyList<ImmichAsset>> GetAlbumAssetsAsync(string albumId, CancellationToken cancellationToken);
    Task DownloadPreviewAsync(string assetId, Stream destination, CancellationToken cancellationToken);
    Task<IReadOnlyList<ImmichFace>> GetFacesAsync(string assetId, CancellationToken cancellationToken);

    /// <summary>
    /// Opens the video playback stream for proxying. The response is returned as-is (no status check) with only the
    /// headers read; the caller owns and must dispose it.
    /// </summary>
    Task<HttpResponseMessage> OpenVideoAsync(string assetId, string? rangeHeader, CancellationToken cancellationToken);
}
