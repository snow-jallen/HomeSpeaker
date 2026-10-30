namespace HomeSpeaker.Server2.Tests;

public sealed class CountTextTests
{
    [Theory]
    [InlineData(0, "0 songs")]
    [InlineData(1, "1 song")]
    [InlineData(2, "2 songs")]
    [InlineData(1234, "1,234 songs")]
    public void Songs_PluralisesCorrectly(int count, string expected)
    {
        Assert.Equal(expected, CountText.Songs(count));
    }

    [Theory]
    [InlineData(1, "1 group")]
    [InlineData(290, "290 groups")]
    public void Of_PluralisesAnyNoun(int count, string expected)
    {
        Assert.Equal(expected, CountText.Of(count, "group"));
    }
}
