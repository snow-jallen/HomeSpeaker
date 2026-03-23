using System.Text.Json;
using System.Text.RegularExpressions;
using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Services;

public class ImageSearchService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ImageSearchService> _logger;

    public ImageSearchService(HttpClient httpClient, ILogger<ImageSearchService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<ImageSearchResult>> SearchAsync(string query)
    {
        var ddgTask = SearchDuckDuckGoAsync(query);
        var wikiTask = SearchWikipediaAsync(query);
        await Task.WhenAll(ddgTask, wikiTask);

        var results = new List<ImageSearchResult>();
        results.AddRange(ddgTask.Result);
        results.AddRange(wikiTask.Result);
        return results.Take(20).ToList();
    }

    private async Task<List<ImageSearchResult>> SearchDuckDuckGoAsync(string query)
    {
        try
        {
            // Step 1: fetch vqd token from the DDG search page
            var response = await _httpClient.GetAsync(
                $"https://duckduckgo.com/?q={Uri.EscapeDataString(query)}&iax=images&ia=images");
            var html = await response.Content.ReadAsStringAsync();

            var vqdMatch = Regex.Match(html, @"vqd=['""]?([\w-]+)['""]?");
            if (!vqdMatch.Success)
            {
                _logger.LogWarning("Could not extract DDG vqd token for query: {Query}", query);
                return [];
            }
            var vqd = vqdMatch.Groups[1].Value;

            // Step 2: fetch image results with safe=strict
            var imgUrl = $"https://duckduckgo.com/i.js" +
                         $"?q={Uri.EscapeDataString(query)}" +
                         $"&vqd={Uri.EscapeDataString(vqd)}" +
                         $"&p=1&o=json&f=,,,&l=&s=safe";

            var imgResponse = await _httpClient.GetAsync(imgUrl);
            if (!imgResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("DDG image search returned {StatusCode}", imgResponse.StatusCode);
                return [];
            }

            var json = await imgResponse.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var results = new List<ImageSearchResult>();

            if (doc.RootElement.TryGetProperty("results", out var resultsEl))
            {
                foreach (var item in resultsEl.EnumerateArray().Take(12))
                {
                    var imageUrl = item.TryGetProperty("image", out var img) ? img.GetString() ?? "" : "";
                    var thumbUrl = item.TryGetProperty("thumbnail", out var thumb) ? thumb.GetString() ?? imageUrl : imageUrl;
                    var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                    if (!string.IsNullOrWhiteSpace(imageUrl))
                        results.Add(new ImageSearchResult(imageUrl, thumbUrl, "DuckDuckGo", title));
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DDG image search failed for query: {Query}", query);
            return [];
        }
    }

    private async Task<List<ImageSearchResult>> SearchWikipediaAsync(string query)
    {
        try
        {
            var url = "https://en.wikipedia.org/w/api.php" +
                      $"?action=query&generator=search&gsrsearch={Uri.EscapeDataString(query)}" +
                      "&prop=pageimages&piprop=thumbnail&pithumbsize=200&format=json&origin=*";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return [];

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var results = new List<ImageSearchResult>();

            if (doc.RootElement.TryGetProperty("query", out var queryEl) &&
                queryEl.TryGetProperty("pages", out var pages))
            {
                foreach (var page in pages.EnumerateObject().Take(8))
                {
                    var pageEl = page.Value;
                    var title = pageEl.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                    if (pageEl.TryGetProperty("thumbnail", out var thumb) &&
                        thumb.TryGetProperty("source", out var src))
                    {
                        var thumbUrl = src.GetString() ?? "";
                        if (!string.IsNullOrWhiteSpace(thumbUrl))
                            results.Add(new ImageSearchResult(thumbUrl, thumbUrl, "Wikipedia", title));
                    }
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Wikipedia image search failed for query: {Query}", query);
            return [];
        }
    }
}
