using System.Diagnostics.CodeAnalysis;
using TagLib;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Converter;
using YoutubeExplode.Search;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;
using TagFile = TagLib.File;

namespace HomeSpeaker.Server2.Services;

public class YoutubeService : IDisposable
{
    public YoutubeService(IConfiguration config, ILogger<YoutubeService> logger, Mp3Library library)
    {
        this.config = config;
        this.logger = logger;
        this.library = library;
    }

#pragma warning disable CA2213 // YoutubeClient does not implement IDisposable — false positive
    private readonly YoutubeClient client = new();
#pragma warning restore CA2213
    private readonly IConfiguration config;
    private readonly ILogger<YoutubeService> logger;
    private readonly Mp3Library library;
    private bool disposed;

    public async Task<IEnumerable<VideoDto>> SearchAsync(string searchTerm, int maxItems = 50)
    {
        List<VideoDto> results = new();
        await foreach (var batch in client.Search.GetResultBatchesAsync(searchTerm))
        {
            foreach (var result in batch.Items)
            {
                switch (result)
                {
                    case VideoSearchResult v:
                        results.Add(new VideoDto(v.Title, v.Id, v.Url, v.Thumbnails.Count > 0 ? v.Thumbnails[0]?.Url : null, v.Author?.ChannelTitle, v.Duration));
                        break;
                        //case PlaylistSearchResult p:
                        //    results.Add(new Video(p.Title, p.Url, p.Thumbnails.FirstOrDefault()?.Url, p.Author?.ChannelTitle, TimeSpan.Zero));
                        //    break;
                        //case ChannelSearchResult c:
                        //    results.Add(new Video(c.Title, c.Url, c.Thumbnails.FirstOrDefault()?.Url, "Author Unlisted", TimeSpan.Zero));
                        //    break;
                }

                if (results.Count > maxItems)
                {
                    return results;
                }
            }
        }

        return results;
    }

    public bool IsFfmpegAvailable()
    {
        var ffmpegLocation = config[ConfigKeys.FFMpegLocation];
        if (string.IsNullOrWhiteSpace(ffmpegLocation))
        {
            return false;
        }

        // Check absolute path first, then fall back to PATH resolution
        if (Path.IsPathRooted(ffmpegLocation))
        {
            return System.IO.File.Exists(ffmpegLocation);
        }

        // For relative/name-only values like "ffmpeg.exe", check via PATH
        try
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffmpegLocation,
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            process.Start();
            process.WaitForExit(2000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string?> GetBestAudioStreamUrlAsync(string videoId)
    {
        var manifest = await client.Videos.Streams.GetManifestAsync(VideoId.Parse(videoId));
        var audioStream = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();
        return audioStream?.Url;
    }

    public async Task CacheVideoAsync(string id, string title, IProgress<double> progress)
    {
        var fileName = string.Join("_", $"{title}.mp3".Split(Path.GetInvalidFileNameChars()));
        var destinationPath = Path.Combine(config[ConfigKeys.MediaFolder]!, "YouTube Cache");
        if (!Directory.Exists(destinationPath))
        {
            Directory.CreateDirectory(destinationPath);
        }

        destinationPath = Path.Combine(destinationPath, fileName);
        var ffmpegLocation = config[ConfigKeys.FFMpegLocation] ?? throw new Exception("Missing ffmeg path in config: " + ConfigKeys.FFMpegLocation);

        logger.LogInformation("Beginning to cache {Title}", title);

        await client.Videos.DownloadAsync(VideoId.Parse(id), destinationPath, o => o
            .SetFFmpegPath(ffmpegLocation)
            .SetContainer(Container.Mp3)
            .SetPreset(ConversionPreset.Medium), progress);

        try
        {
            using var mediaFile = MediaFile.Create(destinationPath);
            mediaFile.SetArtist("Youtube Cache");
            mediaFile.SetAlbum("Youtube Cache");
            mediaFile.SetTitle(title);
        }
        catch
        {
            // Media tagging is not critical
        }

        logger.LogInformation("Finished caching {Title}.  Saved to {Destination}", title, destinationPath);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                // YoutubeClient does not implement IDisposable — nothing to dispose
            }

            disposed = true;
        }
    }
}

public record VideoDto(string Title, string Id, string Url, string? Thumbnail, string? Author, TimeSpan? Duration);

/// <summary>
/// Metadata associated with a YouTube video.
/// </summary>
public class Video : IVideo
{
    /// <inheritdoc />
    public VideoId Id { get; }

    /// <inheritdoc />
    public string Url => $"https://www.youtube.com/watch?v={Id}";

    /// <inheritdoc />
    public string Title { get; }

    /// <inheritdoc />
    public Author Author { get; }

    /// <summary>
    /// Video upload date.
    /// </summary>
    public DateTimeOffset UploadDate { get; }

    /// <summary>
    /// Video description.
    /// </summary>
    public string Description { get; }

    /// <inheritdoc />
    public TimeSpan? Duration { get; }

    /// <inheritdoc />
    public IReadOnlyList<Thumbnail> Thumbnails { get; }

    /// <summary>
    /// Available search keywords for the video.
    /// </summary>
    public IReadOnlyList<string> Keywords { get; }

    /// <summary>
    /// Engagement statistics for the video.
    /// </summary>
    public Engagement Engagement { get; }

    /// <summary>
    /// Initializes an instance of <see cref="Video" />.
    /// </summary>
    public Video(
        VideoId id,
        string title,
        Author author,
        DateTimeOffset uploadDate,
        string description,
        TimeSpan? duration,
        IReadOnlyList<Thumbnail> thumbnails,
        IReadOnlyList<string> keywords,
        Engagement engagement)
    {
        Id = id;
        Title = title;
        Author = author;
        UploadDate = uploadDate;
        Description = description;
        Duration = duration;
        Thumbnails = thumbnails;
        Keywords = keywords;
        Engagement = engagement;
    }

    /// <inheritdoc />
    [ExcludeFromCodeCoverage]
    public override string ToString() => $"Video ({Title})";
}

internal sealed partial class MediaFile : IDisposable
{
    private readonly TagFile file;

    public MediaFile(TagFile file) => this.file = file;

    public void SetThumbnail(byte[] thumbnailData) =>
        file.Tag.Pictures = new IPicture[] { new Picture(thumbnailData) };

    public void SetArtist(string artist) =>
        file.Tag.Performers = new[] { artist };

    public void SetArtistSort(string artistSort) =>
        file.Tag.PerformersSort = new[] { artistSort };

    public void SetTitle(string title) =>
        file.Tag.Title = title;

    public void SetAlbum(string album) =>
        file.Tag.Album = album;

    public void SetDescription(string description) =>
        file.Tag.Description = description;

    public void SetComment(string comment) =>
        file.Tag.Comment = comment;

    public void Dispose()
    {
        file.Tag.DateTagged = DateTime.UtcNow.ToLocalTime();
        file.Save();
        file.Dispose();
    }

    public static MediaFile Create(string filePath) => new(TagFile.Create(filePath));
}