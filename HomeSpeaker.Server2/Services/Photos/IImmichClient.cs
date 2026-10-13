namespace HomeSpeaker.Server2.Services.Photos;

public interface IImmichClient
{
    Task<IReadOnlyList<ImmichAsset>> GetAlbumImagesAsync(string albumId, CancellationToken cancellationToken);
    Task DownloadPreviewAsync(string assetId, Stream destination, CancellationToken cancellationToken);
    Task<IReadOnlyList<ImmichFace>> GetFacesAsync(string assetId, CancellationToken cancellationToken);
}
