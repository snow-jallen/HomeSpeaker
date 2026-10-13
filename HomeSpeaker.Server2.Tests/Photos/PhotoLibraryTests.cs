using HomeSpeaker.Server2.Services.Photos;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeSpeaker.Server2.Tests.Photos;

public sealed class PhotoLibraryTests : IDisposable
{
    private const string IdA = "11111111-1111-4111-8111-111111111111";
    private const string IdB = "22222222-2222-4222-8222-222222222222";
    private static readonly FixedTimeProvider today = new(new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero));
    private readonly TempDirectory temp = new();
    private readonly PhotoCacheStore store;

    public PhotoLibraryTests() => store = new PhotoCacheStore(temp.Path, NullLogger<PhotoCacheStore>.Instance);

    public void Dispose() => temp.Dispose();

    private Task addAsync(string id, string? city = "Bear Lake", bool withImage = true, params FaceBox[] faces) =>
        addEntryAsync(id, city, withImage, isVideo: false, durationMs: null, faces);

    private Task addVideoAsync(string id, int durationMs = 12000) =>
        addEntryAsync(id, "Bear Lake", withImage: false, isVideo: true, durationMs: durationMs);

    private async Task addEntryAsync(string id, string? city, bool withImage, bool isVideo, int? durationMs, params FaceBox[] faces)
    {
        if (withImage)
        {
            await store.SaveImageAsync(id, s => s.WriteAsync(new byte[] { 1 }).AsTask());
        }

        var entries = store.LoadManifest().ToList();
        entries.Add(new PhotoEntry
        {
            AssetId = id,
            TakenAt = new DateTime(2021, 8, 14),
            City = city,
            State = "Utah",
            ImageWidth = faces.Length > 0 ? 1024 : 0,
            ImageHeight = faces.Length > 0 ? 600 : 0,
            Faces = faces,
            IsVideo = isVideo,
            DurationMs = durationMs,
        });
        store.SaveManifest(entries);
    }

    private static string idFor(int n) => $"{n:x8}-0000-4000-8000-000000000000";

    /// <summary>Writes one manifest with the given counts; the first <paramref name="cached"/> images get a file on disk.</summary>
    private async Task seedAlbumAsync(int images, int videos, int cached)
    {
        var entries = new List<PhotoEntry>();
        for (var i = 0; i < images; i++)
        {
            var id = idFor(i + 1);
            if (i < cached)
            {
                await store.SaveImageAsync(id, s => s.WriteAsync(new byte[] { 1 }).AsTask());
            }

            entries.Add(new PhotoEntry { AssetId = id, TakenAt = new DateTime(2021, 8, 14) });
        }

        for (var i = 0; i < videos; i++)
        {
            entries.Add(new PhotoEntry { AssetId = idFor(10_000 + i), TakenAt = new DateTime(2021, 8, 14), IsVideo = true, DurationMs = 5000 });
        }

        store.SaveManifest(entries);
    }

    private int visibleVideoCount(PhotoLibrary library) =>
        store.LoadManifest().Count(p => p.IsVideo && library.IsVideo(p.AssetId));

    [Fact]
    public async Task Reload_SamplesVideosInProportionToPhotos()
    {
        await seedAlbumAsync(images: 400, videos: 40, cached: 40);

        var library = new PhotoLibrary(store, today, new Random(1), cacheSize: 40);

        Assert.Equal(4, visibleVideoCount(library));
        Assert.Equal(44, library.Count);
    }

    [Fact]
    public async Task Reload_WithFewVideosInBigAlbum_StillShowsAtLeastOneVideo()
    {
        await seedAlbumAsync(images: 1000, videos: 5, cached: 40);

        var library = new PhotoLibrary(store, today, new Random(1), cacheSize: 40);

        Assert.Equal(1, visibleVideoCount(library));
    }

    [Fact]
    public async Task Reload_VideosOnlyAlbum_ShowsCacheSizeVideos()
    {
        await seedAlbumAsync(images: 0, videos: 30, cached: 0);

        var library = new PhotoLibrary(store, today, new Random(1), cacheSize: 10);

        Assert.Equal(10, visibleVideoCount(library));
    }

    [Fact]
    public async Task Reload_VideosOnlyAlbum_SmallerThanCacheSize_ShowsAllVideos()
    {
        await seedAlbumAsync(images: 0, videos: 3, cached: 0);

        var library = new PhotoLibrary(store, today, new Random(1), cacheSize: 10);

        Assert.Equal(3, visibleVideoCount(library));
    }

    [Fact]
    public async Task Reload_NeverSamplesHiddenVideos()
    {
        await seedAlbumAsync(images: 0, videos: 30, cached: 0);
        var hidden = Enumerable.Range(0, 20).Select(i => idFor(10_000 + i)).ToList();
        foreach (var id in hidden)
        {
            store.AddHidden(id);
        }

        var library = new PhotoLibrary(store, today, new Random(1), cacheSize: 30);

        Assert.Equal(10, visibleVideoCount(library));
        for (var i = 0; i < 20; i++)
        {
            library.Reload();
            Assert.All(hidden, id => Assert.False(library.IsVideo(id)));
        }
    }

    [Fact]
    public async Task Reload_DrawsAFreshVideoSampleEachTime()
    {
        await seedAlbumAsync(images: 400, videos: 40, cached: 40);
        var library = new PhotoLibrary(store, today, new Random(1), cacheSize: 40);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < 10; i++)
        {
            library.Reload();
            Assert.Equal(4, visibleVideoCount(library));
            foreach (var video in store.LoadManifest().Where(p => p.IsVideo && library.IsVideo(p.AssetId)))
            {
                seen.Add(video.AssetId);
            }
        }

        Assert.True(seen.Count > 4, $"expected more than 4 distinct videos over 10 reloads, saw {seen.Count}");
    }

    [Fact]
    public async Task Next_ReturnsUrlCaptionAndLayout()
    {
        await addAsync(IdA, faces: [new FaceBox(0.75, 0.10, 0.90, 0.35)]);
        var library = new PhotoLibrary(store, today, new Random(1));

        var next = library.Next()!;

        Assert.Equal(IdA, next.Id);
        Assert.Equal($"/api/photos/{IdA}.jpg", next.Url);
        Assert.Equal("Bear Lake, Utah", next.Place);
        Assert.Equal("August 2021 · 5 years ago", next.When);
        Assert.Equal("mirrored", next.Layout);
        Assert.Equal("photo", next.Kind);
        Assert.Null(next.DurationMs);
    }

    [Fact]
    public async Task Reload_KeepsVideoEntryWithoutImageFile()
    {
        await addVideoAsync(IdA);

        var library = new PhotoLibrary(store, today, new Random(1));

        Assert.Equal(1, library.Count);
    }

    [Fact]
    public async Task Next_ForVideo_ReturnsVideoKindUrlAndDuration()
    {
        await addVideoAsync(IdA, 12345);
        var library = new PhotoLibrary(store, today, new Random(1));

        var next = library.Next()!;

        Assert.Equal("video", next.Kind);
        Assert.Equal($"/api/photos/{IdA}/video", next.Url);
        Assert.Equal("default", next.Layout);
        Assert.Equal(12345, next.DurationMs);
        Assert.Equal("Bear Lake, Utah", next.Place);
    }

    [Fact]
    public async Task IsVideo_TrueOnlyForVisibleVideos()
    {
        const string hiddenId = "33333333-3333-4333-8333-333333333333";
        await addVideoAsync(IdA);
        await addAsync(IdB);
        await addVideoAsync(hiddenId);
        var library = new PhotoLibrary(store, today, new Random(1));
        library.Hide(hiddenId);

        Assert.True(library.IsVideo(IdA));
        Assert.False(library.IsVideo(IdB));
        Assert.False(library.IsVideo("44444444-4444-4444-8444-444444444444"));
        Assert.False(library.IsVideo(hiddenId));
    }

    [Fact]
    public async Task Reload_SkipsEntriesWhoseImageIsMissing()
    {
        await addAsync(IdA);
        await addAsync(IdB, withImage: false);

        var library = new PhotoLibrary(store, today, new Random(1));

        Assert.Equal(1, library.Count);
    }

    [Fact]
    public async Task Hide_RemovesPhoto_AndPersists()
    {
        await addAsync(IdA);
        await addAsync(IdB);
        var library = new PhotoLibrary(store, today, new Random(1));

        Assert.True(library.Hide(IdA));

        Assert.Equal(1, library.Count);
        Assert.Equal(IdB, new PhotoLibrary(store, today, new Random(1)).Next()!.Id);
    }

    [Fact]
    public void Hide_UnknownOrInvalidId_ReturnsFalse()
    {
        var library = new PhotoLibrary(store, today, new Random(1));
        Assert.False(library.Hide(IdA));
        Assert.False(library.Hide("../x"));
    }

    [Fact]
    public async Task Next_WhenOnlyPhotoHidden_ReturnsNull()
    {
        await addAsync(IdA);
        var library = new PhotoLibrary(store, today, new Random(1));

        library.Hide(IdA);

        Assert.Null(library.Next());
    }

    [Fact]
    public async Task Next_UsesLocalTimeZoneForDateCaption()
    {
        var utcInstant = new DateTimeOffset(2026, 8, 14, 3, 0, 0, TimeSpan.Zero);
        var mountainTimeZone = TimeZoneInfo.CreateCustomTimeZone("UTC-7", TimeSpan.FromHours(-7), "UTC-7", "UTC-7");
        var timeProvider = new FixedTimeProvider(utcInstant, mountainTimeZone);

        await addAsync(IdA, faces: [new FaceBox(0.75, 0.10, 0.90, 0.35)]);
        var library = new PhotoLibrary(store, timeProvider, new Random(1));

        var next = library.Next()!;

        Assert.Equal("August 2021 · 4 years ago", next.When);
    }
}
