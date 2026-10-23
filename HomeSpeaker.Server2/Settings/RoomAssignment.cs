namespace HomeSpeaker.Server2.Settings;

/// <summary>One Govee sensor in the room picker: the room it reports for (null hides it) and whether it is the outside one.</summary>
public sealed record RoomAssignment(string DeviceName, string? Room, bool IsOutside);
