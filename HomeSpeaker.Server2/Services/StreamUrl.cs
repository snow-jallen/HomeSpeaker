namespace HomeSpeaker.Server2.Services;

/// <summary>
/// Validates a stream URL before it is saved or handed to VLC. Only absolute http/https URLs are
/// allowed, and the escaped form is returned so quotes or spaces cannot reach the command line.
/// </summary>
public static class StreamUrl
{
    /// <summary>Returns the escaped absolute URL, or null when it is not an http or https URL.</summary>
    public static string? Normalize(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)
            || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        return uri.AbsoluteUri;
    }
}
