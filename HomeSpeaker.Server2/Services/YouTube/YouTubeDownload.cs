namespace HomeSpeaker.Server2.Services.YouTube;

/// <summary>A request in the <see cref="YouTubeDownloadQueue"/> and how far it has got.</summary>
public sealed class YouTubeDownload
{
    public YouTubeDownload(YouTubeDownloadRequest request)
    {
        Request = request;
    }

    public YouTubeDownloadRequest Request { get; }

    public string VideoId => Request.VideoId;

    public string Title => Request.Title;

    public YouTubeDownloadStatus Status { get; internal set; } = YouTubeDownloadStatus.Queued;

    /// <summary>0..1 while downloading.</summary>
    public double Progress { get; internal set; }

    public string? Error { get; internal set; }

    public bool IsFinished => Status is not (YouTubeDownloadStatus.Queued or YouTubeDownloadStatus.Downloading);
}
