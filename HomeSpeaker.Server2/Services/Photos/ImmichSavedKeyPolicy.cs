namespace HomeSpeaker.Server2.Services.Photos;

/// <summary>
/// Decides whether "Load albums" may send the saved Immich key: only to the saved Immich address, so a typed URL
/// can never be used to collect the key. Any other address needs the key typed in.
/// </summary>
public static class ImmichSavedKeyPolicy
{
    public static bool Allows(string? typedUrl, string? savedUrl) =>
        normalize(typedUrl) is { } typed && normalize(savedUrl) is { } saved && string.Equals(typed, saved, StringComparison.Ordinal);

    // Scheme and host are case-insensitive (Uri lowercases them); the path keeps its case, minus a trailing slash.
    private static string? normalize(string? url) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? $"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath.TrimEnd('/')}"
            : null;
}
