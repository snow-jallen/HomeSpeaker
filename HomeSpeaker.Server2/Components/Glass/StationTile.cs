using System.Globalization;
using System.Text;

namespace HomeSpeaker.Server2.Components.Glass;

/// <summary>
/// The look of a generated station tile: a hue and 1–2 initials derived from the station name.
/// The hue uses FNV-1a over the UTF-8 bytes (not string.GetHashCode, which changes per process),
/// so a station keeps its colour across restarts and devices.
/// </summary>
public sealed record StationTile(int Hue, string Initials)
{
    private const uint FnvOffsetBasis = 2166136261;
    private const uint FnvPrime = 16777619;
    private const string FallbackInitials = "♪";

    private static readonly HashSet<string> fillerWords = new(StringComparer.OrdinalIgnoreCase) { "Stream", "Radio", "The", "Your" };

    public static StationTile For(string? name) => new(HueFor(name), InitialsFor(name));

    /// <summary>A hue in [0, 360), stable for a name regardless of case or surrounding spaces.</summary>
    public static int HueFor(string? name)
    {
        var hash = FnvOffsetBasis;
        foreach (var b in Encoding.UTF8.GetBytes((name ?? string.Empty).Trim().ToUpperInvariant()))
        {
            hash ^= b;
            hash *= FnvPrime;
        }

        return (int)(hash % 360);
    }

    /// <summary>
    /// First letters of the first two meaningful words ("Classical Kids" → "CK"), skipping filler words
    /// unless that would leave nothing. A lone all-caps word such as a call sign keeps two letters ("KBAQ" → "KB").
    /// </summary>
    public static string InitialsFor(string? name)
    {
        var words = (name ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Any(char.IsLetterOrDigit))
            .ToList();
        if (words.Count == 0)
        {
            return FallbackInitials;
        }

        var meaningful = words.Where(w => !fillerWords.Contains(w)).ToList();
        if (meaningful.Count == 0)
        {
            meaningful = words;
        }

        if (meaningful.Count == 1)
        {
            var letters = meaningful[0].Where(char.IsLetterOrDigit).ToArray();
            var isCallSign = letters.Length >= 2 && letters.All(c => char.IsLetter(c) && char.IsUpper(c));
            return new string(letters, 0, isCallSign ? 2 : 1).ToUpperInvariant();
        }

        return string.Concat(meaningful.Take(2).Select(w => char.ToUpperInvariant(w.First(char.IsLetterOrDigit))));
    }

    /// <summary>The tile background: a diagonal two-stop gradient from the hue to a deeper neighbour.</summary>
    public string Background => string.Create(
        CultureInfo.InvariantCulture,
        $"linear-gradient(135deg, hsl({Hue} 58% 36%), hsl({(Hue + 40) % 360} 64% 22%))");
}
