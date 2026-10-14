using HomeSpeaker.Server2.Services.Photos;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeSpeaker.Server2.Tests.Photos;

public sealed class PhotoCacheStoreTests : IDisposable
{
    private const string idA = "11111111-1111-4111-8111-111111111111";
    private readonly TempDirectory temp = new();

    private PhotoCacheStore createStore() => new(temp.Path, NullLogger<PhotoCacheStore>.Instance);

    public void Dispose() => temp.Dispose();

    [Fact]
    public async Task RelativeRoot_IsResolvedToAbsolutePaths()
    {
        var relativeRoot = "hs-rel-" + Guid.NewGuid().ToString("N");
        try
        {
            var store = new PhotoCacheStore(relativeRoot, NullLogger<PhotoCacheStore>.Instance);
            await store.SaveImageAsync(idA, s => s.WriteAsync(new byte[] { 1 }).AsTask());

            Assert.True(Path.IsPathRooted(store.RootPath));
            Assert.True(store.TryGetImagePath(idA, out var path));
            Assert.True(Path.IsPathRooted(path));
        }
        finally
        {
            if (Directory.Exists(relativeRoot))
            {
                Directory.Delete(relativeRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void LoadManifest_WhenMissing_ReturnsEmpty() => Assert.Empty(createStore().LoadManifest());

    [Fact]
    public void SaveManifest_ThenLoad_RoundTrips()
    {
        var store = createStore();
        var entry = new PhotoEntry
        {
            AssetId = idA,
            TakenAt = new DateTime(2021, 8, 14, 13, 22, 5),
            City = "Garden City",
            State = "Utah",
            ImageWidth = 1440,
            ImageHeight = 1080,
            Faces = [new FaceBox(0.1, 0.2, 0.3, 0.4)],
        };

        store.SaveManifest([entry]);
        var loaded = Assert.Single(createStore().LoadManifest());

        Assert.Equal(entry.AssetId, loaded.AssetId);
        Assert.Equal(entry.TakenAt, loaded.TakenAt);
        Assert.Equal("Garden City", loaded.City);
        Assert.Equal(1440, loaded.ImageWidth);
        Assert.Equal(new FaceBox(0.1, 0.2, 0.3, 0.4), Assert.Single(loaded.Faces));
        Assert.False(File.Exists(Path.Combine(temp.Path, "manifest.json.tmp")));
    }

    [Fact]
    public void LoadManifest_WhenCorrupt_ReturnsEmpty()
    {
        File.WriteAllText(Path.Combine(temp.Path, "manifest.json"), "{ not json");
        Assert.Empty(createStore().LoadManifest());
    }

    [Fact]
    public async Task ListCachedImageIds_ReturnsOnlyValidJpgs()
    {
        var store = createStore();
        await store.SaveImageAsync(idA, s => s.WriteAsync(new byte[] { 1 }).AsTask());
        await File.WriteAllBytesAsync(Path.Combine(temp.Path, "not-a-guid.jpg"), [1]);
        await File.WriteAllBytesAsync(Path.Combine(temp.Path, "22222222-2222-4222-8222-222222222222.jpg.tmp"), [1]);
        await File.WriteAllTextAsync(Path.Combine(temp.Path, "manifest.json"), "[]");

        Assert.Equal([idA], store.ListCachedImageIds());
    }

    [Fact]
    public async Task SaveImageAsync_WritesFile_AndTryGetImagePathFindsIt()
    {
        var store = createStore();

        await store.SaveImageAsync(idA, s => s.WriteAsync(new byte[] { 9, 9 }).AsTask());

        Assert.True(store.HasImage(idA));
        Assert.True(store.TryGetImagePath(idA, out var path));
        Assert.Equal([9, 9], await File.ReadAllBytesAsync(path!));
        Assert.Empty(Directory.GetFiles(temp.Path, "*.tmp"));
    }

    [Fact]
    public async Task SaveImageAsync_WhenWriterThrows_LeavesNoFile()
    {
        var store = createStore();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.SaveImageAsync(idA, _ => throw new InvalidOperationException("network died")));

        Assert.False(store.HasImage(idA));
        Assert.Empty(Directory.GetFiles(temp.Path));
    }

    [Theory]
    [InlineData("../manifest")]
    [InlineData("manifest")]
    [InlineData("")]
    public void TryGetImagePath_RejectsNonGuidIds(string id)
    {
        File.WriteAllText(Path.Combine(temp.Path, "manifest.jpg"), "x");
        Assert.False(createStore().TryGetImagePath(id, out _));
    }

    [Fact]
    public async Task DeleteImage_RemovesFile()
    {
        var store = createStore();
        await store.SaveImageAsync(idA, s => s.WriteAsync(new byte[] { 1 }).AsTask());

        store.DeleteImage(idA);

        Assert.False(store.HasImage(idA));
    }

    [Fact]
    public void AddHidden_PersistsAcrossInstances()
    {
        createStore().AddHidden(idA);
        Assert.Contains(idA, createStore().LoadHidden());
    }

    [Fact]
    public async Task SaveVideoAsync_PublishesTheFileOnlyOnceComplete()
    {
        var store = createStore();

        await store.SaveVideoAsync(idA, ".mp4", async s =>
        {
            await s.WriteAsync(new byte[] { 1, 2 });
            Assert.False(store.HasVideo(idA));
            Assert.True(File.Exists(Path.Combine(store.VideosPath, idA + ".mp4.tmp")));
        });

        Assert.True(store.TryGetVideo(idA, out var path, out var contentType));
        Assert.Equal(Path.Combine(store.VideosPath, idA + ".mp4"), path);
        Assert.Equal("video/mp4", contentType);
        Assert.Equal([idA], store.ListCachedVideoIds());
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public async Task SaveVideoAsync_WhenWritingFails_LeavesNoFileBehind()
    {
        var store = createStore();

        await Assert.ThrowsAsync<IOException>(() => store.SaveVideoAsync(idA, ".mp4", async s =>
        {
            await s.WriteAsync(new byte[] { 1 });
            throw new IOException("disk full");
        }));

        Assert.False(store.HasVideo(idA));
        Assert.Empty(Directory.GetFiles(store.VideosPath));
    }

    [Fact]
    public async Task SaveVideoAsync_RejectsAnInvalidAssetId() =>
        await Assert.ThrowsAsync<ArgumentException>(() => createStore().SaveVideoAsync("../evil", ".mp4", _ => Task.CompletedTask));

    [Theory]
    [InlineData("video/mp4", ".mp4")]
    [InlineData("Video/MP4; codecs=avc1", ".mp4")]
    [InlineData("video/quicktime", ".mov")]
    [InlineData("video/webm", ".webm")]
    [InlineData("application/octet-stream", null)]
    [InlineData(null, null)]
    public void VideoExtensionFor_MapsPlayableTypes(string? contentType, string? expected) =>
        Assert.Equal(expected, PhotoCacheStore.VideoExtensionFor(contentType));

    [Fact]
    public async Task TryGetVideo_ReturnsTheRecordedTypeForTheStoredExtension()
    {
        var store = createStore();
        await store.SaveVideoAsync(idA, ".mov", s => s.WriteAsync(new byte[] { 1 }).AsTask());

        Assert.True(store.TryGetVideo(idA, out _, out var contentType));
        Assert.Equal("video/quicktime", contentType);
        Assert.False(store.TryGetVideo("not-a-guid", out _, out _));
    }

    [Fact]
    public async Task TryDeleteVideo_RemovesTheFile()
    {
        var store = createStore();
        await store.SaveVideoAsync(idA, ".mp4", s => s.WriteAsync(new byte[] { 1 }).AsTask());

        Assert.True(store.TryDeleteVideo(idA));

        Assert.False(store.HasVideo(idA));
    }

    [Fact]
    public async Task DeleteStrayVideoFiles_RemovesTempAndUnknownFilesButKeepsCompletedVideos()
    {
        var store = createStore();
        await store.SaveVideoAsync(idA, ".mp4", s => s.WriteAsync(new byte[] { 1 }).AsTask());
        await File.WriteAllBytesAsync(Path.Combine(store.VideosPath, "22222222-2222-4222-8222-222222222222.mp4.tmp"), [1]);
        await File.WriteAllBytesAsync(Path.Combine(store.VideosPath, "junk.mp4"), [1]);
        await File.WriteAllBytesAsync(Path.Combine(store.VideosPath, "notes.txt"), [1]);

        store.DeleteStrayVideoFiles();

        Assert.Equal([Path.Combine(store.VideosPath, idA + ".mp4")], Directory.GetFiles(store.VideosPath));
    }

    [Fact]
    public void VideoFolderQueries_WhenFolderDoesNotExist_AreEmpty()
    {
        var store = createStore();

        Assert.Empty(store.ListCachedVideoIds());
        Assert.False(store.HasVideo(idA));
        store.DeleteStrayVideoFiles();
    }
}
