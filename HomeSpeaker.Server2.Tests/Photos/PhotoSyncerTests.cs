using HomeSpeaker.Server2.Services.Photos;
using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HomeSpeaker.Server2.Tests.Photos;

public sealed class PhotoSyncerTests : IDisposable
{
    private const string IdA = "11111111-1111-4111-8111-111111111111";
    private const string IdB = "22222222-2222-4222-8222-222222222222";
    private readonly TempDirectory temp = new();
    private readonly FakeImmichClient immich = new();
    private readonly PhotoCacheStore store;
    private readonly PhotoLibrary library;

    public PhotoSyncerTests()
    {
        store = new PhotoCacheStore(temp.Path, NullLogger<PhotoCacheStore>.Instance);
        library = new PhotoLibrary(store, new FixedTimeProvider(new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero)), new Random(1));
    }

    public void Dispose() => temp.Dispose();

    private PhotoSyncer createSyncer(ILogger<PhotoSyncer>? logger = null) => new(
        immich,
        store,
        library,
        Options.Create(new ImmichOptions { BaseUrl = "https://immich.test", ApiKey = "key", AlbumId = "album" }),
        logger ?? NullLogger<PhotoSyncer>.Instance);

    private static ImmichAsset asset(string id, string? city = "Bear Lake") => new(id, new DateTime(2021, 8, 14), city, "Utah", null);

    private static ImmichAsset videoAsset(string id, int durationMs, string? city = "Bear Lake") =>
        new(id, new DateTime(2021, 8, 14), city, "Utah", null, IsVideo: true, DurationMs: durationMs);

    [Fact]
    public async Task Sync_VideoIsRecordedButNeverDownloadedOrFaceChecked()
    {
        immich.Assets.AddRange([asset(IdA), videoAsset(IdB, 12345)]);

        Assert.True(await createSyncer().SyncAsync(CancellationToken.None));

        Assert.Equal([IdA], immich.Downloaded);
        Assert.Equal([IdA], immich.FacesRequested);
        Assert.False(store.HasImage(IdB));
        var video = Assert.Single(store.LoadManifest(), p => string.Equals(p.AssetId, IdB, StringComparison.Ordinal));
        Assert.True(video.IsVideo);
        Assert.Equal(12345, video.DurationMs);
        Assert.Equal(0, video.ImageWidth);
        Assert.Empty(video.Faces);
        Assert.Equal(2, library.Count);
    }

    [Fact]
    public async Task Sync_ExistingVideoIsKeptAndMetadataRefreshed()
    {
        immich.Assets.Add(videoAsset(IdB, 1000));
        await createSyncer().SyncAsync(CancellationToken.None);

        immich.Assets[0] = videoAsset(IdB, 2000, city: "Garden City");
        await createSyncer().SyncAsync(CancellationToken.None);

        var video = Assert.Single(store.LoadManifest());
        Assert.Equal(2000, video.DurationMs);
        Assert.Equal("Garden City", video.City);
        Assert.True(video.IsVideo);
        Assert.Empty(immich.Downloaded);
    }

    [Fact]
    public async Task Sync_RemovesVideoThatLeftTheAlbum()
    {
        immich.Assets.AddRange([asset(IdA), videoAsset(IdB, 1000)]);
        await createSyncer().SyncAsync(CancellationToken.None);

        immich.Assets.RemoveAll(a => string.Equals(a.Id, IdB, StringComparison.Ordinal));
        await createSyncer().SyncAsync(CancellationToken.None);

        Assert.Equal([IdA], store.LoadManifest().Select(p => p.AssetId));
        Assert.False(library.IsVideo(IdB));
    }

    [Fact]
    public async Task Sync_DownloadsNewAssets_AndNormalizesFaces()
    {
        immich.Assets.Add(asset(IdA));
        immich.Faces[IdA] = [new ImmichFace(100, 50, 300, 250, 1000, 500)];

        Assert.True(await createSyncer().SyncAsync(CancellationToken.None));

        var entry = Assert.Single(store.LoadManifest());
        Assert.True(store.HasImage(IdA));
        Assert.Equal(1000, entry.ImageWidth);
        Assert.Equal(500, entry.ImageHeight);
        Assert.Equal(new FaceBox(0.1, 0.1, 0.3, 0.5), Assert.Single(entry.Faces));
        Assert.Equal(1, library.Count);
    }

    [Fact]
    public async Task Sync_RemovesAssetsNoLongerInAlbum()
    {
        immich.Assets.AddRange([asset(IdA), asset(IdB)]);
        await createSyncer().SyncAsync(CancellationToken.None);

        immich.Assets.RemoveAll(a => string.Equals(a.Id, IdB, StringComparison.Ordinal));
        await createSyncer().SyncAsync(CancellationToken.None);

        Assert.Equal([IdA], store.LoadManifest().Select(p => p.AssetId));
        Assert.False(store.HasImage(IdB));
        Assert.Equal(1, library.Count);
    }

    [Fact]
    public async Task Sync_DoesNotRedownloadCachedAssets_ButRefreshesMetadata()
    {
        immich.Assets.Add(asset(IdA));
        await createSyncer().SyncAsync(CancellationToken.None);
        immich.Downloaded.Clear();

        immich.Assets[0] = asset(IdA, city: "Garden City");
        await createSyncer().SyncAsync(CancellationToken.None);

        Assert.Empty(immich.Downloaded);
        Assert.Equal("Garden City", Assert.Single(store.LoadManifest()).City);
    }

    [Fact]
    public async Task Sync_WhenAlbumFetchFails_KeepsExistingCache()
    {
        immich.Assets.Add(asset(IdA));
        await createSyncer().SyncAsync(CancellationToken.None);

        immich.AlbumError = new HttpRequestException("401 Unauthorized");
        Assert.False(await createSyncer().SyncAsync(CancellationToken.None));

        Assert.Equal([IdA], store.LoadManifest().Select(p => p.AssetId));
        Assert.True(store.HasImage(IdA));
    }

    [Fact]
    public async Task Sync_SkipsAssetWhoseDownloadFails()
    {
        immich.Assets.AddRange([asset(IdA), asset(IdB)]);
        immich.FailingDownloads.Add(IdB);

        Assert.True(await createSyncer().SyncAsync(CancellationToken.None));

        Assert.Equal([IdA], store.LoadManifest().Select(p => p.AssetId));
        Assert.False(store.HasImage(IdB));
        Assert.Empty(Directory.GetFiles(temp.Path, "*.tmp"));
    }

    [Fact]
    public async Task Sync_SkipsAssetWhoseDownloadTimesOut_AndStillSavesTheRest()
    {
        immich.Assets.AddRange([asset(IdA), asset(IdB)]);
        immich.TimedOutDownloads.Add(IdA);

        Assert.True(await createSyncer().SyncAsync(CancellationToken.None));

        Assert.Equal([IdB], store.LoadManifest().Select(p => p.AssetId));
        Assert.False(store.HasImage(IdA));
        Assert.Empty(Directory.GetFiles(temp.Path, "*.tmp"));
    }

    [Fact]
    public async Task Sync_WhenAlbumEmptied_RemovesEverything()
    {
        immich.Assets.Add(asset(IdA));
        await createSyncer().SyncAsync(CancellationToken.None);

        immich.Assets.Clear();
        await createSyncer().SyncAsync(CancellationToken.None);

        Assert.Empty(store.LoadManifest());
        Assert.Null(library.Next());
    }

    [Fact]
    public async Task Sync_LogsHttpStatusWhenAssetDownloadFails()
    {
        immich.Assets.Add(asset(IdA));
        immich.FailingDownloads.Add(IdA);
        immich.DownloadError = new HttpRequestException("x", null, HttpStatusCode.Forbidden);
        var logger = new CapturingLogger<PhotoSyncer>();

        Assert.True(await createSyncer(logger).SyncAsync(CancellationToken.None));

        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains(IdA, warning.Message, StringComparison.Ordinal);
        Assert.Contains("HTTP Forbidden", warning.Message, StringComparison.Ordinal);
        Assert.Same(immich.DownloadError, warning.Exception);
    }

    [Fact]
    public async Task Sync_LogsAssetWithoutStatusWhenFailureIsNotHttp()
    {
        immich.Assets.Add(asset(IdA));
        immich.FailingDownloads.Add(IdA);
        var logger = new CapturingLogger<PhotoSyncer>();

        await createSyncer(logger).SyncAsync(CancellationToken.None);

        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains(IdA, warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("HTTP", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sync_LogsHttpStatusWhenAlbumFetchFails()
    {
        immich.AlbumError = new HttpRequestException("x", null, HttpStatusCode.Unauthorized);
        var logger = new CapturingLogger<PhotoSyncer>();

        Assert.False(await createSyncer(logger).SyncAsync(CancellationToken.None));

        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("HTTP Unauthorized", warning.Message, StringComparison.Ordinal);
    }
}
