using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using HomeSpeaker.Server2.Settings;
using HomeSpeaker.Shared.Anchors;

namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>
/// Client for an external anchor feed (docs/anchor-feed.md), configured with <c>Anchors:Feed:*</c>. Reads the last 92 days
/// and keeps them for a minute; when the feed doesn't answer it serves the last good copy, marked stale.
/// </summary>
public sealed class HttpAnchorFeed : IDisposable
{
    public const string HttpClientName = "anchors-feed";

    /// <summary>The longest range the contract allows; streaks look back this far for feed anchors.</summary>
    public const int RangeDays = 92;

    private static readonly TimeSpan freshFor = TimeSpan.FromSeconds(60);

    /// <summary>After a failed read, how long to keep answering from memory before asking the feed again.</summary>
    private static readonly TimeSpan quietAfterFailure = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory httpClientFactory;
    private readonly IConfiguration configuration;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<HttpAnchorFeed> logger;
    private readonly Lock gate = new();
    private readonly SemaphoreSlim fetchGate = new(1, 1);
    private (string Key, FeedPayload Payload, DateTimeOffset At)? last;
    private (DateTimeOffset At, string Error)? lastFailure;

    public HttpAnchorFeed(IHttpClientFactory httpClientFactory, IConfiguration configuration, TimeProvider timeProvider, ILogger<HttpAnchorFeed> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.configuration = configuration;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    public bool IsConfigured => baseUrl() is not null;

    public string Name => configuration[SettingKeys.AnchorsFeedName]?.Trim() is { Length: > 0 } name ? name : "Feed";

    /// <summary>The feed's last 92 days ending <paramref name="today"/>, or null when no feed is configured.</summary>
    public async Task<FeedSnapshot?> GetAsync(DateOnly today, CancellationToken cancellationToken)
    {
        if (baseUrl() is not { } feed)
        {
            return null;
        }

        var path = $"anchors?from={wire(today.AddDays(-(RangeDays - 1)))}&to={wire(today)}";
        var key = feed.AbsoluteUri + path;
        if (fromMemory(key) is { } remembered)
        {
            return remembered;
        }

        // One read at a time: the pages, the Home card and a tick can all ask at once, and a slow feed shouldn't get
        // a pile of identical requests.
        await fetchGate.WaitAsync(cancellationToken);
        try
        {
            if (fromMemory(key) is { } meanwhile)
            {
                return meanwhile;
            }

            return await fetchAsync(feed, path, key, cancellationToken);
        }
        finally
        {
            fetchGate.Release();
        }
    }

    public void Dispose() => fetchGate.Dispose();

    /// <summary>A fresh copy, or, shortly after a failure, the last good copy (or "unreachable") without asking again.</summary>
    private FeedSnapshot? fromMemory(string key)
    {
        var now = timeProvider.GetUtcNow();
        lock (gate)
        {
            if (last is { } cached && cached.Key == key && now - cached.At < freshFor)
            {
                return new FeedSnapshot(cached.Payload, new AnchorFeedStatusDto(Name, "ok", cached.At));
            }

            if (lastFailure is { } failure && now - failure.At < quietAfterFailure)
            {
                return failedSnapshot(failure.Error);
            }
        }

        return null;
    }

    private async Task<FeedSnapshot> fetchAsync(Uri feed, string path, string key, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        try
        {
            using var client = createClient();
            using var response = await client.GetAsync(new Uri(feed, path), cancellationToken);
            response.EnsureSuccessStatusCode();
            var payload = await JsonSerializer.DeserializeAsync<FeedPayload>(await response.Content.ReadAsStreamAsync(cancellationToken), json, cancellationToken)
                ?? throw new JsonException("The feed sent an empty body.");
            // An anchor without a start date can't be placed on any day (and would send streaks back to year 1): skip it.
            payload = payload with
            {
                People = payload.People ?? [],
                Anchors = [.. (payload.Anchors ?? []).Where(a => a.ActiveFrom != default)],
                Logs = payload.Logs ?? [],
            };
            lock (gate)
            {
                last = (key, payload, now);
                lastFailure = null;
            }

            return new FeedSnapshot(payload, new AnchorFeedStatusDto(Name, "ok", now));
        }
        catch (Exception ex) when (isFailure(ex, cancellationToken))
        {
            logger.LogWarning(ex, "Anchor feed {Name} didn't answer", Name);
            lock (gate)
            {
                lastFailure = (now, ex.Message);
                return failedSnapshot(ex.Message);
            }
        }
    }

    /// <summary>The last good copy marked stale, or "unreachable" when there never was one. Call under <see cref="gate"/>.</summary>
    private FeedSnapshot failedSnapshot(string error) =>
        last is { } previous
            ? new FeedSnapshot(previous.Payload, new AnchorFeedStatusDto(Name, "stale", previous.At, error))
            : new FeedSnapshot(null, new AnchorFeedStatusDto(Name, "unreachable", null, error));

    /// <summary>Forget the fresh copy so the next read asks the feed (after a tick it accepted).</summary>
    public void Expire()
    {
        lock (gate)
        {
            if (last is { } cached)
            {
                last = cached with { At = DateTimeOffset.MinValue };
            }
        }
    }

    public async Task<FeedWriteOutcome> WriteAsync(string feedAnchorId, DateOnly date, bool done, DateTimeOffset clientUpdatedAt, CancellationToken cancellationToken)
    {
        if (baseUrl() is not { } feed)
        {
            return FeedWriteOutcome.Failed;
        }

        try
        {
            using var client = createClient();
            using var content = JsonContent.Create(new { done, clientUpdatedAt }, options: json);
            using var response = await client.PutAsync(new Uri(feed, $"anchors/{Uri.EscapeDataString(feedAnchorId)}/days/{wire(date)}"), content, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return FeedWriteOutcome.Sent;
            }

            // 401/403 is a token problem someone can fix, and 408/429 are "later": keep those queued.
            var code = (int)response.StatusCode;
            return code is >= 400 and < 500 and not (401 or 403 or 408 or 429) ? FeedWriteOutcome.Rejected : FeedWriteOutcome.Failed;
        }
        catch (Exception ex) when (isFailure(ex, cancellationToken))
        {
            logger.LogWarning(ex, "Anchor feed {Name} didn't take a tick for {AnchorId}", Name, feedAnchorId);
            return FeedWriteOutcome.Failed;
        }
    }

    private Uri? baseUrl()
    {
        var text = configuration[SettingKeys.AnchorsFeedUrl]?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (!text.EndsWith('/'))
        {
            text += "/";
        }

        return Uri.TryCreate(text, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) ? uri : null;
    }

    private HttpClient createClient()
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        if (configuration[SettingKeys.AnchorsFeedToken]?.Trim() is { Length: > 0 } token)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }

    private static string wire(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static bool isFailure(Exception ex, CancellationToken cancellationToken) =>
        ex is HttpRequestException or JsonException or NotSupportedException
        || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested);
}
