using System.Globalization;

namespace HomeSpeaker.Server2;

/// <summary>Formats counts for display ("1 song", "12 songs").</summary>
public static class CountText
{
    public static string Songs(int count) =>
        count == 1 ? "1 song" : string.Create(CultureInfo.InvariantCulture, $"{count:n0} songs");
}
