using System.Text.RegularExpressions;
using HomeSpeaker.Shared;

namespace HomeSpeaker.Server2.Services.MediaSearch;

/// <summary>
/// Picks the library item a spoken request most likely means. Speech transcription drops
/// punctuation and track numbers and gets the odd letter wrong, so names are compared
/// normalized, then by word overlap, then by edit distance.
/// </summary>
public static partial class MediaMatcher
{
    private const double MinimumScore = 0.5;
    // Enough for "hymns playlist" to pick "Hymns for Home and Church" over an exact "Hymns" stream.
    private const double KindHintBonus = 0.2;

    private static readonly (string Word, MediaKind Kind)[] kindWords =
    [
        ("playlist", MediaKind.Playlist),
        ("radio station", MediaKind.Stream),
        ("station", MediaKind.Stream),
        ("radio", MediaKind.Stream),
        ("stream", MediaKind.Stream),
        ("album", MediaKind.Album),
        ("song", MediaKind.Song),
    ];

    // Siri hears some names as genres and passes them as run-together codes:
    // "classical kids" arrives as "classical" and "kidsfamily".
    private static readonly Dictionary<string, string> genreCodes = new(StringComparer.Ordinal)
    {
        ["kidsfamily"] = "kids family",
        ["childrensmusic"] = "childrens music",
        ["christiangospel"] = "christian gospel",
        ["easylistening"] = "easy listening",
        ["hiphoprap"] = "hip hop rap",
        ["newage"] = "new age",
        ["rbsoul"] = "r and b soul",
        ["singersongwriter"] = "singer songwriter",
        ["worldmusic"] = "world music",
    };

    public static MediaCandidate? Match(IEnumerable<MediaCandidate> candidates, MediaQuery query)
    {
        var full = Normalize(query.Text);
        var kind = query.Kind;
        var artist = query.Artist is { } a ? Normalize(a) : "";
        if (full.Length == 0 && artist.Length > 0)
        {
            (full, kind, artist) = (artist, MediaKind.Artist, "");
        }

        if (full.Length == 0)
        {
            return null;
        }

        // "hymns playlist" is also read as "hymns" with a playlist hint.
        var (stripped, strippedKind) = splitKindWord(full, kind);

        MediaCandidate? best = null;
        var bestScore = 0.0;
        foreach (var candidate in candidates)
        {
            if (artist.Length > 0 && candidate.Kind is MediaKind.Song or MediaKind.Album
                && !Normalize(candidate.Artist ?? "").Contains(artist, StringComparison.Ordinal))
            {
                continue;
            }

            var name = Normalize(candidate.Name);
            var score = scoreWithHint(full, kind, candidate.Kind, name, wholeRequest: true);
            if (stripped != full)
            {
                score = Math.Max(score, scoreWithHint(stripped, strippedKind, candidate.Kind, name, wholeRequest: false));
            }

            if (score < MinimumScore)
            {
                continue;
            }

            if (score > bestScore || (score == bestScore && best != null && candidate.Kind < best.Kind))
            {
                (best, bestScore) = (candidate, score);
            }
        }

        return best;
    }

    /// <summary>Lowercase words only: "1039 - King's Singers &amp; Friends" becomes "kings singers and friends".</summary>
    public static string Normalize(string value)
    {
        var text = value.ToLowerInvariant().Replace("&", " and ", StringComparison.Ordinal);
        text = apostrophes().Replace(text, "");
        text = trackNumber().Replace(text, "");
        text = nonWord().Replace(text, " ").Trim();
        text = string.Join(' ', text.Split(' ').Select(word => genreCodes.GetValueOrDefault(word, word)));
        return text.StartsWith("the ", StringComparison.Ordinal) ? text[4..] : text;
    }

    /// <summary>How well the spoken text names a candidate, from 0 to 1 (both already normalized).</summary>
    public static double Score(string query, string name)
    {
        if (name.Length == 0)
        {
            return 0;
        }

        if (query == name)
        {
            return 1;
        }

        var score = 0.0;
        if (name.StartsWith(query + " ", StringComparison.Ordinal))
        {
            score = 0.85;
        }
        else if ((" " + name + " ").Contains(" " + query + " ", StringComparison.Ordinal))
        {
            score = 0.75;
        }

        var queryWords = query.Split(' ').ToHashSet();
        var nameWords = name.Split(' ').ToHashSet();
        var shared = queryWords.Count(nameWords.Contains);
        score = Math.Max(score, 0.7 * 2.0 * shared / (queryWords.Count + nameWords.Count));

        var longer = Math.Max(query.Length, name.Length);
        if (Math.Min(query.Length, name.Length) >= longer * 0.7)
        {
            var similarity = 1.0 - ((double)editDistance(query, name) / longer);
            if (similarity >= 0.8)
            {
                score = Math.Max(score, 0.9 * similarity);
            }
        }

        return score;
    }

    /// <summary>
    /// Adds the hint bonus. An exact match on everything said also beats any reading with the
    /// kind word dropped, so a playlist named "Christmas Radio" wins "christmas radio" over a "Christmas" stream.
    /// </summary>
    private static double scoreWithHint(string text, MediaKind? hint, MediaKind kind, string name, bool wholeRequest)
    {
        var score = Score(text, name);
        if (score == 0)
        {
            return 0;
        }

        if (wholeRequest && score == 1)
        {
            score += 2 * KindHintBonus;
        }

        return kind == hint ? score + KindHintBonus : score;
    }

    /// <summary>"hymns playlist" or "playlist hymns" asks for a playlist named "hymns".</summary>
    private static (string Text, MediaKind? Kind) splitKindWord(string text, MediaKind? kind)
    {
        foreach (var (word, wordKind) in kindWords)
        {
            if (text.EndsWith(" " + word, StringComparison.Ordinal))
            {
                return (text[..^(word.Length + 1)], kind ?? wordKind);
            }

            if (text.StartsWith(word + " ", StringComparison.Ordinal))
            {
                return (Normalize(text[(word.Length + 1)..]), kind ?? wordKind);
            }
        }

        return (text, kind);
    }

    private static int editDistance(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }

    [GeneratedRegex("['’]")]
    private static partial Regex apostrophes();

    [GeneratedRegex(@"^\s*\d+\s*[-.]\s+")]
    private static partial Regex trackNumber();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex nonWord();
}
