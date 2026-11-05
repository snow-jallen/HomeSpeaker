using System.Globalization;
using System.Net;
using System.Text.Json;
using HomeSpeaker.Shared.Anchors;

namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>
/// Anchors kept on another HomeSpeaker (the anchor home), reached through its anchor API, so every speaker in the house
/// shows the same anchors. Reads fall back to the last board the home sent when it doesn't answer; writes report
/// <see cref="AnchorResult.Unavailable"/> rather than queueing, because they happen while someone is looking.
/// </summary>
public sealed class RemoteAnchorStore : IAnchorStore
{
    public const string HttpClientName = "anchors-remote";

    private static readonly TimeSpan freshFor = TimeSpan.FromSeconds(15);

    /// <summary>After the home didn't answer, how long to serve the last board without asking again.</summary>
    private static readonly TimeSpan quietAfterFailure = TimeSpan.FromSeconds(30);

    private readonly IHttpClientFactory httpClientFactory;
    private readonly IConfiguration configuration;
    private readonly AnchorChangeNotifier notifier;
    private readonly RemoteAnchorCache cache;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<RemoteAnchorStore> logger;

    public RemoteAnchorStore(IHttpClientFactory httpClientFactory, IConfiguration configuration, AnchorChangeNotifier notifier, RemoteAnchorCache cache, TimeProvider timeProvider, ILogger<RemoteAnchorStore> logger)
    {
        this.timeProvider = timeProvider;
        this.httpClientFactory = httpClientFactory;
        this.configuration = configuration;
        this.notifier = notifier;
        this.cache = cache;
        this.logger = logger;
    }

    public async Task<AnchorBoardDto> GetBoardAsync(DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default)
    {
        var path = "api/anchors/board" + query(("from", from), ("to", to));
        var key = (configuration[Settings.SettingKeys.AnchorsRemoteUrl] ?? string.Empty) + "|" + path;
        var now = timeProvider.GetUtcNow();
        if (cache.TryGet(key, out var cached, out var at) && now - at < freshFor)
        {
            return cached;
        }

        if (cache.FailedSince(key, now - quietAfterFailure))
        {
            return cache.TryGet(key, out var last, out _)
                ? last with { Stale = true }
                : throw new AnchorSourceUnavailableException("The anchor home couldn't be reached.");
        }

        try
        {
            using var client = createClient(out var home);
            var board = await client.GetFromJsonAsync<AnchorBoardDto>(new Uri(home, path), cancellationToken)
                ?? throw new AnchorSourceUnavailableException("The anchor home sent an empty board.");
            cache.Set(key, board, now);
            return board;
        }
        catch (Exception ex) when (isUnreachable(ex, cancellationToken))
        {
            logger.LogWarning(ex, "Anchor home didn't answer for {Path}", path);
            cache.MarkFailed(key, now);
            if (cache.TryGet(key, out var last, out _))
            {
                return last with { Stale = true };
            }

            throw new AnchorSourceUnavailableException("The anchor home couldn't be reached.", ex);
        }
    }

    public async Task<AnchorPersonDto?> GetDayAsync(string personId, DateOnly? date = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = createClient(out var home);
            using var response = await client.GetAsync(new Uri(home, $"api/anchors/people/{Uri.EscapeDataString(personId)}/day" + query(("date", date))), cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<AnchorPersonDto>(cancellationToken);
        }
        catch (Exception ex) when (isUnreachable(ex, cancellationToken))
        {
            // Today's day is in the last board the home sent; better that (it says it's stale) than nothing.
            var todayKey = (configuration[Settings.SettingKeys.AnchorsRemoteUrl] ?? string.Empty) + "|api/anchors/board";
            if (cache.TryGet(todayKey, out var board, out _)
                && (date is null || date == board.Today)
                && board.People.FirstOrDefault(p => string.Equals(p.Id, personId, StringComparison.Ordinal)) is { } person)
            {
                return person;
            }

            throw new AnchorSourceUnavailableException("The anchor home couldn't be reached.", ex);
        }
    }

    public Task<AnchorOutcome<AnchorPersonDto>> CreatePersonAsync(AnchorPersonRequest request, CancellationToken cancellationToken = default) =>
        sendAsync<AnchorPersonDto>(HttpMethod.Post, "api/anchors/people", request, cancellationToken);

    public Task<AnchorResult> UpdatePersonAsync(string personId, AnchorPersonRequest request, CancellationToken cancellationToken = default) =>
        resultOf(HttpMethod.Put, $"api/anchors/people/{Uri.EscapeDataString(personId)}", request, cancellationToken);

