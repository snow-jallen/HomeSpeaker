using HomeSpeaker.Server2.Settings;

namespace HomeSpeaker.Server2.Tests.Settings;

public sealed class SettingKeysTests
{
    [Theory]
    [InlineData(SettingKeys.PhotoSource)]
    [InlineData(SettingKeys.ImmichBaseUrl)]
    [InlineData(SettingKeys.ImmichApiKey)]
    [InlineData(SettingKeys.ImmichAlbumId)]
    [InlineData(SettingKeys.SlideSeconds)]
    [InlineData(SettingKeys.NightscoutUrl)]
    [InlineData(SettingKeys.TemperatureApiBaseUrl)]
    [InlineData(SettingKeys.TemperatureApiKey)]
    [InlineData(SettingKeys.Anchors)]
    [InlineData("immich:baseurl")]
    public void RequiresRestart_StartupKeys_True(string key) => Assert.True(SettingKeys.RequiresRestart(key));

    [Theory]
    [InlineData(SettingKeys.ForecastLatitude)]
    [InlineData(SettingKeys.ForecastLongitude)]
    [InlineData(SettingKeys.IdleSeconds)]
    [InlineData(SettingKeys.TemperatureUnit)]
    [InlineData(SettingKeys.TemperatureRooms)]
    [InlineData(SettingKeys.TemperatureOutsideRoom)]
    [InlineData(SettingKeys.TemperatureIndoorRoom)]
    [InlineData(SettingKeys.TemperatureWindowAdvice)]
    [InlineData(SettingKeys.TemperatureThreshold)]
    [InlineData("Setup:Completed")]
    public void RequiresRestart_LiveKeys_False(string key) => Assert.False(SettingKeys.RequiresRestart(key));
}
