using System.Text.RegularExpressions;

namespace HomeSpeaker.Server2.Services;

/// <summary>
/// Normalizes and validates APNs tokens (device tokens and Live Activity push tokens),
/// which iOS hands out as hex strings.
/// </summary>
public static class ApnsToken
{
    private const int MinLength = 32;
    private const int MaxLength = 512;
    private static readonly Regex hexRegex = new("^[0-9a-fA-F]+$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    /// <summary>Strips the spaces and angle brackets of NSData's description format.</summary>
    public static string Normalize(string token)
        => token.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("<", string.Empty, StringComparison.Ordinal)
            .Replace(">", string.Empty, StringComparison.Ordinal)
            .Trim();

    public static bool IsValid(string normalizedToken)
        => normalizedToken.Length >= MinLength &&
           normalizedToken.Length <= MaxLength &&
           hexRegex.IsMatch(normalizedToken);
}
