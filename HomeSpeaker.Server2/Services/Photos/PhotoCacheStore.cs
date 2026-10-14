using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace HomeSpeaker.Server2.Services.Photos;

/// <summary>
/// Owns the on-disk photo cache: preview JPEGs named {assetId}.jpg, manifest.json and hidden.json.
/// Every write goes to a .tmp file first and is then renamed, so readers never see a partial file.
/// </summary>
public sealed class PhotoCacheStore
{
    private const string ManifestFile = "manifest.json";
    private const string HiddenFile = "hidden.json";
    private const string VideosFolder = "videos";
    private const string TempSuffix = ".tmp";

    /// <summary>Containers a browser can play straight from a file, keyed by the upstream Content-Type. Anything else is not prefetched.</summary>
    private static readonly (string ContentType, string Extension)[] videoFormats =
    [
        ("video/mp4", ".mp4"),
        ("video/quicktime", ".mov"),
        ("video/webm", ".webm"),
    ];
    private static readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);
    private readonly Lock hiddenGate = new();
    private readonly ILogger<PhotoCacheStore> logger;

    public PhotoCacheStore(string rootPath, ILogger<PhotoCacheStore> logger)
    {
        RootPath = Path.GetFullPath(rootPath);
        this.logger = logger;
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public static bool IsValidAssetId(string assetId) => Guid.TryParse(assetId, out _);

    public IReadOnlyList<PhotoEntry> LoadManifest() => readJson<List<PhotoEntry>>(ManifestFile) ?? [];

    public void SaveManifest(IReadOnlyList<PhotoEntry> entries) => writeJsonAtomic(ManifestFile, entries);

    public bool HasImage(string assetId) => IsValidAssetId(assetId) && File.Exists(imagePath(assetId));

    public bool TryGetImagePath(string assetId, [NotNullWhen(true)] out string? path)
    {
        path = null;
        if (!HasImage(assetId))
        {
            return false;
        }

        path = imagePath(assetId);
        return true;
    }

    public async Task SaveImageAsync(string assetId, Func<Stream, Task> writeContent)
    {
        if (!IsValidAssetId(assetId))
        {
            throw new ArgumentException($"Not a valid asset id: {assetId}", nameof(assetId));
        }

        var finalPath = imagePath(assetId);
        var tempPath = finalPath + ".tmp";
        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await writeContent(stream);
            }

            File.Move(tempPath, finalPath, overwrite: true);
        }
        catch
        {
            tryDelete(tempPath);
            throw;
        }
    }

    public void DeleteImage(string assetId)
    {
        if (IsValidAssetId(assetId))
        {
            tryDelete(imagePath(assetId));
        }
    }

    /// <summary>Asset ids of every {assetId}.jpg on disk, whether or not the manifest knows about it. Ignores .tmp files.</summary>
    public IReadOnlyList<string> ListCachedImageIds() => Directory.EnumerateFiles(RootPath, "*.jpg")
        .Select(f => Path.GetFileNameWithoutExtension(f))
        .Where(IsValidAssetId)
        .ToList();

    /// <summary>Deletes leftover {assetId}.jpg.tmp files from interrupted downloads that are older than <paramref name="olderThan"/>.</summary>
    public void DeleteStaleTempImages(TimeSpan olderThan)
    {
        var cutoff = DateTimeOffset.UtcNow - olderThan;
        foreach (var file in Directory.EnumerateFiles(RootPath, "*.jpg.tmp"))
        {
            if (File.GetLastWriteTimeUtc(file) < cutoff)
            {
                tryDelete(file);
            }
        }
    }

    /// <summary>Folder holding prefetched slideshow videos, {assetId}.mp4 (or .mov/.webm, by upstream type). Created on first save.</summary>
    public string VideosPath => Path.Combine(RootPath, VideosFolder);

    /// <summary>The file extension to store a video of this Content-Type under, or null when it is not a format worth caching.</summary>
    public static string? VideoExtensionFor(string? contentType)
    {
        var mediaType = contentType?.Split(';')[0].Trim();
        foreach (var (type, extension) in videoFormats)
        {
            if (string.Equals(type, mediaType, StringComparison.OrdinalIgnoreCase))
            {
                return extension;
            }
        }

        return null;
    }

    public bool HasVideo(string assetId) => TryGetVideo(assetId, out _, out _);

    /// <summary>Finds a completed cached video. Partial downloads live under a .tmp name and are never returned.</summary>
    public bool TryGetVideo(string assetId, [NotNullWhen(true)] out string? path, [NotNullWhen(true)] out string? contentType)
    {
        path = null;
        contentType = null;
        if (!IsValidAssetId(assetId))
        {
            return false;
        }

        foreach (var (type, extension) in videoFormats)
        {
            var candidate = Path.Combine(VideosPath, assetId + extension);
            if (File.Exists(candidate))
            {
                path = candidate;
                contentType = type;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Saves a video via a .tmp file in the same folder and an atomic rename, so a partial file is never served. If
    /// <paramref name="writeContent"/> throws (including cancellation) the temp file is deleted.
    /// </summary>
    public async Task SaveVideoAsync(string assetId, string extension, Func<Stream, Task> writeContent)
    {
        if (!IsValidAssetId(assetId))
        {
            throw new ArgumentException($"Not a valid asset id: {assetId}", nameof(assetId));
        }

        Directory.CreateDirectory(VideosPath);
        var finalPath = Path.Combine(VideosPath, assetId + extension);
        var tempPath = finalPath + TempSuffix;
        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await writeContent(stream);
            }

            File.Move(tempPath, finalPath, overwrite: true);
        }
        catch
        {
            tryDelete(tempPath);
            throw;
        }
    }

    /// <summary>
    /// Opens a completed cached video for reading, sharing read and delete so an eviction never fails a stream that is under
    /// way. False when there is no such file, it was evicted a moment ago, or it cannot be opened; the caller then streams live.
    /// </summary>
    public bool TryOpenVideo(string assetId, [NotNullWhen(true)] out FileStream? stream, [NotNullWhen(true)] out string? contentType)
    {
        stream = null;
        contentType = null;
        if (!IsValidAssetId(assetId))
        {
            return false;
        }

        foreach (var (type, extension) in videoFormats)
        {
            try
            {
                stream = new FileStream(
                    Path.Combine(VideosPath, assetId + extension),
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read | FileShare.Delete,
                    bufferSize: 4096,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                contentType = type;
                return true;
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                // Not cached under this extension.
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogDebug(ex, "Could not open cached video {AssetId}; streaming it live instead", assetId);
                return false;
            }
        }

        return false;
    }

    /// <summary>Free bytes on the drive holding the cache, or null if it cannot be determined.</summary>
    public long? GetAvailableFreeSpace()
    {
        try
        {
            return new DriveInfo(RootPath).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Asset ids of every completed cached video.</summary>
    public IReadOnlyList<string> ListCachedVideoIds()
    {
        if (!Directory.Exists(VideosPath))
        {
            return [];
        }

        return Directory.EnumerateFiles(VideosPath)
            .Where(f => videoFormats.Any(v => f.EndsWith(v.Extension, StringComparison.Ordinal)))
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .Where(IsValidAssetId)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Deletes a cached video. Returns false when a file is still there afterwards, i.e. it is being served (Windows holds
    /// it open); the caller retries later.
    /// </summary>
    public bool TryDeleteVideo(string assetId)
    {
        if (!IsValidAssetId(assetId))
        {
            return true;
        }

        var deleted = true;
        foreach (var (_, extension) in videoFormats)
        {
            var path = Path.Combine(VideosPath, assetId + extension);
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogDebug(ex, "Could not delete video {File} yet; it is probably being served", path);
                deleted = false;
            }
        }

        return deleted;
    }

    /// <summary>Startup cleanup: removes temp files from interrupted downloads and anything else in the videos folder that is not a cached video.</summary>
    public void DeleteStrayVideoFiles()
    {
        if (!Directory.Exists(VideosPath))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(VideosPath))
        {
            var isCachedVideo = videoFormats.Any(v => file.EndsWith(v.Extension, StringComparison.Ordinal))
                && IsValidAssetId(Path.GetFileNameWithoutExtension(file));
            if (!isCachedVideo)
            {
                tryDelete(file);
            }
        }
    }

    public IReadOnlySet<string> LoadHidden()
    {
        lock (hiddenGate)
        {
            return readJson<HashSet<string>>(HiddenFile) ?? [];
        }
    }

    public void AddHidden(string assetId)
    {
        lock (hiddenGate)
        {
            var hidden = readJson<HashSet<string>>(HiddenFile) ?? [];
            if (hidden.Add(assetId))
            {
                writeJsonAtomic(HiddenFile, hidden);
            }
        }
    }

    private string imagePath(string assetId) => Path.Combine(RootPath, assetId + ".jpg");

    private T? readJson<T>(string fileName)
        where T : class
    {
        var path = Path.Combine(RootPath, fileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), json);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            logger.LogWarning(ex, "Could not read {File}; treating it as empty", path);
            return null;
        }
    }

    private void writeJsonAtomic<T>(string fileName, T value)
    {
        var path = Path.Combine(RootPath, fileName);
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(value, json));
        File.Move(tempPath, path, overwrite: true);
    }

    private void tryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not delete {File}", path);
        }
    }
}
