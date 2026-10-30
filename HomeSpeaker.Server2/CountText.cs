using System.Globalization;

namespace HomeSpeaker.Server2;

/// <summary>Formats counts for display ("1 song", "12 songs").</summary>
public static class CountText
{
    public static string Songs(int count) => Of(count, "song");

    /// <summary>"1 group", "1,234 groups": <paramref name="noun"/> plus "s" unless the count is one.</summary>
    public static string Of(int count, string noun) =>
        count == 1 ? "1 " + noun : string.Create(CultureInfo.InvariantCulture, $"{count:n0} {noun}s");
}
