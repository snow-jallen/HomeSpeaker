using HomeSpeaker.Server2.Components.Glass;

namespace HomeSpeaker.Server2.Tests;

public sealed class StationTileTests
{
    [Theory]
    [InlineData("Classical Kids", "CK")]
    [InlineData("Radio Clasique", "C")] // "Radio" is filler, leaving one ordinary word: its first letter
    [InlineData("KBAQ", "KB")] // a lone all-caps call sign keeps two letters
    [InlineData("The Jazz Stream", "J")]
    [InlineData("Your Classical Radio", "C")]
    [InlineData("Radio", "R")] // only filler: fall back to the filler words rather than nothing
    [InlineData("The Radio Stream", "TR")]
    [InlineData("lo-fi beats to relax", "LB")]
    [InlineData("  bbc   radio 3 ", "3")]
    [InlineData("Classical 89", "89")] // a numeric word is more recognisable than a letter
    [InlineData("98.7 KLOS", "98")] // the whole-number part of a frequency
    [InlineData("KZ 104 FM", "104")]
    [InlineData("Hits 1055 FM", "105")] // capped at three characters
    [InlineData("89", "89")]
    [InlineData("Radio 1", "1")]
    [InlineData("Jazz24", "J")] // digits glued to letters are not a numeric word
    [InlineData("Chamber 3 Music 7", "3")] // the first numeric word wins
    [InlineData("Jazz", "J")]
    public void InitialsSkipFillerWords(string name, string expected) =>
        Assert.Equal(expected, StationTile.InitialsFor(name));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!! ???")]
    public void InitialsAreNeverEmpty(string? name) => Assert.False(string.IsNullOrEmpty(StationTile.InitialsFor(name)));

    [Theory]
    [InlineData("Classical Kids", 39)]
    [InlineData("KBAQ", 334)]
    [InlineData("Radio Clasique", 139)]
    public void HueIsPinned(string name, int expected)
    {
        // FNV-1a over the upper-cased UTF-8 bytes, so every process and device agrees. If this changes,
        // every station's colour changes, so update these values only on purpose.
        Assert.Equal(expected, StationTile.HueFor(name));
    }

    [Fact]
    public void HueIsInRangeForAnyName()
    {
        Assert.InRange(StationTile.HueFor(null), 0, 359);
        Assert.InRange(StationTile.HueFor("Ünïcødé ☕ FM"), 0, 359);
    }

    [Fact]
    public void HueIgnoresCaseAndSurroundingSpaces() =>
        Assert.Equal(StationTile.HueFor("Classical Kids"), StationTile.HueFor("  classical kids "));

    [Fact]
    public void DifferentNamesUsuallyGetDifferentHues()
    {
        var hues = new[] { "Classical Kids", "KBAQ", "Radio Clasique", "Jazz24", "KUSC", "Lofi Girl" }
            .Select(StationTile.HueFor)
            .Distinct()
            .Count();
        Assert.True(hues >= 5, $"Expected the hues to spread out, got {hues} distinct values");
    }

    [Fact]
    public void BackgroundIsATwoStopGradientUsingTheHue()
    {
        var tile = new StationTile(350, "CK");
        Assert.Equal("linear-gradient(135deg, hsl(350 58% 36%), hsl(30 64% 22%))", tile.Background);
    }
}
