namespace HomeSpeaker.Server2.Services.Photos;

/// <summary>Where slideshow photos come from: an Immich album or a local folder.</summary>
public interface IPhotoSource
{
    bool IsConfigured { get; }
    Task<IReadOnlyList<ImmichAsset>> ListAssetsAsync(CancellationToken cancellationToken);
    Task DownloadPreviewAsync(string assetId, Stream destination, CancellationToken cancellationToken);
    Task<IReadOnlyList<ImmichFace>> GetFacesAsync(string assetId, CancellationToken cancellationToken);
}
