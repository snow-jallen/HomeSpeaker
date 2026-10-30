using System.Net;
using HomeSpeaker.Server2.Services.AlbumArt;
using HomeSpeaker.Server2.Tests.Photos;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeSpeaker.Server2.Tests.AlbumArt;

public class AlbumArtApplierTests
{
    private const string ImageUrl = "https://images.example/cover.jpg";

    private static readonly byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52];

    [Fact]
    public async Task ApplyAsync_EmbedsOnlyInGroupSongsThatLackArt_AndMarksLibraryDirty()
    {
        using var fixture = new AlbumArtLibraryFixture();
        var lacking1 = fixture.Add("album/01.mp3", "One", "Queen", "Jazz");
        var lacking2 = fixture.Add("album/02.mp3", "Two", "Queen", "Jazz");
        var otherAlbum = fixture.Add("other/01.mp3", "Other", "Queen", "News of the World");
        var group = Assert.Single(await fixture.Finder.GetGroupsAsync(false, CancellationToken.None), g => g.Album == "Jazz");

        // Art arrives on one song after the page listed it: Apply must leave that file alone.
        var original = TestJpeg.Header(100, 100);
        TestMp3.Create(fixture.Folder, "album/02.mp3", "Two", "Queen", "Jazz", original);
        var cover = TestJpeg.Header(600, 600);
        var applier = createApplier(fixture, imageResponse(cover, "image/jpeg"));

        var result = await applier.ApplyAsync(group, ImageUrl, CancellationToken.None);

        Assert.Equal([lacking1], result.Updated);
        Assert.Equal([lacking2], result.AlreadyHadArt);
        Assert.Empty(result.Failures);
        var embedded = Assert.Single(TestMp3.Pictures(lacking1));
        Assert.Equal(cover, embedded.Data.Data);
        Assert.Equal("image/jpeg", embedded.MimeType);
        Assert.Equal(TagLib.PictureType.FrontCover, embedded.Type);
        Assert.Equal(original, Assert.Single(TestMp3.Pictures(lacking2)).Data.Data);
        Assert.Empty(TestMp3.Pictures(otherAlbum));
        Assert.True(fixture.Library.IsDirty);

        var remaining = await fixture.Finder.GetGroupsAsync(false, CancellationToken.None);
        Assert.DoesNotContain(remaining, g => g.Album == "Jazz");
        Assert.Contains(remaining, g => g.Album == "News of the World");
    }

    [Fact]
    public async Task ApplyAsync_UploadedImage_EmbedsWithoutDownloading()
    {
        using var fixture = new AlbumArtLibraryFixture();
        var path = fixture.Add("a.mp3", "A", "Artist", "Album");
        var group = Assert.Single(await fixture.Finder.GetGroupsAsync(false, CancellationToken.None));
        var downloads = 0;
        var applier = createApplier(fixture, () =>
        {
            downloads++;
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var cover = TestJpeg.Header(400, 400);
        using var upload = new MemoryStream(cover);

        var result = await applier.ApplyAsync(group, await AlbumArtImage.FromUploadAsync(upload, CancellationToken.None), CancellationToken.None);

        Assert.Equal([path], result.Updated);
        Assert.Equal(cover, Assert.Single(TestMp3.Pictures(path)).Data.Data);
        Assert.Equal(0, downloads);
        Assert.True(fixture.Library.IsDirty);
    }

    [Fact]
    public async Task ApplyAsync_Png_IsEmbeddedAsPng()
    {
        using var fixture = new AlbumArtLibraryFixture();
        var path = fixture.Add("a.mp3", "A", "Artist", "Album");
        var group = Assert.Single(await fixture.Finder.GetGroupsAsync(false, CancellationToken.None));
        // Content type is wrong on purpose: the bytes decide.
        var applier = createApplier(fixture, imageResponse(png, "application/octet-stream"));

        await applier.ApplyAsync(group, ImageUrl, CancellationToken.None);

        Assert.Equal("image/png", Assert.Single(TestMp3.Pictures(path)).MimeType);
    }

    [Fact]
    public async Task ApplyAsync_NotAnImage_ThrowsAndWritesNothing()
    {
        using var fixture = new AlbumArtLibraryFixture();
        var path = fixture.Add("a.mp3", "A", "Artist", "Album");
        var group = Assert.Single(await fixture.Finder.GetGroupsAsync(false, CancellationToken.None));
        var html = "<html><body>Not found</body></html>"u8.ToArray();
        var applier = createApplier(fixture, imageResponse(html, "image/jpeg"));

        var ex = await Assert.ThrowsAsync<AlbumArtDownloadException>(() => applier.ApplyAsync(group, ImageUrl, CancellationToken.None));

        Assert.Contains("JPEG or PNG", ex.Message, StringComparison.Ordinal);
        Assert.Empty(TestMp3.Pictures(path));
        Assert.False(fixture.Library.IsDirty);
    }

    [Fact]
    public async Task ApplyAsync_DeclaredTooLarge_Throws()
    {
        using var fixture = new AlbumArtLibraryFixture();
        var path = fixture.Add("a.mp3", "A", "Artist", "Album");
        var group = Assert.Single(await fixture.Finder.GetGroupsAsync(false, CancellationToken.None));
        var response = imageResponse(TestJpeg.Header(1, 1), "image/jpeg");
        var applier = createApplier(fixture, () =>
        {
            var r = response();
            r.Content.Headers.ContentLength = AlbumArtDownloader.MaxBytes + 1;
            return r;
        });

        var ex = await Assert.ThrowsAsync<AlbumArtDownloadException>(() => applier.ApplyAsync(group, ImageUrl, CancellationToken.None));

        Assert.Contains("over 5 MB", ex.Message, StringComparison.Ordinal);
        Assert.Empty(TestMp3.Pictures(path));
    }

    [Fact]
    public async Task ApplyAsync_StreamLargerThanCap_Throws()
    {
        using var fixture = new AlbumArtLibraryFixture();
        fixture.Add("a.mp3", "A", "Artist", "Album");
        var group = Assert.Single(await fixture.Finder.GetGroupsAsync(false, CancellationToken.None));
        var huge = new byte[AlbumArtDownloader.MaxBytes + 10];
        TestJpeg.Header(1, 1).CopyTo(huge, 0);
        // No Content-Length, so the cap has to be enforced while reading.
        var applier = createApplier(fixture, () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new MemoryStream(huge)) });

        await Assert.ThrowsAsync<AlbumArtDownloadException>(() => applier.ApplyAsync(group, ImageUrl, CancellationToken.None));
    }

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("not a url")]
    [InlineData("")]
    public async Task ApplyAsync_NonHttpUrl_Throws(string url)
    {
        using var fixture = new AlbumArtLibraryFixture();
        fixture.Add("a.mp3", "A", "Artist", "Album");
        var group = Assert.Single(await fixture.Finder.GetGroupsAsync(false, CancellationToken.None));
        var applier = createApplier(fixture, imageResponse(TestJpeg.Header(1, 1), "image/jpeg"));

        await Assert.ThrowsAsync<AlbumArtDownloadException>(() => applier.ApplyAsync(group, url, CancellationToken.None));
    }

    [Fact]
    public async Task ApplyAsync_HttpError_Throws()
    {
        using var fixture = new AlbumArtLibraryFixture();
        fixture.Add("a.mp3", "A", "Artist", "Album");
        var group = Assert.Single(await fixture.Finder.GetGroupsAsync(false, CancellationToken.None));
        var applier = createApplier(fixture, () => new HttpResponseMessage(HttpStatusCode.NotFound));

        var ex = await Assert.ThrowsAsync<AlbumArtDownloadException>(() => applier.ApplyAsync(group, ImageUrl, CancellationToken.None));

        Assert.Contains("404", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyAsync_OneFileFails_ReportsItAndKeepsGoing()
    {
        using var fixture = new AlbumArtLibraryFixture();
        var good = fixture.Add("a/01.mp3", "One", "Artist", "Album");
        var broken = fixture.Add("a/02.mp3", "Two", "Artist", "Album");
        var group = Assert.Single(await fixture.Finder.GetGroupsAsync(false, CancellationToken.None));
        File.Delete(broken);
        var applier = createApplier(fixture, imageResponse(TestJpeg.Header(1, 1), "image/jpeg"));

        var result = await applier.ApplyAsync(group, ImageUrl, CancellationToken.None);

        Assert.Equal([good], result.Updated);
        Assert.Equal(broken, Assert.Single(result.Failures).Path);
        Assert.Single(TestMp3.Pictures(good));
        Assert.True(fixture.Library.IsDirty);
    }

    private static Func<HttpResponseMessage> imageResponse(byte[] bytes, string contentType) => () =>
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    };

    private static AlbumArtApplier createApplier(AlbumArtLibraryFixture fixture, Func<HttpResponseMessage> respond)
    {
        var (factory, _) = AlbumArtTestHttp.Respond(_ => respond());
        var downloader = new AlbumArtDownloader(factory, NullLogger<AlbumArtDownloader>.Instance);
        return new AlbumArtApplier(downloader, fixture.Library, fixture.Finder, NullLogger<AlbumArtApplier>.Instance);
    }
}
