namespace HomeSpeaker.Server2.Services;

/// <summary>
/// Checks a station-art file name sent through the REST API, so callers can only point a stream
/// at an image that was already uploaded into the favicons folder, never at an arbitrary path.
/// </summary>
public static class FaviconFileNameValidator
{
    /// <summary>Returns an error message, or null when the name is acceptable (null or blank means "leave the art unchanged").</summary>
    public static string? Validate(string? fileName, string faviconsDirectory)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        if (fileName.Contains('/', StringComparison.Ordinal)
            || fileName.Contains('\\', StringComparison.Ordinal)
            || fileName.Contains("..", StringComparison.Ordinal)
            || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || Path.IsPathRooted(fileName)
            || !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
        {
            return "FaviconFileName must be a bare file name in the favicons folder.";
        }

        if (!File.Exists(Path.Combine(faviconsDirectory, fileName)))
        {
            return $"Favicon file '{fileName}' was not found. Upload it first with POST /api/streams/upload-image.";
        }

        return null;
    }

    /// <summary>Returns an error message, or null when the URL is blank or an absolute http(s) URL.</summary>
    public static string? ValidateDownloadUrl(string? faviconUrl)
    {
        if (string.IsNullOrWhiteSpace(faviconUrl))
        {
            return null;
        }

        if (!Uri.TryCreate(faviconUrl, UriKind.Absolute, out var uri)
            || (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)))
        {
            return "FaviconUrl must be an absolute http or https URL.";
        }

        return null;
    }
}
