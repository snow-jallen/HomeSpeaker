namespace HomeSpeaker.Server2.Services.AlbumArt;

/// <summary>An online catalog that can suggest cover art. Implementations throw on failure; <see cref="AlbumArtSearchService"/> isolates them.</summary>
public interface IAlbumArtSource
{
    string Name { get; }

    Task<IReadOnlyList<AlbumArtCandidate>> SearchAsync(AlbumArtQuery query, CancellationToken cancellationToken);
}
