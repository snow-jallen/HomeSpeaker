using HomeSpeaker.Server2.Data;
using Microsoft.EntityFrameworkCore;

namespace HomeSpeaker.Server2.Services;

public class RadioStreamService
{
    private readonly MusicContext _dbContext;
    private readonly ILogger<RadioStreamService> _logger;
    private readonly IWebHostEnvironment _environment;

    public RadioStreamService(
        MusicContext dbContext,
        ILogger<RadioStreamService> logger,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _logger = logger;
        _environment = environment;
    }

    public async Task<IEnumerable<RadioStream>> GetAllStreamsAsync()
    {
        return await _dbContext.RadioStreams
            .OrderByDescending(s => s.PlayCount)
            .ThenBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<RadioStream?> GetStreamByIdAsync(int id)
    {
        return await _dbContext.RadioStreams.FindAsync(id);
    }

    public async Task<RadioStream> CreateStreamAsync(string name, string url, string? faviconUrl = null)
    {
        var stream = new RadioStream
        {
            Name = name,
            Url = url,
            CreatedAt = DateTime.UtcNow,
            DisplayOrder = await GetNextDisplayOrderAsync()
        };

        // Try to download favicon if provided
        if (!string.IsNullOrWhiteSpace(faviconUrl))
        {
            stream.FaviconFileName = await DownloadFaviconAsync(name, faviconUrl);
        }

        await _dbContext.RadioStreams.AddAsync(stream);
        await _dbContext.SaveChangesAsync();

        return stream;
    }

    public async Task UpdateStreamAsync(int id, string name, string url, string? faviconUrl = null)
    {
        var stream = await _dbContext.RadioStreams.FindAsync(id);
        if (stream == null) return;

        stream.Name = name;
        stream.Url = url;

        // Update favicon if new URL provided
        if (!string.IsNullOrWhiteSpace(faviconUrl))
        {
            // Download new favicon first
            var newFileName = await DownloadFaviconAsync(name, faviconUrl);

            // Delete old favicon if new one was successfully downloaded
            if (newFileName != null && !string.IsNullOrWhiteSpace(stream.FaviconFileName))
            {
                DeleteFavicon(stream.FaviconFileName);
            }

            stream.FaviconFileName = newFileName;
        }

        await _dbContext.SaveChangesAsync();
    }

    public async Task DeleteStreamAsync(int id)
    {
        var stream = await _dbContext.RadioStreams.FindAsync(id);
        if (stream == null) return;

        // Delete favicon file if exists
        if (!string.IsNullOrWhiteSpace(stream.FaviconFileName))
        {
            DeleteFavicon(stream.FaviconFileName);
        }

        _dbContext.RadioStreams.Remove(stream);
        await _dbContext.SaveChangesAsync();
    }

    public async Task IncrementPlayCountAsync(int streamId)
    {
        var stream = await _dbContext.RadioStreams.FindAsync(streamId);
        if (stream == null) return;

        stream.PlayCount++;
        stream.LastPlayedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
    }

    private async Task<string?> DownloadFaviconAsync(string streamName, string faviconUrl)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            var response = await httpClient.GetAsync(faviconUrl);
            if (!response.IsSuccessStatusCode) return null;

            var contentType = response.Content.Headers.ContentType?.MediaType;
            var extension = GetExtensionFromContentType(contentType) ?? ".png";

            // Generate safe filename from stream name
            var safeFileName = GetSafeFileName(streamName) + extension;
            var faviconDir = Path.Combine(_environment.WebRootPath, "favicons");
            var faviconPath = Path.Combine(faviconDir, safeFileName);

            // Ensure favicons directory exists
            Directory.CreateDirectory(faviconDir);

            var bytes = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(faviconPath, bytes);

            _logger.LogInformation("Downloaded favicon for {StreamName} to {Path}", streamName, safeFileName);
            return safeFileName;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download favicon from {Url} for {StreamName}", faviconUrl, streamName);
            return null;
        }
    }

    private void DeleteFavicon(string fileName)
    {
        try
        {
            var path = Path.Combine(_environment.WebRootPath, "favicons", fileName);
            if (File.Exists(path))
            {
                File.Delete(path);
                _logger.LogInformation("Deleted favicon file {FileName}", fileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete favicon {FileName}", fileName);
        }
    }

    private string GetSafeFileName(string name)
    {
        // Remove invalid filename characters
        var invalid = Path.GetInvalidFileNameChars();
        var safeName = string.Join("_", name.Split(invalid));
        return safeName.ToLowerInvariant().Replace(" ", "_");
    }

    private string? GetExtensionFromContentType(string? contentType)
    {
        return contentType switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/gif" => ".gif",
            "image/x-icon" => ".ico",
            "image/vnd.microsoft.icon" => ".ico",
            _ => ".png"
        };
    }

    private async Task<int> GetNextDisplayOrderAsync()
    {
        var maxOrder = await _dbContext.RadioStreams.MaxAsync(s => (int?)s.DisplayOrder);
        return (maxOrder ?? 0) + 1;
    }
}
