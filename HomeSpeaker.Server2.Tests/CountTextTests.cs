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
}
