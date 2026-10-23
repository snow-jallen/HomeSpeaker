using Microsoft.Extensions.Options;

namespace HomeSpeaker.Server2.Services.Photos;

public sealed class ImmichPhotoSource(IImmichClient client, IOptions<ImmichOptions> options) : IPhotoSource
{
    public bool IsConfigured => options.Value.IsConfigured;

    public Task<IReadOnlyList<ImmichAsset>> ListAssetsAsync(CancellationToken cancellationToken) =>
        client.GetAlbumAssetsAsync(options.Value.AlbumId!, cancellationToken);

    public Task DownloadPreviewAsync(string assetId, Stream destination, CancellationToken cancellationToken) =>
        client.DownloadPreviewAsync(assetId, destination, cancellationToken);

    public Task<IReadOnlyList<ImmichFace>> GetFacesAsync(string assetId, CancellationToken cancellationToken) =>
        client.GetFacesAsync(assetId, cancellationToken);
}
