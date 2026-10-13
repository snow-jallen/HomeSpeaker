using HomeSpeaker.Server2.Services.Photos;

namespace HomeSpeaker.Server2.Tests.Photos;

public sealed class PhotoCaptionTests
{
    private static PhotoEntry photo(string? city, string? state, string? country) =>
        new() { AssetId = "x", City = city, State = state, Country = country };

    [Fact]
    public void Place_CityAndState() => Assert.Equal("Bear Lake, Utah", PhotoCaption.Place(photo("Bear Lake", "Utah", "United States of America")));

    [Fact]
    public void Place_CityAndCountry_WhenNoState() => Assert.Equal("Cancún, Mexico", PhotoCaption.Place(photo("Cancún", null, "Mexico")));

    [Fact]
    public void Place_RegionOnly() => Assert.Equal("Utah", PhotoCaption.Place(photo(null, "Utah", null)));

    [Fact]
    public void Place_WithNoLocation_ReturnsNull() => Assert.Null(PhotoCaption.Place(photo(null, " ", null)));

    [Theory]
    [InlineData("2021-08-14", "2026-10-13", "August 2021 · 5 years ago")]
    [InlineData("2021-11-01", "2026-10-13", "November 2021 · 4 years ago")]
    [InlineData("2025-10-01", "2026-10-13", "October 2025 · 1 year ago")]
    [InlineData("2026-03-05", "2026-10-13", "March 2026")]
    public void When_FormatsMonthAndYearsAgo(string taken, string today, string expected) =>
        Assert.Equal(expected, PhotoCaption.When(DateTime.Parse(taken, System.Globalization.CultureInfo.InvariantCulture), DateTime.Parse(today, System.Globalization.CultureInfo.InvariantCulture)));
}
