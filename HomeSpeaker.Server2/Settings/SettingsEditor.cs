namespace HomeSpeaker.Server2.Settings;

/// <summary>
/// The settings page's working copy, shared by its section components: the fields as loaded, the values being
/// edited (secrets hold <see cref="SettingsForm.MaskedValue"/> until replaced) and the inline errors.
/// </summary>
public sealed class SettingsEditor
{
    private readonly Dictionary<string, SettingField> fields;

    public SettingsEditor(IReadOnlyList<SettingField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        Fields = fields;
        this.fields = fields.ToDictionary(f => f.Key, StringComparer.OrdinalIgnoreCase);
        Values = fields.ToDictionary(f => f.Key, f => f.Value, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<SettingField> Fields { get; }

    public Dictionary<string, string?> Values { get; }

    public Dictionary<string, string> Errors { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Raised when a section changes a value another section shows (units, rooms).</summary>
    public event Action? Changed;

    public string? this[string key]
    {
        get => Values.TryGetValue(key, out var value) ? value : null;
        set
        {
            Values[key] = value;
            Errors.Remove(key);
            Changed?.Invoke();
        }
    }

    public SettingField Field(string key) => fields[key];

    public bool IsOverridden(string key) => fields.TryGetValue(key, out var field) && field.IsOverridden;

    public static SettingsEditor Load(IConfiguration configuration, SqliteSettingsStore? store) =>
        new(SettingsForm.Read(configuration, store?.LoadAll() ?? new Dictionary<string, string>()));

    /// <summary>
    /// Validates every value, records the errors, and returns the database changes for the valid ones (an
    /// invalid field is left out, so the rest still save).
    /// </summary>
    public IReadOnlyDictionary<string, string?> ValidChanges()
    {
        foreach (var (key, message) in SettingsForm.Validate(Values))
        {
            Errors[key] = message;
        }

        var valid = Values.Where(v => !Errors.ContainsKey(v.Key)).ToDictionary(v => v.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);
        return SettingsForm.Changes(Fields, valid);
    }
}
