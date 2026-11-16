using HomeSpeaker.Server2.Services.BleTemperature;

namespace HomeSpeaker.Server2.Tests.BleTemperature;

public sealed class ReadingAgeTests
{
    private static readonly DateTime now = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);

    private static ReadingAge ageFor(TimeSpan old) => ReadingAge.Of(now - old, now);

    [Theory]
    [InlineData(0)]
    [InlineData(60)]
    [InlineData(299)]
    public void UnderFiveMinutes_ShowsValueWithoutNote(int seconds)
    {
        var age = ageFor(TimeSpan.FromSeconds(seconds));

        Assert.True(age.ShowsValue);
        Assert.Null(age.Note);
    }

    [Theory]
    [InlineData(300, "5 min old")]
    [InlineData(7 * 60 + 59, "7 min old")]
    [InlineData(20 * 60, "20 min old")]
    [InlineData(20 * 60 + 59, "20 min old")]
    public void FiveToTwentyMinutes_ShowsValueAndAge(int seconds, string note)
    {
        var age = ageFor(TimeSpan.FromSeconds(seconds));

        Assert.True(age.ShowsValue);
        Assert.Equal(note, age.Note);
    }

    [Theory]
    [InlineData(21 * 60, "No reading for 21 min")]
    [InlineData(34 * 60 + 30, "No reading for 34 min")]
    [InlineData(600 * 60, "No reading for 600 min")]
    public void OverTwentyMinutes_HidesValue(int seconds, string note)
    {
        var age = ageFor(TimeSpan.FromSeconds(seconds));

        Assert.False(age.ShowsValue);
        Assert.Equal(note, age.Note);
    }

    [Fact]
    public void NeverHeard_SaysNoReadingYet()
    {
        var age = ReadingAge.Of(null, now);

        Assert.False(age.ShowsValue);
        Assert.Equal("No reading yet", age.Note);
    }

    [Fact]
    public void ReadingFromTheFuture_CountsAsFresh()
    {
        var age = ReadingAge.Of(now.AddSeconds(30), now);

        Assert.True(age.ShowsValue);
        Assert.Null(age.Note);
    }
}
