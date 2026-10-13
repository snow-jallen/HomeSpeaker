namespace HomeSpeaker.Server2;

/// <summary>
/// The server's verdict on whether a request comes from a kiosk. <see cref="Auto"/> means the
/// server cannot tell and the browser decides from its screen size.
/// </summary>
public enum KioskMode
{
    On,
    Off,
    Auto,
}
