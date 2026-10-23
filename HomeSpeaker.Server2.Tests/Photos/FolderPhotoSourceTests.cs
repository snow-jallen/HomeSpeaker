using HomeSpeaker.Server2.Services.Photos;

namespace HomeSpeaker.Server2.Tests.Photos;

public sealed class FolderPhotoSourceTests : IDisposable
{
    private readonly TempDirectory temp = new();

    public void Dispose() => temp.Dispose();

    private string write(string relative, byte[]? bytes = null)
    {
        var path = Path.Combine(temp.Path, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes ?? TestJpeg.Header(4, 3));
        return path;
    }

    [Fact]
    public async Task List_FindsJpgAndJpegRecursively_CaseInsensitive()
    {
        write("a.jpg");
        write("b.JPEG");
        write("sub/c.Jpg");
        write("sub/deeper/d.jpeg");

        var assets = await new FolderPhotoSource(temp.Path).ListAssetsAsync(CancellationToken.None);

        Assert.Equal(4, assets.Count);
        Assert.All(assets, a => Assert.False(a.IsVideo));
    }

    [Fact]
    public async Task List_IgnoresPngHeicAndDotfiles()
    {
        write("a.jpg");
        write("b.png");
        write("c.heic");
        write(".hidden.jpg");
        write("sub/.d.jpeg");

        var assets = await new FolderPhotoSource(temp.Path).ListAssetsAsync(CancellationToken.None);

        Assert.Single(assets);
    }

    [Fact]
    public async Task List_SpacesAndUnicodeNames_Work()
    {
        write("Summer trip/Café été.jpg");
        write("日本/写真 1.jpeg");

        var assets = await new FolderPhotoSource(temp.Path).ListAssetsAsync(CancellationToken.None);

        Assert.Equal(2, assets.Count);
    }

    [Fact]
    public async Task AssetId_IsStableAcrossInstances_AndValidGuid()
    {
        write("sub/Photo.jpg");

        var first = await new FolderPhotoSource(temp.Path).ListAssetsAsync(CancellationToken.None);
        var second = await new FolderPhotoSource(temp.Path).ListAssetsAsync(CancellationToken.None);

        Assert.Equal(first[0].Id, second[0].Id);
        Assert.True(PhotoCacheStore.IsValidAssetId(first[0].Id));
    }

    [Fact]
    public async Task AssetId_DiffersForSameNameInDifferentFolders()
    {
        write("one/photo.jpg");
        write("two/photo.jpg");

        var assets = await new FolderPhotoSource(temp.Path).ListAssetsAsync(CancellationToken.None);

        Assert.Equal(2, assets.Select(a => a.Id).Distinct().Count());
    }

    [Fact]
    public async Task List_MissingFolder_ReturnsEmpty_AndIsConfiguredFalse()
    {
        var source = new FolderPhotoSource(Path.Combine(temp.Path, "nope"));

        Assert.False(source.IsConfigured);
        Assert.Empty(await source.ListAssetsAsync(CancellationToken.None));
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "nope")));
    }

    [Fact]
    public async Task DownloadPreview_CopiesBytes()
    {
        var bytes = TestJpeg.Header(10, 20);
        write("p.jpg", bytes);
        var source = new FolderPhotoSource(temp.Path);
        var assets = await source.ListAssetsAsync(CancellationToken.None);

        using var destination = new MemoryStream();
        await source.DownloadPreviewAsync(assets[0].Id, destination, CancellationToken.None);

        Assert.Equal(bytes, destination.ToArray());
    }

    [Fact]
    public async Task DownloadPreview_UnknownId_Throws()
    {
        write("p.jpg");
        var source = new FolderPhotoSource(temp.Path);
        await source.ListAssetsAsync(CancellationToken.None);

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            source.DownloadPreviewAsync(Guid.NewGuid().ToString(), new MemoryStream(), CancellationToken.None));
    }
}
