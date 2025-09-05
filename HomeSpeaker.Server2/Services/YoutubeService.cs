using HomeSpeaker.Server2;
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
        _config = config;
        _logger = logger;
        _library = library;
    }

    private readonly YoutubeClient _client = new();
    private readonly IConfiguration _config;
    private readonly ILogger<YoutubeService> _logger;
    private readonly Mp3Library _library;
    private bool _disposed;

    public async Task<IEnumerable<VideoDto>> SearchAsync(string searchTerm, int maxItems = 50)
    {
        List<VideoDto> results = new();
        await foreach (var batch in _client.Search.GetResultBatchesAsync(searchTerm))
        {
            foreach (var result in batch.Items)
            {
                switch (result)
                {
                    case VideoSearchResult v:
                        results.Add(new VideoDto(v.Title, v.Id, v.Url, v.Thumbnails.FirstOrDefault()?.Url, v.Author?.ChannelTitle, v.Duration));
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
    public async Task CacheVideoAsync(string id, string title, IProgress<double> progress)
    {
        var fileName = string.Join("_", $"{title}.mp3".Split(Path.GetInvalidFileNameChars()));
        var destinationPath = Path.Combine(_config[ConfigKeys.MediaFolder]!, "YouTube Cache");
        if (!Directory.Exists(destinationPath))
            Directory.CreateDirectory(destinationPath);
        destinationPath = Path.Combine(destinationPath, fileName);
        var ffmpegLocation = _config[ConfigKeys.FFMpegLocation] ?? throw new Exception("Missing ffmeg path in config: " + ConfigKeys.FFMpegLocation);

        _logger.LogInformation("Beginning to cache {title}", title);

        await _client.Videos.DownloadAsync(VideoId.Parse(id), new ConversionRequest(ffmpegLocation, destinationPath, Container.Mp3, ConversionPreset.Medium), progress);

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

        _logger.LogInformation("Finished caching {title}.  Saved to {destination}", title, destinationPath);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // YoutubeClient doesn't implement IDisposable, so no cleanup needed
                // Note: _client is readonly and cannot be reassigned
            }
            _disposed = true;
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

internal partial class MediaFile : IDisposable
{
    private readonly TagFile _file;

    public MediaFile(TagFile file) => _file = file;

    public void SetThumbnail(byte[] thumbnailData) =>
        _file.Tag.Pictures = new IPicture[] { new Picture(thumbnailData) };

    public void SetArtist(string artist) =>
        _file.Tag.Performers = new[] { artist };

    public void SetArtistSort(string artistSort) =>
        _file.Tag.PerformersSort = new[] { artistSort };

    public void SetTitle(string title) =>
        _file.Tag.Title = title;

    public void SetAlbum(string album) =>
        _file.Tag.Album = album;

    public void SetDescription(string description) =>
        _file.Tag.Description = description;

    public void SetComment(string comment) =>
        _file.Tag.Comment = comment;

    public void Dispose()
    {
        _file.Tag.DateTagged = DateTime.Now;
        _file.Save();
        _file.Dispose();
    }

    public static MediaFile Create(string filePath) => new(TagFile.Create(filePath));
}
