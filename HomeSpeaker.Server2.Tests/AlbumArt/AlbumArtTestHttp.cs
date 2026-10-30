using System.Net;
using HomeSpeaker.Server2.Tests.Photos;

namespace HomeSpeaker.Server2.Tests.AlbumArt;

internal static class AlbumArtTestHttp
{
    public static (FakeHttpClientFactory Factory, FakeHttpMessageHandler Handler) Json(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new FakeHttpMessageHandler((_, _) => FakeHttpMessageHandler.Json(json, status));
        return (new FakeHttpClientFactory(new HttpClient(handler)), handler);
    }

    public static (FakeHttpClientFactory Factory, FakeHttpMessageHandler Handler) Respond(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new FakeHttpMessageHandler((request, _) => respond(request));
        return (new FakeHttpClientFactory(new HttpClient(handler)), handler);
    }
}
