namespace HomeSpeaker.Server2.Services;

/// <summary>
/// Serves song files to mobile clients for offline caching. Which songs a
/// device keeps offline is entirely the device's business — the server only
/// hands out media.
/// </summary>
public sealed class OfflineDownloadService
{
    private readonly Mp3Library library;

    public OfflineDownloadService(Mp3Library library)
    {
        this.library = library ?? throw new ArgumentNullException(nameof(library));
    }

    public OfflineDownloadMediaResult? GetMedia(string songPath)
    {
        if (string.IsNullOrWhiteSpace(songPath))
        {
            return null;
        }

        var song = library.Songs.FirstOrDefault(candidate =>
            !string.IsNullOrWhiteSpace(candidate.Path)
            && string.Equals(candidate.Path, songPath, StringComparison.OrdinalIgnoreCase));

        if (song?.Path is null)
        {
            return null;
        }

        var resolvedPath = Path.GetFullPath(song.Path);
        var fileInfo = new FileInfo(resolvedPath);
        if (!fileInfo.Exists)
        {
            return null;
        }

        return new OfflineDownloadMediaResult
        {
            FilePath = resolvedPath,
            ContentType = "audio/mpeg",
            DownloadFileName = fileInfo.Name,
            ETag = createEtag(fileInfo),
            LastModifiedUtc = fileInfo.LastWriteTimeUtc
        };
    }

    private static string createEtag(FileInfo fileInfo) =>
        $"\"{fileInfo.Length:x}-{fileInfo.LastWriteTimeUtc.Ticks:x}\"";
}

public sealed record OfflineDownloadMediaResult
{
    public string FilePath { get; init; } = string.Empty;
    public string ContentType { get; init; } = "audio/mpeg";
    public string DownloadFileName { get; init; } = string.Empty;
    public string ETag { get; init; } = string.Empty;
    public DateTime LastModifiedUtc { get; init; }
}
