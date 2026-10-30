namespace HomeSpeaker.Server2.Services.AlbumArt;

/// <summary>Fetches a chosen cover over http(s), at most <see cref="MaxBytes"/>, and accepts it only if its bytes are a JPEG or PNG.</summary>
public sealed class AlbumArtDownloader
{
    public const int MaxBytes = 5 * 1024 * 1024;

    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<AlbumArtDownloader> logger;

    public AlbumArtDownloader(IHttpClientFactory httpClientFactory, ILogger<AlbumArtDownloader> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.logger = logger;
    }

    public async Task<AlbumArtImage> DownloadAsync(string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new AlbumArtDownloadException("That isn't an http or https link.");
        }

        var client = httpClientFactory.CreateClient(AlbumArtHttp.DownloadClientName);
        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new AlbumArtDownloadException("Couldn't download the image: " + ex.Message, ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new AlbumArtDownloadException($"The image server answered {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            if (response.Content.Headers.ContentLength > MaxBytes)
            {
                throw new AlbumArtDownloadException(tooLarge());
            }

            var bytes = await readCappedAsync(response.Content, cancellationToken).ConfigureAwait(false);
            var mimeType = AlbumArtImage.DetectMimeType(bytes);
            if (mimeType is null)
            {
                logger.LogWarning("Rejected album art from {Url}: {Length} bytes of {ContentType}, not a JPEG or PNG", uri, bytes.Length, response.Content.Headers.ContentType?.MediaType);
                throw new AlbumArtDownloadException("That link isn't a JPEG or PNG image.");
            }

            return new AlbumArtImage(bytes, mimeType);
        }
    }

    private static async Task<byte[]> readCappedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + read > MaxBytes)
                {
                    throw new AlbumArtDownloadException(tooLarge());
                }

                await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            return buffer.ToArray();
        }
    }

    private static string tooLarge() => $"That image is over {MaxBytes / (1024 * 1024)} MB.";
}
