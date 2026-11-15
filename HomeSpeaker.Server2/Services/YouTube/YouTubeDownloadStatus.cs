namespace HomeSpeaker.Server2.Services.YouTube;

public enum YouTubeDownloadStatus
{
    Queued,
    Downloading,
    Done,
    AlreadyHad,
    Failed,
    Cancelled,
}
