using System.Net;
using HomeSpeaker.Server2.Services.Photos;
using Microsoft.AspNetCore.Http;

namespace HomeSpeaker.Server2.Tests.Photos;

public sealed class VideoProxyTests
{
    [Fact]
    public async Task CopyAsync_CopiesStatusHeadersAndBody()
    {
        byte[] bytes = [1, 2, 3, 4, 5];
        using var upstream = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(bytes) };
        upstream.Content.Headers.ContentType = new("video/mp4");
        upstream.Content.Headers.ContentRange = new(0, 4, 100);
        upstream.Headers.TryAddWithoutValidation("Accept-Ranges", "bytes");
        var context = new DefaultHttpContext();
        using var body = new MemoryStream();
        context.Response.Body = body;

        await VideoProxy.CopyAsync(upstream, context.Response, CancellationToken.None);

        Assert.Equal(StatusCodes.Status206PartialContent, context.Response.StatusCode);
        Assert.Equal("video/mp4", context.Response.Headers.ContentType.ToString());
        Assert.Equal(5, context.Response.ContentLength);
        Assert.Equal("bytes 0-4/100", context.Response.Headers.ContentRange.ToString());
        Assert.Equal("bytes", context.Response.Headers.AcceptRanges.ToString());
        Assert.Equal(bytes, body.ToArray());
    }

    [Fact]
    public async Task CopyAsync_OmitsHeadersTheUpstreamDidNotSend()
    {
        using var upstream = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new MemoryStream([9])) };
        var context = new DefaultHttpContext();
        using var body = new MemoryStream();
        context.Response.Body = body;

        await VideoProxy.CopyAsync(upstream, context.Response, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.False(context.Response.Headers.ContainsKey("Content-Range"));
        Assert.False(context.Response.Headers.ContainsKey("Accept-Ranges"));
        Assert.Equal([9], body.ToArray());
    }
}
