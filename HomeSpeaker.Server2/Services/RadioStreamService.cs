using HomeSpeaker.Server2.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace HomeSpeaker.Server2.Services;

public class RadioStreamService
{
    private readonly MusicContext dbContext;
    private readonly ILogger<RadioStreamService> logger;
    private readonly IMemoryCache cache;
    private readonly HttpClient httpClient;
    private readonly string faviconsDirectory;
    private readonly TimeProvider timeProvider;

    private const string CACHE_KEY = "radio_streams_all";
    private static readonly TimeSpan cacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan downloadTimeout = TimeSpan.FromSeconds(15);

    /// <summary>The largest station image accepted, uploaded or downloaded.</summary>
    public const long MaxFaviconBytes = 2 * 1024 * 1024;

    /// <summary>The image content types accepted for station art, uploaded or downloaded.</summary>
    public static readonly IReadOnlySet<string> AllowedImageTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/gif",
        "image/x-icon", "image/vnd.microsoft.icon", "image/webp"
    };

    public RadioStreamService(
        MusicContext dbContext,
        ILogger<RadioStreamService> logger,
        IConfiguration configuration,
        IMemoryCache cache,
        HttpClient httpClient,
        TimeProvider timeProvider)
    {
        this.dbContext = dbContext;
        this.logger = logger;
        this.cache = cache;
        this.httpClient = httpClient;
        this.httpClient.Timeout = downloadTimeout;
        this.timeProvider = timeProvider;

        // Store favicons in the media folder (volume-mounted, writable) rather than wwwroot (read-only in container)
        var mediaFolder = configuration[ConfigKeys.MediaFolder] ?? "/music";
        faviconsDirectory = Path.Combine(mediaFolder, "favicons");
    }

    /// <summary>The folder uploaded and downloaded station art lives in (served at /favicons).</summary>
    public string FaviconsDirectory => faviconsDirectory;

    public async Task<IEnumerable<RadioStream>> GetAllStreamsAsync()
    {
        return await cache.GetOrCreateAsync(CACHE_KEY, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = cacheDuration;
            logger.LogDebug("Cache miss - loading radio streams from database");

            var streams = await dbContext.RadioStreams
                .AsNoTracking()  // Performance: Read-only query
                .ToListAsync();

            return RadioStreamOrdering.ByPopularity(
                streams,
                s => s.PlayCount,
                s => s.Name
            ).ToList();
        }) ?? Enumerable.Empty<RadioStream>();
    }

    /// <summary>
    /// Finds a saved stream by its display name (ordinal, ignoring case) from the cached list,
    /// so now-playing views that only know the stream name can show its art cheaply.
    /// </summary>
    public async Task<RadioStream?> FindStreamByNameAsync(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var streams = await GetAllStreamsAsync();
        return streams.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Drops the cached stream list; call after changing streams outside this service.</summary>
    public void InvalidateCache() => cache.Remove(CACHE_KEY);

    public async Task<RadioStream?> GetStreamByIdAsync(int id)
    {
        return await dbContext.RadioStreams.FindAsync(id);
    }

    public async Task<RadioStream> CreateStreamAsync(string name, string url, string? faviconUrl = null, string? faviconFileName = null)
    {
        var stream = new RadioStream
        {
            Name = name,
            Url = url,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
            DisplayOrder = await getNextDisplayOrderAsync()
        };

        if (!string.IsNullOrWhiteSpace(faviconFileName))
        {
            // Pre-uploaded file — store filename directly
            stream.FaviconFileName = faviconFileName;
        }
        else if (!string.IsNullOrWhiteSpace(faviconUrl))
        {
            stream.FaviconFileName = await downloadFaviconAsync(name, faviconUrl);
        }

        await dbContext.RadioStreams.AddAsync(stream);
        await dbContext.SaveChangesAsync();

        // Invalidate cache
        cache.Remove(CACHE_KEY);

        return stream;
    }

    public async Task UpdateStreamAsync(int id, string name, string url, string? faviconUrl = null, string? faviconFileName = null)
    {
        var stream = await dbContext.RadioStreams.FindAsync(id);
        if (stream == null)
        {
            return;
        }

        stream.Name = name;
        stream.Url = url;

        // The old art is deleted only once the save succeeds, so a failed save never leaves the row pointing at a deleted file
        var oldFileName = stream.FaviconFileName;
        string? newFileName = null;
        string? downloadedFileName = null;

        if (!string.IsNullOrWhiteSpace(faviconFileName))
        {
            // Pre-uploaded file — store filename directly
            newFileName = faviconFileName;
        }
        else if (!string.IsNullOrWhiteSpace(faviconUrl))
        {
            // A failed download keeps the current favicon rather than clearing it
            downloadedFileName = await downloadFaviconAsync(name, faviconUrl);
            newFileName = downloadedFileName;
        }
        // else: both empty — no change to favicon

        if (newFileName != null)
        {
            stream.FaviconFileName = newFileName;
        }

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch
        {
            // Nothing references the freshly downloaded file, so do not leave it behind
            if (downloadedFileName != null)
            {
                deleteFavicon(downloadedFileName);
            }

            throw;
        }

        // Delete the replaced file (unless it is the same file or another stream still uses it)
        if (newFileName != null && !string.IsNullOrWhiteSpace(oldFileName) && !string.Equals(oldFileName, newFileName, StringComparison.Ordinal))
        {
            await deleteFaviconIfUnusedAsync(oldFileName, stream.Id);
        }

        // Invalidate cache
        cache.Remove(CACHE_KEY);
    }

    public async Task<string?> UploadFaviconAsync(IFormFile file)
    {
        if (!AllowedImageTypes.Contains(file.ContentType))
        {
            return null;
        }

        if (file.Length > MaxFaviconBytes)
        {
            return null;
        }

        var extension = getExtensionFromContentType(file.ContentType) ?? ".png";
        var baseName = getSafeFileName(Path.GetFileNameWithoutExtension(file.FileName));
        var uniqueName = baseName + Guid.NewGuid().ToString("N")[..8] + extension;

        Directory.CreateDirectory(faviconsDirectory);

        var filePath = Path.Combine(faviconsDirectory, uniqueName);
        using var stream = File.Create(filePath);
        await file.CopyToAsync(stream);

        logger.LogInformation("Uploaded favicon saved as {FileName}", uniqueName);
        return uniqueName;
    }

    public async Task DeleteStreamAsync(int id)
    {
        var stream = await dbContext.RadioStreams.FindAsync(id);
        if (stream == null)
        {
            return;
        }

        // Delete the favicon file unless another stream still uses it
        if (!string.IsNullOrWhiteSpace(stream.FaviconFileName))
        {
            await deleteFaviconIfUnusedAsync(stream.FaviconFileName, stream.Id);
        }

        dbContext.RadioStreams.Remove(stream);
        await dbContext.SaveChangesAsync();

        // Invalidate cache
        cache.Remove(CACHE_KEY);
    }

    public async Task IncrementPlayCountAsync(int streamId)
    {
        var stream = await dbContext.RadioStreams.FindAsync(streamId);
        if (stream == null)
        {
            return;
        }

        stream.PlayCount++;
        stream.LastPlayedAt = timeProvider.GetUtcNow().UtcDateTime;
        await dbContext.SaveChangesAsync();

        // Invalidate cache
        cache.Remove(CACHE_KEY);
    }

    // FaviconUrl reaches here from the unauthenticated REST API, so only a small image is saved:
    // an allowed image content type, at most MaxFaviconBytes (enforced while streaming, not just from
    // Content-Length), within downloadTimeout. Anything else returns null and the caller keeps the current art.
    private async Task<string?> downloadFaviconAsync(string streamName, string faviconUrl)
    {
        try
        {
            using var timeout = new CancellationTokenSource(downloadTimeout, timeProvider);
            using var response = await httpClient.GetAsync(faviconUrl, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType is null || !AllowedImageTypes.Contains(contentType))
            {
                logger.LogWarning("Not saving favicon from {Url} for {StreamName}: content type {ContentType} is not an allowed image type", faviconUrl, streamName, contentType);
                return null;
            }

            if (response.Content.Headers.ContentLength > MaxFaviconBytes)
            {
                logger.LogWarning("Not saving favicon from {Url} for {StreamName}: {Length} bytes is over the limit", faviconUrl, streamName, response.Content.Headers.ContentLength);
                return null;
            }

            var bytes = await readAtMostAsync(response.Content, MaxFaviconBytes, timeout.Token);
            if (bytes is null)
            {
                logger.LogWarning("Not saving favicon from {Url} for {StreamName}: the body is over {Limit} bytes", faviconUrl, streamName, MaxFaviconBytes);
                return null;
            }

            // A unique name, like uploads, so two streams whose names sanitize alike never overwrite each other's art
            var extension = getExtensionFromContentType(contentType.ToLowerInvariant()) ?? ".png";
            var safeFileName = getSafeFileName(streamName) + Guid.NewGuid().ToString("N")[..8] + extension;
            var faviconPath = Path.Combine(faviconsDirectory, safeFileName);

            // Ensure favicons directory exists
            Directory.CreateDirectory(faviconsDirectory);
            await File.WriteAllBytesAsync(faviconPath, bytes, timeout.Token);

            logger.LogInformation("Downloaded favicon for {StreamName} to {Path}", streamName, safeFileName);
            return safeFileName;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to download favicon from {Url} for {StreamName}", faviconUrl, streamName);
            return null;
        }
    }

    // Reads the body into memory, giving up (null) as soon as it passes maxBytes.
    private static async Task<byte[]?> readAtMostAsync(HttpContent content, long maxBytes, CancellationToken cancellationToken)
    {
        await using var source = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        return buffer.ToArray();
    }

    // The REST API can point several streams at one file, so a stream only owns its file once no other stream uses it.
    private async Task deleteFaviconIfUnusedAsync(string fileName, int streamId)
    {
        var sharedWithAnotherStream = await dbContext.RadioStreams
            .AnyAsync(s => s.Id != streamId && s.FaviconFileName == fileName);
        if (sharedWithAnotherStream)
        {
            logger.LogInformation("Keeping favicon {FileName}: another stream still uses it", fileName);
            return;
        }

        deleteFavicon(fileName);
    }

    private void deleteFavicon(string fileName)
    {
        try
        {
            var path = Path.Combine(faviconsDirectory, fileName);
            if (File.Exists(path))
            {
                File.Delete(path);
                logger.LogInformation("Deleted favicon file {FileName}", fileName);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete favicon {FileName}", fileName);
        }
    }

    private string getSafeFileName(string name)
    {
        // Remove invalid filename characters
        var invalid = Path.GetInvalidFileNameChars();
        var safeName = string.Join("_", name.Split(invalid));
        return safeName.ToLowerInvariant().Replace(" ", "_");
    }

    private string? getExtensionFromContentType(string? contentType)
    {
        return contentType switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/gif" => ".gif",
            "image/x-icon" => ".ico",
            "image/vnd.microsoft.icon" => ".ico",
            "image/webp" => ".webp",
            _ => ".png"
        };
    }

    private async Task<int> getNextDisplayOrderAsync()
    {
        var maxOrder = await dbContext.RadioStreams.MaxAsync(s => (int?)s.DisplayOrder);
        return (maxOrder ?? 0) + 1;
    }
}
