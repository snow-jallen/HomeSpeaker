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
