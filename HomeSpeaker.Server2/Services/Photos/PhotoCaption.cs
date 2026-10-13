using System.Globalization;

namespace HomeSpeaker.Server2.Services.Photos;

public static class PhotoCaption
{
    public static string? Place(PhotoEntry photo)
    {
        var city = clean(photo.City);
        var region = clean(photo.State) ?? clean(photo.Country);
        return (city, region) switch
        {
            (not null, not null) => $"{city}, {region}",
            (not null, null) => city,
            (null, not null) => region,
            _ => null,
        };
    }

    public static string When(DateTime takenAt, DateTime today)
    {
        var years = today.Year - takenAt.Year;
        if (today.Month < takenAt.Month || (today.Month == takenAt.Month && today.Day < takenAt.Day))
        {
            years--;
        }

        var month = takenAt.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        return years switch
        {
            < 1 => month,
            1 => $"{month} · 1 year ago",
            _ => string.Create(CultureInfo.InvariantCulture, $"{month} · {years} years ago"),
        };
    }

    private static string? clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
