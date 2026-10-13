namespace HomeSpeaker.Server2.Tests.Photos;

internal sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    public string? LastName { get; private set; }

    public HttpClient CreateClient(string name)
    {
        LastName = name;
        return client;
    }
}
