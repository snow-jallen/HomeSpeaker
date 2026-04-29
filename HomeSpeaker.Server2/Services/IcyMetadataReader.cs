using System.Text;
using System.Text.RegularExpressions;

namespace HomeSpeaker.Server2.Services;

public sealed partial class IcyMetadataReader : IDisposable
{
    private CancellationTokenSource? cts;
    private readonly ILogger<IcyMetadataReader> logger;

    public event Action<string>? TitleChanged;

    public IcyMetadataReader(ILogger<IcyMetadataReader> logger)
    {
        this.logger = logger;
    }

    public void Start(string streamUrl)
    {
        Stop();
        cts = new CancellationTokenSource();
        _ = Task.Run(() => readAsync(streamUrl, cts.Token), cts.Token);
    }

    public void Stop()
    {
        cts?.Cancel();
        cts?.Dispose();
        cts = null;
    }

    private async Task readAsync(string streamUrl, CancellationToken ct)
    {
        try
        {
            using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
            client.DefaultRequestHeaders.Add("Icy-MetaData", "1");

            using var response = await client.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, streamUrl),
                HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.Headers.TryGetValues("icy-metaint", out var values)
                || !int.TryParse(values.FirstOrDefault(), out var metaint)
                || metaint <= 0)
            {
                logger.LogInformation("Stream {Url} does not support ICY metadata", streamUrl);
                return;
            }

            logger.LogInformation("Reading ICY metadata from {Url} with metaint={Metaint}", streamUrl, metaint);

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            var skipBuffer = new byte[4096];
            var lengthBuf = new byte[1];
            string? lastTitle = null;

            while (!ct.IsCancellationRequested)
            {
                // Skip metaint bytes of audio data
                var remaining = metaint;
                while (remaining > 0)
                {
                    var toRead = Math.Min(remaining, skipBuffer.Length);
                    var read = await stream.ReadAsync(skipBuffer.AsMemory(0, toRead), ct);
                    if (read == 0)
                    {
                        return;
                    }

                    remaining -= read;
                }

                // Read the 1-byte metadata length indicator (actual length = byte * 16)
                if (await stream.ReadAsync(lengthBuf.AsMemory(0, 1), ct) == 0)
                {
                    return;
                }

                var metaLength = lengthBuf[0] * 16;

                if (metaLength == 0)
                {
                    continue;
                }

                // Read the metadata block
                var metaBuffer = new byte[metaLength];
                var metaOffset = 0;
                while (metaOffset < metaLength)
                {
                    var read = await stream.ReadAsync(metaBuffer.AsMemory(metaOffset, metaLength - metaOffset), ct);
                    if (read == 0)
                    {
                        return;
                    }

                    metaOffset += read;
                }

                var metadata = Encoding.UTF8.GetString(metaBuffer).TrimEnd('\0');
                var match = StreamTitleRegex().Match(metadata);
                if (!match.Success)
                {
                    continue;
                }

                var title = match.Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(title) || title == lastTitle)
                {
                    continue;
                }

                lastTitle = title;
                logger.LogInformation("ICY stream title changed: {Title}", title);
                TitleChanged?.Invoke(title);
            }
        }
        catch (OperationCanceledException) { /* expected on Stop() */ }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ICY metadata reader stopped for {Url}", streamUrl);
        }
    }

    [GeneratedRegex(@"StreamTitle='([^']*)'")]
    private static partial Regex StreamTitleRegex();

    public void Dispose() => Stop();
}