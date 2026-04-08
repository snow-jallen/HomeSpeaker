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

    private const string CACHE_KEY = "radio_streams_all";
    private static readonly TimeSpan cacheDuration = TimeSpan.FromMinutes(5);

    public RadioStreamService(
        MusicContext dbContext,
        ILogger<RadioStreamService> logger,
        IConfiguration configuration,
        IMemoryCache cache,
        HttpClient httpClient)
    {
        this.dbContext = dbContext;
        this.logger = logger;
        this.cache = cache;
        this.httpClient = httpClient;
        this.httpClient.Timeout = TimeSpan.FromSeconds(10);

        // Store favicons in the media folder (volume-mounted, writable) rather than wwwroot (read-only in container)
        var mediaFolder = configuration[ConfigKeys.MediaFolder] ?? "/music";
        faviconsDirectory = Path.Combine(mediaFolder, "favicons");
    }

    public async Task<IEnumerable<RadioStream>> GetAllStreamsAsync()
    {
        return await cache.GetOrCreateAsync(CACHE_KEY, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = cacheDuration;
            logger.LogDebug("Cache miss - loading radio streams from database");

            return await dbContext.RadioStreams
                .OrderByDescending(s => s.PlayCount)
                .ThenBy(s => s.Name)
                .AsNoTracking()  // Performance: Read-only query
                .ToListAsync();
        }) ?? Enumerable.Empty<RadioStream>();
    }

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
            CreatedAt = DateTime.UtcNow,
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

        if (!string.IsNullOrWhiteSpace(faviconFileName))
        {
            // Pre-uploaded file — delete old favicon and store new filename directly
            if (!string.IsNullOrWhiteSpace(stream.FaviconFileName))
            {
                deleteFavicon(stream.FaviconFileName);
            }

            stream.FaviconFileName = faviconFileName;
        }
        else if (!string.IsNullOrWhiteSpace(faviconUrl))
        {
            // Download new favicon first
            var newFileName = await downloadFaviconAsync(name, faviconUrl);

            // Delete old favicon if new one was successfully downloaded (and it's a different file)
            if (newFileName != null && !string.IsNullOrWhiteSpace(stream.FaviconFileName) && stream.FaviconFileName != newFileName)
            {
                deleteFavicon(stream.FaviconFileName);
            }

            stream.FaviconFileName = newFileName;
        }
        // else: both empty — no change to favicon

        await dbContext.SaveChangesAsync();

        // Invalidate cache
        cache.Remove(CACHE_KEY);
    }

    public async Task<string?> UploadFaviconAsync(IFormFile file)
    {
        var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/png", "image/jpeg", "image/gif",
            "image/x-icon", "image/vnd.microsoft.icon", "image/webp"
        };

        if (!allowedTypes.Contains(file.ContentType))
        {
            return null;
        }

        if (file.Length > 2 * 1024 * 1024)
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

        // Delete favicon file if exists
        if (!string.IsNullOrWhiteSpace(stream.FaviconFileName))
        {
            deleteFavicon(stream.FaviconFileName);
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
        stream.LastPlayedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        // Invalidate cache
        cache.Remove(CACHE_KEY);
    }

    private async Task<string?> downloadFaviconAsync(string streamName, string faviconUrl)
    {
        try
        {
            var response = await httpClient.GetAsync(faviconUrl);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            var extension = getExtensionFromContentType(contentType) ?? ".png";

            // Generate safe filename from stream name
            var safeFileName = getSafeFileName(streamName) + extension;
            var faviconPath = Path.Combine(faviconsDirectory, safeFileName);

            // Ensure favicons directory exists
            Directory.CreateDirectory(faviconsDirectory);

            var bytes = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(faviconPath, bytes);

            logger.LogInformation("Downloaded favicon for {StreamName} to {Path}", streamName, safeFileName);
            return safeFileName;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to download favicon from {Url} for {StreamName}", faviconUrl, streamName);
            return null;
        }
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