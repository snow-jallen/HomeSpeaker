namespace HomeSpeaker.Server2.Services.YouTube;

public static class YouTubeDownloads
{
    /// <summary>
    /// One request per video. A playlist's videos share its title as their folder and album and are
    /// numbered in playlist order; a single video goes straight into "YouTube Cache" as before.
    /// </summary>
    public static IEnumerable<YouTubeDownloadRequest> RequestsFor(YouTubeLinkContents contents) =>
        contents.Videos.Select((v, i) => contents.PlaylistTitle is null
            ? new YouTubeDownloadRequest(v.Id, v.Title, v.Author)
            : new YouTubeDownloadRequest(v.Id, v.Title, v.Author, contents.PlaylistTitle, i + 1));
}
