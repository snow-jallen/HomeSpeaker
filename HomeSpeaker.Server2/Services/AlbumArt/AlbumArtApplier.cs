namespace HomeSpeaker.Server2.Services.AlbumArt;

/// <summary>
/// Embeds an admin-approved cover into a group's songs. Only songs that still have no picture are written,
/// one failure doesn't stop the rest, and the library is marked dirty once afterwards (its watcher ignores
/// in-place tag writes).
/// </summary>
public sealed class AlbumArtApplier
{
    private readonly AlbumArtDownloader downloader;
    private readonly Mp3Library library;
    private readonly MissingAlbumArtFinder finder;
    private readonly ILogger<AlbumArtApplier> logger;

    public AlbumArtApplier(AlbumArtDownloader downloader, Mp3Library library, MissingAlbumArtFinder finder, ILogger<AlbumArtApplier> logger)
    {
        this.downloader = downloader;
        this.library = library;
        this.finder = finder;
        this.logger = logger;
    }

    /// <summary>Downloads <paramref name="imageUrl"/> and embeds it. Throws <see cref="AlbumArtDownloadException"/> before touching any file if the image is unusable.</summary>
    public async Task<AlbumArtApplyResult> ApplyAsync(AlbumArtGroup group, string imageUrl, CancellationToken cancellationToken)
    {
        var image = await downloader.DownloadAsync(imageUrl, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Applying album art from {Url} ({Bytes} bytes, {MimeType}) to {Artist} / {Name}", imageUrl, image.Bytes.Length, image.MimeType, group.Artist, group.DisplayName);
        return await Task.Run(() => Embed(group, image), cancellationToken).ConfigureAwait(false);
    }

    public AlbumArtApplyResult Embed(AlbumArtGroup group, AlbumArtImage image)
    {
        var updated = new List<string>();
        var alreadyHadArt = new List<string>();
        var failures = new List<AlbumArtApplyFailure>();
        foreach (var song in group.Songs)
        {
            try
            {
                if (AlbumArtTags.HasPicture(song.Path))
                {
                    alreadyHadArt.Add(song.Path);
                    continue;
                }

                AlbumArtTags.EmbedFrontCover(song.Path, image);
                updated.Add(song.Path);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to embed album art in {Path}", song.Path);
                failures.Add(new AlbumArtApplyFailure(song.Path, ex.Message));
            }
        }

        if (updated.Count > 0)
        {
            library.MarkDirty("album-art-review");
        }

        finder.ForgetSongs(updated.Concat(alreadyHadArt));
        logger.LogInformation(
            "Album art for {Artist} / {Name}: embedded in {Updated} songs, {Skipped} already had art, {Failed} failed",
            group.Artist, group.DisplayName, updated.Count, alreadyHadArt.Count, failures.Count);
        return new AlbumArtApplyResult(updated, alreadyHadArt, failures);
    }
}
