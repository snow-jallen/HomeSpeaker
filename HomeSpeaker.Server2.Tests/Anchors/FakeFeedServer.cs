using System.Net;
using System.Text;

namespace HomeSpeaker.Server2.Tests.Anchors;

/// <summary>Plays an anchor feed's server: answers every request with one canned response, or fails like a dead network.</summary>
internal sealed class FakeFeedServer : HttpMessageHandler, IHttpClientFactory
{
    private HttpStatusCode status = HttpStatusCode.OK;
    private string body = string.Empty;
    private bool down;

    public List<SeenRequest> Requests { get; } = [];

    /// <summary>Every request that reached the server, including ones it failed.</summary>
    public int Attempts { get; private set; }

    /// <summary>Runs while a request is in flight, before the answer: lets a test change things mid-request.</summary>
    public Func<Task>? DuringRequest { get; set; }

    public void Respond(HttpStatusCode code, string content)
    {
        status = code;
        body = content;
        down = false;
    }

    public void Fail() => down = true;

    public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Attempts++;
        if (down)
        {
            throw new HttpRequestException("Connection refused");
        }

        if (DuringRequest is { } during)
        {
            await during();
        }

        var sent = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new SeenRequest(request.Method.Method, request.RequestUri!.AbsoluteUri, request.Headers.Authorization?.ToString(), sent));
        return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
