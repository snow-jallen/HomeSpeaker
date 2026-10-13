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
}
