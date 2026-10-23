namespace HomeSpeaker.Server2.Settings;

/// <summary>
/// One editable setting. A secret's <see cref="Value"/> is <see cref="SettingsForm.MaskedValue"/> when set, never
/// the secret. <see cref="IsOverridden"/> means an environment variable or the command line sets it, so the
/// database value has no effect and the field is read-only.
/// </summary>
public sealed record SettingField(string Key, string Label, string? Value, bool IsSecret, bool IsOverridden);
