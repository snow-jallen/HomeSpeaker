using HomeSpeaker.Server2.Services;
using HomeSpeaker.Server2.Services.Dashboard;
using HomeSpeaker.Server2.Tests.Photos;
using HomeSpeaker.Shared.Forecast;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace HomeSpeaker.Server2.Tests.Dashboard;

public sealed class HourlyForecastTests
{
    // Denver is UTC-6 in mid-October (MDT), so 2026-10-13 21:30 UTC is 15:30 local.
    private static readonly DateTime utcNow = new(2026, 10, 13, 21, 30, 0, DateTimeKind.Utc);

    private static ForecastStatus forecast(DateTime firstHour, int hours = 48, string? timezone = "America/Denver")
    {
        var status = new ForecastStatus { TimeZoneId = timezone };
        for (var i = 0; i < hours; i++)
        {
            var time = firstHour.AddHours(i);
            status.Hourly.Add(new ForecastData
            {
                DateTime = time,
                Temperature = 50 + i,
                PrecipitationChance = i * 5,
                WeatherCode = 0,
                IsDay = time.Hour is >= 7 and < 19,
                Conditions = "Clear",
            });
        }

        return status;
    }

    private static readonly DateTime midnight = new(2026, 10, 13, 0, 0, 0, DateTimeKind.Unspecified);

    [Fact]
    public void Select_StartsWithTheCurrentHourAsNow_InTheForecastsTimeZone()
    {
        var columns = HourlyColumn.Select(forecast(midnight), utcNow);

        Assert.Equal(8, columns.Count);
        Assert.Equal(["Now", "4p", "5p", "6p", "7p", "8p", "9p", "10p"], columns.Select(c => c.Label));
        Assert.Equal(65, columns[0].Temperature); // the 15:00 entry: 50 + 15
    }

    [Fact]
    public void Select_ADayOldForecast_DoesNotFreezeOnOldHours()
    {
        var stale = forecast(midnight);

        var nextDay = HourlyColumn.Select(stale, utcNow.AddHours(10)); // 01:30 local on the 14th

        Assert.Equal("Now", nextDay[0].Label);
        Assert.Equal(75, nextDay[0].Temperature); // the 14th 01:00 entry: 50 + 25
    }

    [Fact]
    public void Select_ForecastRunningOut_ReturnsWhatIsLeft_OrNothing()
    {
        var shortForecast = forecast(midnight, hours: 18); // ends at 17:00 on the 13th

        Assert.Equal(3, HourlyColumn.Select(shortForecast, utcNow).Count); // 15:00, 16:00, 17:00
        Assert.Empty(HourlyColumn.Select(shortForecast, utcNow.AddHours(5)));
    }

    [Fact]
    public void Select_MissingOrErroredForecast_IsEmpty()
    {
        Assert.Empty(HourlyColumn.Select(null, utcNow));
        Assert.Empty(HourlyColumn.Select(new ForecastStatus(), utcNow));
        Assert.Empty(HourlyColumn.Select(new ForecastStatus { Error = "boom", Hourly = forecast(midnight).Hourly }, utcNow));
    }

    [Fact]
    public void Select_UnknownTimeZone_FallsBackToServerLocal()
    {
        var columns = HourlyColumn.Select(forecast(midnight, timezone: "Not/AZone"), utcNow);

        Assert.NotEmpty(columns); // no exception; the exact hours depend on the machine zone
    }

    [Theory]
    [InlineData(0, "12a")]
    [InlineData(1, "1a")]
    [InlineData(11, "11a")]
    [InlineData(12, "12p")]
    [InlineData(15, "3p")]
    [InlineData(23, "11p")]
    public void FormatLabel_IsTwelveHourWithoutMinutes(int hour, string expected) =>
        Assert.Equal(expected, HourlyColumn.FormatLabel(new DateTime(2026, 10, 13, hour, 0, 0, DateTimeKind.Unspecified), isNow: false));

