using HomeSpeaker.Server2.Settings;

namespace HomeSpeaker.Server2.Tests.Settings;

public sealed class RoomAssignmentsTests
{
    private const string OwnerRules = "Outside: Outside; Greenhouse: Greenhouse; Emma: Emma; Boys: Boy; Downstairs: Downstairs, Mom, Dad, Parent; Girls: Girl";

    private static readonly string[] ownerDevices = ["Outside Sensor", "Girl room", "Emma's room", "Boy room", "Mom and Dad"];

    [Fact]
    public void RoomAssignments_FromOwnerRules_MapsDevicesToRooms()
    {
        var assignments = RoomAssignments.FromRules(ownerDevices, OwnerRules, outsideRoom: null);

        var byDevice = assignments.ToDictionary(a => a.DeviceName, a => a.Room, StringComparer.Ordinal);
        Assert.Equal("Outside", byDevice["Outside Sensor"]);
        Assert.Equal("Girls", byDevice["Girl room"]);
        Assert.Equal("Emma", byDevice["Emma's room"]);
        Assert.Equal("Boys", byDevice["Boy room"]);
        Assert.Equal("Downstairs", byDevice["Mom and Dad"]);

        // Ordered by the rules' room order.
        Assert.Equal(["Outside", "Emma", "Boys", "Downstairs", "Girls"], assignments.Select(a => a.Room));
        Assert.Equal([true, false, false, false, false], assignments.Select(a => a.IsOutside));
    }

    [Fact]
    public void RoomAssignments_UnmatchedDevices_ComeLastWithNoRoom()
    {
        var assignments = RoomAssignments.FromRules(["Garage", "Kitchen sensor"], "Kitchen: Kitchen sensor", outsideRoom: null);

        Assert.Equal(
            [new RoomAssignment("Kitchen sensor", "Kitchen", false), new RoomAssignment("Garage", null, false)],
            assignments);
    }

    [Fact]
    public void RoomAssignments_NoRules_EveryDeviceIsItsOwnRoom()
    {
        var assignments = RoomAssignments.FromRules(["A", "B"], roomsSetting: null, outsideRoom: null);

        Assert.Equal([new RoomAssignment("A", "A", false), new RoomAssignment("B", "B", false)], assignments);
    }

    [Fact]
    public void RoomAssignments_ToSettings_WritesExactDeviceNamesInOrder()
    {
        var (rooms, outside) = RoomAssignments.ToSettings(
        [
            new RoomAssignment("Outside Sensor", "Outside", true),
            new RoomAssignment("Mom and Dad", " Downstairs ", false),
            new RoomAssignment("Girl room", "Girls", false),
        ]);

        Assert.Equal("Outside: Outside Sensor; Downstairs: Mom and Dad; Girls: Girl room", rooms);
        Assert.Equal("Outside", outside);
    }

    [Fact]
    public void RoomAssignments_BlankRoom_IsOmitted()
    {
        var (rooms, outside) = RoomAssignments.ToSettings(
        [
            new RoomAssignment("Garage", "  ", true),
            new RoomAssignment("Girl room", "Girls", false),
            new RoomAssignment("Spare", null, false),
        ]);

        Assert.Equal("Girls: Girl room", rooms);
        Assert.Null(outside);
    }

    [Fact]
    public void RoomAssignments_IsRepresentable_FalseForMultiPatternRooms()
    {
        Assert.False(RoomAssignments.IsRepresentable(OwnerRules, ownerDevices));
        Assert.False(RoomAssignments.IsRepresentable("Downstairs: Mom and Dad, Parent", ownerDevices));
        Assert.False(RoomAssignments.IsRepresentable("Girls: Girl", ownerDevices));
        Assert.True(RoomAssignments.IsRepresentable("Girls: Girl room; Outside: Outside Sensor", ownerDevices));
        Assert.True(RoomAssignments.IsRepresentable(null, ownerDevices));
        Assert.True(RoomAssignments.IsRepresentable("", ownerDevices));
    }

    [Fact]
    public void RoomAssignments_RoundTrip_FromRulesToSettingsToRules()
    {
        IReadOnlyList<RoomAssignment> original =
        [
            new RoomAssignment("Girl room", "Girls", false),
            new RoomAssignment("Outside Sensor", "Yard", true),
            new RoomAssignment("Boy room", "Boys", false),
            new RoomAssignment("Emma's room", null, false),
            new RoomAssignment("Mom and Dad", null, false),
        ];

        var (rooms, outside) = RoomAssignments.ToSettings(original);
        var roundTripped = RoomAssignments.FromRules(ownerDevices, rooms, outside);

        Assert.True(RoomAssignments.IsRepresentable(rooms, ownerDevices));
        Assert.Equal(original, roundTripped);
    }
}
