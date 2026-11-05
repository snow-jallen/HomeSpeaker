namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>The colours a person can wear. Stored and sent as these keys; the CSS maps each to a swatch.</summary>
public static class AnchorColors
{
    public static readonly IReadOnlyList<string> Palette = ["teal", "amber", "rose", "violet", "sky", "lime", "orange", "slate"];

    public static bool IsValid(string? color) => color is not null && Palette.Contains(color, StringComparer.Ordinal);

    public static string ForIndex(int index) => Palette[((index % Palette.Count) + Palette.Count) % Palette.Count];
}