    [Fact]
    public void FormatLabel_NowWinsOverTheHour() =>
        Assert.Equal("Now", HourlyColumn.FormatLabel(new DateTime(2026, 10, 13, 15, 0, 0, DateTimeKind.Unspecified), isNow: true));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(9, 0)]
    [InlineData(10, 3)]
    [InlineData(30, 7)]
    [InlineData(50, 12)]
    [InlineData(100, 24)]
    public void RainBar_IsAFixedScaleOf24px_DrawnFromTenPercent(int percent, int expectedPx) =>
        Assert.Equal(expectedPx, new HourlyColumn("x", WeatherIcon.Rain, true, "", 50, percent).RainBarPx);

    [Fact]
    public void Select_AccessibleLabelFollowsTheIconNotTheMergedForecastText()
    {
        var status = forecast(midnight);
        status.Hourly[15].WeatherCode = 1;   // mainly clear: drawn as a sun
        status.Hourly[16].WeatherCode = 3;   // overcast: drawn as a cloud

        var columns = HourlyColumn.Select(status, utcNow);

        Assert.Equal(["Clear", "Cloudy"], columns.Take(2).Select(c => c.Conditions));
    }

    [Fact]
    public void Select_MissingDayFlag_DefaultsToDay()
    {
        var status = forecast(midnight);
        status.Hourly[15].IsDay = null;

        Assert.True(HourlyColumn.Select(status, utcNow)[0].IsDay);
    }

    [Fact]
    public void Select_RepeatedHourAtDstEnd_OnlyTheFirstIsNow()
    {
        var status = new ForecastStatus { TimeZoneId = "America/Denver" };
        var repeated = new DateTime(2026, 10, 13, 15, 0, 0, DateTimeKind.Unspecified);
        status.Hourly.Add(new ForecastData { DateTime = repeated, Temperature = 1, WeatherCode = 0 });
        status.Hourly.Add(new ForecastData { DateTime = repeated, Temperature = 2, WeatherCode = 0 });

        var columns = HourlyColumn.Select(status, utcNow);

        Assert.Equal(["Now", "3p"], columns.Select(c => c.Label));
    }

    [Fact]
    public void RainLabel_OnlyFromTwentyPercent()
    {
        Assert.False(new HourlyColumn("x", WeatherIcon.Rain, true, "", 50, 19).ShowRainLabel);
        Assert.True(new HourlyColumn("x", WeatherIcon.Rain, true, "", 50, 20).ShowRainLabel);
    }

    [Fact]
    public void Select_RainChanceIsClampedAndRounded()
    {
        var status = forecast(midnight);
        status.Hourly[15].PrecipitationChance = 140;
        status.Hourly[16].PrecipitationChance = null;
        status.Hourly[17].PrecipitationChance = 33.6;

        var columns = HourlyColumn.Select(status, utcNow);

        Assert.Equal([100, 0, 34], columns.Take(3).Select(c => c.RainPercent));
    }

    [Theory]
    [InlineData(0, WeatherIcon.Clear)]
    [InlineData(1, WeatherIcon.Clear)]
    [InlineData(2, WeatherIcon.PartlyCloudy)]
    [InlineData(3, WeatherIcon.Cloudy)]
    [InlineData(45, WeatherIcon.Fog)]
    [InlineData(48, WeatherIcon.Fog)]
    [InlineData(53, WeatherIcon.Drizzle)]
    [InlineData(57, WeatherIcon.Drizzle)]
    [InlineData(61, WeatherIcon.Rain)]
    [InlineData(67, WeatherIcon.Rain)]
    [InlineData(81, WeatherIcon.Rain)]
    [InlineData(71, WeatherIcon.Snow)]
    [InlineData(86, WeatherIcon.Snow)]
    [InlineData(95, WeatherIcon.Thunderstorm)]
    [InlineData(99, WeatherIcon.Thunderstorm)]
    [InlineData(1234, WeatherIcon.Cloudy)]
    [InlineData(null, WeatherIcon.Cloudy)]
    public void WeatherIconMap_CoversRepresentativeWmoCodes(int? code, WeatherIcon expected) =>
        Assert.Equal(expected, WeatherIconMap.From(code));

    [Fact]
    public void ForecastMerge_KeepsTheLastGoodForecastThroughAFailedRefresh()
    {
        var good = forecast(midnight);
        var failed = new ForecastStatus { Error = "boom" };

        Assert.Same(good, ForecastMerge.Latest(good, failed));
        Assert.Same(good, ForecastMerge.Latest(good, null));
    }

    [Fact]
    public void ForecastMerge_UsesTheRefreshWhenItSucceedsOrThereIsNothingToKeep()
    {
        var good = forecast(midnight);
        var newer = forecast(midnight);
        var failed = new ForecastStatus { Error = "boom" };

        Assert.Same(newer, ForecastMerge.Latest(good, newer));
        Assert.Same(failed, ForecastMerge.Latest(null, failed));
        Assert.Same(failed, ForecastMerge.Latest(new ForecastStatus { Error = "earlier" }, failed));
        Assert.Null(ForecastMerge.Latest(null, null));
    }

    [Fact]
    public async Task ForecastService_ExposesEveryHourWithCodeAndDayFlag_AndAsksForIsDay()
    {
        const string json = """
            {"timezone":"America/Denver","hourly":{
              "time":["2026-10-13T00:00","2026-10-13T01:00","2026-10-13T12:00"],
              "temperature_2m":[41.0,40.2,60.5],
              "precipitation_probability":[0,10,55],
              "weather_code":[0,3,61],
              "is_day":[0,0,1]}}
            """;
        var handler = new FakeHttpMessageHandler((_, _) => FakeHttpMessageHandler.Json(json));
        var service = new ForecastService(new HttpClient(handler), new ConfigurationBuilder().Build(), NullLogger<ForecastService>.Instance, new MemoryCache(new MemoryCacheOptions()), new FixedTimeProvider(new DateTimeOffset(utcNow)));

        var status = await service.GetForecastStatusAsync();

        Assert.Contains("is_day", Assert.Single(handler.Requests).Request.RequestUri!.Query, StringComparison.Ordinal);
        Assert.Equal("America/Denver", status.TimeZoneId);
        Assert.Equal(3, status.Hourly.Count);
        Assert.Equal(new DateTime(2026, 10, 13, 12, 0, 0), status.Hourly[2].DateTime);
        Assert.Equal(61, status.Hourly[2].WeatherCode);
        Assert.Equal(55, status.Hourly[2].PrecipitationChance);
        Assert.True(status.Hourly[2].IsDay);
        Assert.False(status.Hourly[0].IsDay);
        Assert.NotNull(status.TodayHigh); // the existing summaries are still produced
    }
}
