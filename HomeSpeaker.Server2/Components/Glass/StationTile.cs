using System.Globalization;
using System.Text;

namespace HomeSpeaker.Server2.Components.Glass;

/// <summary>
/// The look of a generated station tile: a hue and 1–3 initials derived from the station name.
/// The hue uses FNV-1a over the UTF-8 bytes (not string.GetHashCode, which changes per process),
/// so a station keeps its colour across restarts and devices.
/// </summary>
public sealed record StationTile(int Hue, string Initials)
{
    private const uint FnvOffsetBasis = 2166136261;
    private const uint FnvPrime = 16777619;
    private const int MaxInitialsLength = 3;
    private const string FallbackInitials = "♪";

    private static readonly HashSet<string> fillerWords = new(StringComparer.OrdinalIgnoreCase) { "Stream", "Radio", "The", "Your" };

    public static StationTile For(string? name) => new(HueFor(name), InitialsFor(name));

    /// <summary>A hue in [0, 360), stable for a name regardless of case or surrounding spaces.</summary>
    public static int HueFor(string? name)
    {
        var hash = FnvOffsetBasis;
        unchecked
        {
            foreach (var b in Encoding.UTF8.GetBytes((name ?? string.Empty).Trim().ToUpperInvariant()))
            {
                hash ^= b;
                hash *= FnvPrime;
            }
        }

        return (int)(hash % 360);
    }

    /// <summary>
    /// First letters of the first two meaningful words ("Classical Kids" → "CK"), skipping filler words
    /// unless that would leave nothing. A lone all-caps word such as a call sign keeps two letters ("KBAQ" → "KB").
    /// A numeric word (all digits and punctuation: a frequency or channel such as "89" or "98.7") is kept whole, with
    /// one letter from the name beside it when that fits in <see cref="MaxInitialsLength"/> characters, in the order
    /// the name has them: "Classical 89" → "C89", "98.7 KLOS" → "98K", "BBC Radio 3" → "B3", "KZ 104 FM" → "104".
    /// A number of four or more digits cannot be shown faithfully, so it is ignored ("Hits 1055 FM" → "HF").
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

        var numberIndex = meaningful.FindIndex(w => numberIn(w) is not null);
        if (numberIndex >= 0)
        {
            var number = numberIn(meaningful[numberIndex])!;
            if (number.Length <= MaxInitialsLength)
            {
                if (number.Length == MaxInitialsLength)
                {
                    return number;
                }

                // Pair a short number with the first letter word (a filler word if nothing else), in name order.
                var letterWord = meaningful.FirstOrDefault(w => numberIn(w) is null) ?? words.FirstOrDefault(w => numberIn(w) is null);
                if (letterWord is null)
                {
                    return number;
                }

                var letter = char.ToUpperInvariant(letterWord.First(char.IsLetterOrDigit)).ToString();
                return words.IndexOf(letterWord) < words.IndexOf(meaningful[numberIndex]) ? letter + number : number + letter;
            }

            // Four or more digits: fall back to the letters rather than show a different number.
            var lettered = meaningful.Where(w => numberIn(w) is null).ToList();
            if (lettered.Count > 0)
            {
                meaningful = lettered;
            }
        }

        if (meaningful.Count == 1)
        {
            var letters = meaningful[0].Where(char.IsLetterOrDigit).ToArray();
            var isCallSign = letters.Length >= 2 && letters.All(c => char.IsLetter(c) && char.IsUpper(c));
            return new string(letters, 0, isCallSign ? 2 : 1).ToUpperInvariant();
        }

        return string.Concat(meaningful.Take(2).Select(w => char.ToUpperInvariant(w.First(char.IsLetterOrDigit))));
    }

    /// <summary>
    /// The whole leading run of digits of a word made only of digits and punctuation ("98.7" → "98"),
    /// or null when the word contains letters.
    /// </summary>
    private static string? numberIn(string word)
    {
        var alphanumerics = word.Where(char.IsLetterOrDigit).ToList();
        if (alphanumerics.Count == 0 || !alphanumerics.All(char.IsDigit))
        {
            return null;
        }

        var leadingRun = word.SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit);
        return new string(leadingRun.ToArray());
    }

    /// <summary>The tile background: a diagonal two-stop gradient from the hue to a deeper neighbour.</summary>
    public string Background => string.Create(
        CultureInfo.InvariantCulture,
        $"linear-gradient(135deg, hsl({Hue} 58% 36%), hsl({(Hue + 40) % 360} 64% 22%))");
}
