using Microsoft.AspNetCore.WebUtilities;

namespace HomeSpeaker.Server2.Services.YouTube;

/// <summary>
/// A pasted YouTube address: one video, or a playlist (every video on it). Only real YouTube
/// URLs count, so a search term that happens to look like a video id is still a search.
/// </summary>
public sealed record YouTubeLink(string? VideoId, string? PlaylistId)
{
    private static readonly string[] youTubeHosts = ["youtube.com", "youtu.be", "youtube-nocookie.com"];

    public bool IsPlaylist => PlaylistId is not null;

    /// <summary>
    /// A watch URL that also names a playlist (<c>watch?v=X&amp;list=Y</c>) is treated as the
    /// playlist, except for YouTube's generated mixes (<c>RD…</c>), which are endless radio, not a list
    /// someone made; those mean the one video.
    /// </summary>
    public static YouTubeLink? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        var withScheme = trimmed.Contains("://", StringComparison.Ordinal) ? trimmed : "https://" + trimmed;
        if (!Uri.TryCreate(withScheme, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || !isYouTubeHost(uri.Host))
        {
            return null;
        }

        var video = YoutubeExplode.Videos.VideoId.TryParse(uri.AbsoluteUri)?.Value;
        var list = QueryHelpers.ParseQuery(uri.Query).TryGetValue("list", out var values)
            ? YoutubeExplode.Playlists.PlaylistId.TryParse(values.ToString())?.Value
            : null;

        if (list is not null && video is not null && list.StartsWith("RD", StringComparison.Ordinal))
        {
            list = null;
        }

        return video is null && list is null ? null : new YouTubeLink(list is null ? video : null, list);
    }

    private static bool isYouTubeHost(string host) =>
        youTubeHosts.Any(h => host.Equals(h, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + h, StringComparison.OrdinalIgnoreCase));
}
