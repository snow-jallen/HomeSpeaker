namespace HomeSpeaker.Server2.Models;

public class RadioStreamViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? FaviconFileName { get; set; }
    public int PlayCount { get; set; }
    public int DisplayOrder { get; set; }

    public string FaviconUrl => string.IsNullOrWhiteSpace(FaviconFileName)
        ? "/icon-192.png"  // Default fallback icon
        : $"/favicons/{FaviconFileName}";
}
