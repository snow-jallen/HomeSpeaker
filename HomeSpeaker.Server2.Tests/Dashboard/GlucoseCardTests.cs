using HomeSpeaker.Server2.Services.Dashboard;
using HomeSpeaker.Shared.BloodSugar;

namespace HomeSpeaker.Server2.Tests.Dashboard;

public sealed class GlucoseCardTests
{
    private static readonly DateTime now = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

    private static BloodSugarReading reading(double sgv, int minutesAgo, string direction = "Flat") => new()
    {
        Sgv = sgv,
        DateString = now.AddMinutes(-minutesAgo),
        Direction = direction,
        Type = "sgv",
    };

    private static BloodSugarStatus status(params BloodSugarReading[] newestFirst) => new()
    {
        CurrentReading = newestFirst.FirstOrDefault(),
        RecentReadings = [.. newestFirst],
    };

    [Fact]
    public void Delta_IsCurrentMinusPrevious()
    {
        Assert.Equal(6, status(reading(126, 2), reading(120, 7)).Delta);
        Assert.Equal(-3, status(reading(117, 2), reading(120, 7)).Delta);
    }

    [Fact]
    public void Delta_OmittedWhenPreviousReadingIsOverFifteenMinutesBefore()
    {
        Assert.Null(status(reading(126, 2), reading(120, 20)).Delta);
        Assert.Equal(6, status(reading(126, 2), reading(120, 17)).Delta);
    }

    [Fact]
    public void Delta_OmittedWithoutAPreviousReading()
    {
        Assert.Null(status(reading(126, 2)).Delta);
        Assert.Null(new BloodSugarStatus().Delta);
    }

    [Theory]
    [InlineData(6, "+6")]
    [InlineData(-3, "−3")]
    [InlineData(0, "±0")]
    [InlineData(0.4, "±0")]
    public void DeltaText_IsSignedWithARealMinus(double delta, string expected) =>
        Assert.Equal(expected, GlucoseCard.FormatDelta(delta));

    [Fact]
    public void DeltaText_NullWhenNoDelta() => Assert.Null(GlucoseCard.FormatDelta(null));

    [Theory]
    [InlineData(0, false, "just now")]
    [InlineData(3, false, "3 min ago")]
    [InlineData(16, true, "Stale · 16 min ago")]
    [InlineData(42, true, "Stale · 42 min ago")]
    [InlineData(65, true, "Stale · 1 h 5 min ago")]
    public void AgeText_ReadsNaturally(int minutes, bool stale, string expected) =>
        Assert.Equal(expected, GlucoseCard.FormatAge(TimeSpan.FromMinutes(minutes), stale));

    [Fact]
    public void From_FreshReading_ShowsDeltaAndIsNotStale()
    {
        var card = GlucoseCard.From(status(reading(126, 3, "FortyFiveUp"), reading(120, 8)), now);

        Assert.NotNull(card);
        Assert.Equal("126", card.Value);
        Assert.Equal("↗", card.Arrow);
        Assert.Equal("+6", card.DeltaText);
        Assert.False(card.IsStale);
        Assert.Equal("3 min ago", card.AgeText);
        Assert.Equal("In Range", card.StatusText);
        Assert.NotNull(card.Chart);
    }

    [Fact]
    public void From_StaleReading_IsDimmedWithoutDelta()
    {
        var card = GlucoseCard.From(status(reading(126, 42), reading(120, 47)), now);

        Assert.NotNull(card);
        Assert.True(card.IsStale);
        Assert.Null(card.DeltaText);
        Assert.Equal("Stale · 42 min ago", card.AgeText);
    }

    [Fact]
    public void From_UnknownDirection_HasNoArrow()
    {
        var card = GlucoseCard.From(status(reading(126, 1, "NOT COMPUTABLE")), now);

        Assert.Null(card!.Arrow);
    }

    [Fact]
    public void From_NoReading_IsNull()
    {
        Assert.Null(GlucoseCard.From(null, now));
        Assert.Null(GlucoseCard.From(new BloodSugarStatus(), now));
    }

    [Fact]
    public void Chart_IgnoresReadingsOlderThanThreeHours_AndPutsLatestAtTheRight()
    {
        var chart = GlucoseChart.Build([reading(100, 200), reading(150, 90), reading(120, 0)], now);

        Assert.NotNull(chart);
        Assert.Equal(100, chart.Latest.X, 3);
        Assert.Equal(2, chart.Paths.Count);
        Assert.StartsWith("M50 ", chart.Paths[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Chart_ClampsOutliersToTheFixedRange()
    {
        var high = GlucoseChart.Build([reading(500, 1)], now)!;
        var low = GlucoseChart.Build([reading(10, 1)], now)!;

        Assert.Equal(0, high.Latest.Y, 3);
        Assert.Equal(100, low.Latest.Y, 3);
    }

    [Fact]
    public void Chart_TargetBandSitsBetween70And180()
    {
        var chart = GlucoseChart.Build([reading(100, 1)], now)!;

        Assert.Equal((300.0 - 180) / 260 * 100, chart.BandTopPercent, 3);
        Assert.Equal((180.0 - 70) / 260 * 100, chart.BandHeightPercent, 3);
    }

    [Fact]
    public void Chart_BreaksTheLineAtGapsButKeepsTimeOnTheXAxis()
    {
        var chart = GlucoseChart.Build([reading(100, 60), reading(105, 55), reading(130, 10), reading(135, 5)], now)!;

        Assert.Equal(2, chart.Paths.Count);
    }

    [Fact]
    public void Chart_LoneReadingStillDrawsADot()
    {
        var chart = GlucoseChart.Build([reading(100, 1)], now)!;

        Assert.Equal(2, Assert.Single(chart.Paths).Count(c => c is 'M' or 'L'));
    }

    [Fact]
    public void Chart_NoReadingsInWindow_IsNull() =>
        Assert.Null(GlucoseChart.Build([reading(100, 400)], now));

    [Fact]
    public void Chart_IsIndependentOfInputOrder()
    {
        var oldestFirst = GlucoseChart.Build([reading(100, 30), reading(110, 20), reading(120, 10)], now)!;
        var newestFirst = GlucoseChart.Build([reading(120, 10), reading(110, 20), reading(100, 30)], now)!;

        Assert.Equal(oldestFirst.Paths, newestFirst.Paths);
    }
}