    public Task<AnchorResult> OrderPeopleAsync(AnchorOrderRequest request, CancellationToken cancellationToken = default) =>
        resultOf(HttpMethod.Put, "api/anchors/people/order", request, cancellationToken);

    public Task<AnchorResult> ArchivePersonAsync(string personId, CancellationToken cancellationToken = default) =>
        resultOf(HttpMethod.Delete, $"api/anchors/people/{Uri.EscapeDataString(personId)}", null, cancellationToken);

    public Task<AnchorOutcome<AnchorDto>> CreateAnchorAsync(string personId, AnchorRequest request, CancellationToken cancellationToken = default) =>
        sendAsync<AnchorDto>(HttpMethod.Post, $"api/anchors/people/{Uri.EscapeDataString(personId)}/anchors", request, cancellationToken);

    public Task<AnchorResult> UpdateAnchorAsync(string anchorId, AnchorRequest request, CancellationToken cancellationToken = default) =>
        resultOf(HttpMethod.Put, $"api/anchors/anchors/{Uri.EscapeDataString(anchorId)}", request, cancellationToken);

    public Task<AnchorResult> OrderAnchorsAsync(string personId, AnchorOrderRequest request, CancellationToken cancellationToken = default) =>
        resultOf(HttpMethod.Put, $"api/anchors/people/{Uri.EscapeDataString(personId)}/anchors/order", request, cancellationToken);

    public Task<AnchorResult> RetireAnchorAsync(string anchorId, CancellationToken cancellationToken = default) =>
        resultOf(HttpMethod.Post, $"api/anchors/anchors/{Uri.EscapeDataString(anchorId)}/retire", null, cancellationToken);

    public Task<AnchorResult> RestoreAnchorAsync(string anchorId, CancellationToken cancellationToken = default) =>
        resultOf(HttpMethod.Post, $"api/anchors/anchors/{Uri.EscapeDataString(anchorId)}/restore", null, cancellationToken);

    public Task<AnchorResult> SetDoneAsync(string anchorId, DateOnly date, AnchorLogRequest request, CancellationToken cancellationToken = default) =>
        resultOf(HttpMethod.Put, $"api/anchors/anchors/{Uri.EscapeDataString(anchorId)}/days/{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}", request, cancellationToken);

    private async Task<AnchorResult> resultOf(HttpMethod method, string path, object? body, CancellationToken cancellationToken) =>
        (await sendAsync<object>(method, path, body, cancellationToken)).Result;

    private async Task<AnchorOutcome<T>> sendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var client = createClient(out var home);
            using var request = new HttpRequestMessage(method, new Uri(home, path));
            if (body is not null)
            {
                request.Content = JsonContent.Create(body, body.GetType());
            }

            using var response = await client.SendAsync(request, cancellationToken);
            var result = response.StatusCode switch
            {
                _ when response.IsSuccessStatusCode => AnchorResult.Ok,
                HttpStatusCode.NotFound => AnchorResult.NotFound,
                HttpStatusCode.BadRequest => AnchorResult.Invalid,
                HttpStatusCode.Conflict => AnchorResult.ReadOnly,
                _ => AnchorResult.Unavailable,
            };

            if (result != AnchorResult.Ok)
            {
                return new(result, Message: await problemDetailAsync(response, cancellationToken));
            }

            cache.Expire();
            notifier.NotifyChanged();
            var value = typeof(T) == typeof(object) || response.Content.Headers.ContentLength == 0
                ? null
                : await response.Content.ReadFromJsonAsync<T>(cancellationToken);
            return new(AnchorResult.Ok, value);
        }
        catch (Exception ex) when (isUnreachable(ex, cancellationToken))
        {
            logger.LogWarning(ex, "Anchor home didn't take {Method} {Path}", method, path);
            return new(AnchorResult.Unavailable, Message: "The anchor home couldn't be reached.");
        }
    }

    private HttpClient createClient(out Uri home)
    {
        home = AnchorSourceSettings.RemoteUrl(configuration)
            ?? throw new AnchorSourceUnavailableException("No anchor home URL is set (Anchors:RemoteUrl).");
        return httpClientFactory.CreateClient(HttpClientName);
    }

    private static bool isUnreachable(Exception ex, CancellationToken cancellationToken) =>
        ex is HttpRequestException or JsonException or NotSupportedException or AnchorSourceUnavailableException
        || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested);

    private static async Task<string?> problemDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return json.RootElement.TryGetProperty("detail", out var detail) ? detail.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string query(params (string Name, DateOnly? Value)[] parts)
    {
        var present = parts.Where(p => p.Value is not null).Select(p => $"{p.Name}={p.Value!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}").ToList();
        return present.Count == 0 ? string.Empty : "?" + string.Join('&', present);
    }
}
