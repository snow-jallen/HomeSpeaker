namespace HomeSpeaker.Server2.Services.YouTube;

public interface IYouTubeDownloader
{
    /// <summary>Saves the video's audio into the library. Returns false when the file was already there.</summary>
    Task<bool> DownloadAsync(YouTubeDownloadRequest request, IProgress<double> progress, CancellationToken cancellationToken);
}
