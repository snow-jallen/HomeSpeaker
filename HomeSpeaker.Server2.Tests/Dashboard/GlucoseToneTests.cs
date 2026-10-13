using HomeSpeaker.Server2.Services.Dashboard;

namespace HomeSpeaker.Server2.Tests.Dashboard;

public sealed class GlucoseToneTests
{
    [Theory]
    [InlineData(null, GlucoseTone.Unknown)]
    [InlineData(55.0, GlucoseTone.Urgent)]
    [InlineData(69.0, GlucoseTone.Urgent)]
    [InlineData(70.0, GlucoseTone.Caution)]
    [InlineData(79.0, GlucoseTone.Caution)]
    [InlineData(80.0, GlucoseTone.Normal)]
    [InlineData(180.0, GlucoseTone.Normal)]
    [InlineData(181.0, GlucoseTone.Caution)]
    [InlineData(250.0, GlucoseTone.Caution)]
    [InlineData(251.0, GlucoseTone.Urgent)]
    public void From_MatchesBloodSugarStatusThresholds(double? sgv, GlucoseTone expected) =>
        Assert.Equal(expected, GlucoseToneMap.From(sgv));
}
