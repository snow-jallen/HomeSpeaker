namespace HomeSpeaker.Server2.Services.Photos;

public static class VideoProxy
{
    /// <summary>Relays an upstream video response (status, range-related headers and body) to the client without buffering it.</summary>
    public static async Task CopyAsync(HttpResponseMessage upstream, HttpResponse response, CancellationToken ct)
    {
        response.StatusCode = (int)upstream.StatusCode;

        var content = upstream.Content.Headers;
        if (content.ContentType is { } contentType)
        {
            response.Headers.ContentType = contentType.ToString();
        }

        if (content.ContentLength is { } contentLength)
        {
            response.ContentLength = contentLength;
        }

        if (content.ContentRange is { } contentRange)
        {
            response.Headers.ContentRange = contentRange.ToString();
        }

        if (upstream.Headers.TryGetValues("Accept-Ranges", out var acceptRanges))
        {
            response.Headers.AcceptRanges = string.Join(", ", acceptRanges);
        }

        await upstream.Content.CopyToAsync(response.Body, ct);
    }
}
