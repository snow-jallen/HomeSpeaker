namespace HomeSpeaker.Server2.Settings;

public interface ISettingsWriter
{
    /// <summary>Persists the changes (null value deletes the key), then reloads configuration.</summary>
    void Save(IReadOnlyDictionary<string, string?> changes);
}
