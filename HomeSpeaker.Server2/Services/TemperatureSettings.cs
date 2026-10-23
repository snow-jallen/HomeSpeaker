namespace HomeSpeaker.Server2.Services;

/// <summary>The room and window-advice settings read from configuration. Window values are in Fahrenheit.</summary>
/// <param name="Rooms">Configured rooms in match order; empty means every Govee device is its own room.</param>
/// <param name="OutsideRoom">The outdoor room, or null when there is none.</param>
/// <param name="IndoorRoom">The room compared against outside, or null to use the first non-outside room.</param>
/// <param name="WindowAdvice">Whether advice and the push alert are on (always false without an outside room).</param>
/// <param name="WindowMinOutsideF">Outside temperatures below this always mean "close the windows".</param>
/// <param name="ThresholdF">The "about the same" margin.</param>
public sealed record TemperatureSettings(
    IReadOnlyList<TemperatureDeviceMatcher.RoomPattern> Rooms,
    string? OutsideRoom,
    string? IndoorRoom,
    bool WindowAdvice,
    double WindowMinOutsideF,
    double ThresholdF);
