using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HomeSpeaker.Server2.Services.MediaSearch;

namespace HomeSpeaker.Server2.Services.AlbumArt;

/// <summary>
/// How closely a source's names match a group, from 0 to 1: the average of the artist and album (or title) similarities.
/// Typed text has no artist/album split, so it is compared with "artist album" as a whole.
/// </summary>
public static partial class AlbumArtScore
{
    private const double EditionNotePenalty = 0.95;

    public static double Score(AlbumArtQuery query, string? artist, string? album, string? title)
    {
        if (query.IsFreeText)
        {
            var found = string.Join(' ', new[] { artist, album ?? title }.Where(part => !string.IsNullOrWhiteSpace(part)));
            return Math.Round(similarity(query.Name, found), 2);
        }

        var parts = new List<double>(2);
        if (!string.IsNullOrWhiteSpace(query.Artist))
        {
            parts.Add(similarity(query.Artist, artist));
        }

        parts.Add(query.IsAlbum ? similarity(query.Album!, album) : similarity(query.Title ?? string.Empty, title));
        return Math.Round(Math.Min(1, parts.Average()), 2);
    }

    /// <summary>Lowercase ASCII words without edition notes: "Björk - Début (Remastered)" becomes "bjork debut".</summary>
    public static string Clean(string value)
    {
        var plain = withoutAccents(value);
        var stripped = MediaMatcher.Normalize(bracketed().Replace(plain, " "));
        return stripped.Length > 0 ? stripped : MediaMatcher.Normalize(plain);
    }

    private static string withoutAccents(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static double similarity(string wanted, string? found)
    {
        if (string.IsNullOrWhiteSpace(found))
        {
            return 0;
        }

        // Matching only once edition notes are stripped ("Thriller (Steve Aoki Remix)") ranks just below an exact match.
        var exact = bothWays(MediaMatcher.Normalize(withoutAccents(wanted)), MediaMatcher.Normalize(withoutAccents(found)));
        var cleaned = bothWays(Clean(wanted), Clean(found));
        return Math.Max(exact, cleaned * EditionNotePenalty);
    }

    private static double bothWays(string a, string b) => Math.Max(MediaMatcher.Score(a, b), MediaMatcher.Score(b, a));

    [GeneratedRegex(@"\([^)]*\)|\[[^\]]*\]")]
    private static partial Regex bracketed();
}
