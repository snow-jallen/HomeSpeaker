namespace HomeSpeaker.Server2.Models;

public class RadioStreamViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? FaviconFileName { get; set; }
    public int PlayCount { get; set; }
    public int DisplayOrder { get; set; }

    /// <summary>The uploaded station image, or null so StationArt shows a generated tile.</summary>
    public string? FaviconUrl => FaviconPath(FaviconFileName);

    /// <summary>The /favicons URL for a stored station-art file name, or null when there is none.</summary>
    public static string? FaviconPath(string? faviconFileName) => string.IsNullOrWhiteSpace(faviconFileName)
        ? null
        : "/favicons/" + Uri.EscapeDataString(faviconFileName);
}
