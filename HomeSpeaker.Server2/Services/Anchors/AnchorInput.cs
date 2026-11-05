using System.Globalization;

namespace HomeSpeaker.Server2.Services.Anchors;

/// <summary>Parsing and limits for what people type into anchor forms and requests.</summary>
public static class AnchorInput
{
    public const int MaxTitleLength = 120;
    public const int MaxNameLength = 40;
    public const int MaxEmojiLength = 16;

    public static bool TryTitle(string? text, out string title)
    {
        title = (text ?? string.Empty).Trim();
        return title.Length is > 0 and <= MaxTitleLength;
    }

    public static bool TryName(string? text, out string name)
    {
        name = (text ?? string.Empty).Trim();
        return name.Length is > 0 and <= MaxNameLength;
    }

    public static bool TrySlot(string? text, out AnchorSlot slot) =>
        Enum.TryParse(text, ignoreCase: true, out slot) && Enum.IsDefined(slot) && !int.TryParse(text, CultureInfo.InvariantCulture, out _);

    /// <summary>HH:mm or H:mm; empty means "no target time".</summary>
    public static bool TryTargetTime(string? text, out TimeOnly? time)
    {
        time = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (TimeOnly.TryParseExact(text.Trim(), ["HH:mm", "H:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            time = parsed;
            return true;
        }

        return false;
    }

    public static string? FormatTime(TimeOnly? time) => time?.ToString("HH:mm", CultureInfo.InvariantCulture);

    public static string Wire(AnchorSlot slot) => slot.ToString().ToLowerInvariant();

    public static string Wire(AnchorCell cell) => cell.ToString().ToLowerInvariant();

    public static string Id(int id) => id.ToString(CultureInfo.InvariantCulture);

    public static bool TryLocalId(string? id, out int value) =>
        int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out value);
}
