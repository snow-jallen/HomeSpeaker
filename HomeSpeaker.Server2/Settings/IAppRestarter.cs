namespace HomeSpeaker.Server2.Settings;

/// <summary>Stops the app so Docker (<c>restart: unless-stopped</c>) starts it again with the new settings.</summary>
public interface IAppRestarter
{
    void Restart();
}
