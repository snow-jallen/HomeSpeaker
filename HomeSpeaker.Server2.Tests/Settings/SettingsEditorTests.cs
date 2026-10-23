using HomeSpeaker.Server2.Settings;

namespace HomeSpeaker.Server2.Tests.Settings;

public sealed class SettingsEditorTests
{
    private static SettingsEditor editor() => new(
    [
        new(SettingKeys.TemperatureRooms, "Rooms", "Yard: Outside Sensor", IsSecret: false, IsOverridden: false),
        new(SettingKeys.TemperatureOutsideRoom, "Outside room", "Yard", IsSecret: false, IsOverridden: false),
        new(SettingKeys.TemperatureIndoorRoom, "Indoor room", null, IsSecret: false, IsOverridden: false),
        new(SettingKeys.NightscoutUrl, "Nightscout URL", null, IsSecret: false, IsOverridden: false),
    ]);

    [Fact]
    public void ValidChanges_RoomsInvalid_HoldsBackOutsideAndIndoorRooms()
    {
        var e = editor();
        e[SettingKeys.TemperatureRooms] = "Garden: Outside Sensor";
        e[SettingKeys.TemperatureOutsideRoom] = "Garden";
        e[SettingKeys.TemperatureIndoorRoom] = "Kitchen";
        e[SettingKeys.NightscoutUrl] = "https://ns.example";
        e.Errors[SettingKeys.TemperatureRooms] = "Room names can't contain ; : or ,";

        var changes = e.ValidChanges();

        Assert.Equal(new Dictionary<string, string?> { [SettingKeys.NightscoutUrl] = "https://ns.example" }, changes);
        Assert.True(e.Errors.ContainsKey(SettingKeys.TemperatureOutsideRoom));
        Assert.True(e.Errors.ContainsKey(SettingKeys.TemperatureIndoorRoom));
    }

    [Fact]
    public void ValidChanges_RoomsValid_SavesRoomsWithOutsideRoom()
    {
        var e = editor();
        e[SettingKeys.TemperatureRooms] = "Garden: Outside Sensor";
        e[SettingKeys.TemperatureOutsideRoom] = "Garden";

        var changes = e.ValidChanges();

        Assert.Equal("Garden: Outside Sensor", changes[SettingKeys.TemperatureRooms]);
        Assert.Equal("Garden", changes[SettingKeys.TemperatureOutsideRoom]);
    }
}
